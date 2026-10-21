using System.Text.Json;
using Sandpiper.QA.Dashboard.Server.Models;
using Sandpiper.QA.Dashboard.Server.Services.Analytics;
using Xunit;

namespace Sandpiper.QA.Dashboard.Server.Tests;

public class ReexecutionReportingTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-10-01T00:00:00Z");
    private static readonly ReportingWindow Window = new(Start, Start.AddDays(2));
    private static NormalizedPoint Point(int id = 1) => new(id, "passed", 1, 100, "Case", 1, "Browser",
        null, null, null, null, null, "String", "\"passed\"", null, true, [new(1, "Root / Suite")]);
    private static ExecutionEvent Attempt(int result, string outcome = "passed", int point = 1, DateTimeOffset? at = null) =>
        new($"1:{result}", 1, result, point, outcome, "Completed", at ?? Start.AddHours(result),
            new("tester", "Tester"), [], null, true);

    [Theory]
    [InlineData("passed")]
    [InlineData("failed")]
    [InlineData("blocked")]
    public void AnyQualifyingPreviousOutcomeCountsAndDuplicatesDoNotInflate(string previous)
    {
        var first = Attempt(1, previous); var last = Attempt(2);
        var result = ReexecutionCalculator.Calculate([first, last, last with { Key = "alternate transport key" }], [Point()], Window);
        Assert.Equal("Verified", result.State);
        Assert.Equal(1, result.ReexecutedTestPoints); Assert.Equal(1, result.AdditionalAttempts);
        var row = Assert.Single(result.Points);
        Assert.Equal(2, row.ExecutionCount); Assert.Equal(1, row.ReexecutionCount);
        Assert.Equal(new[] { previous, "passed" }, row.Attempts.Select(a => a.Outcome));
        Assert.False(row.Attempts[0].IsRepeat); Assert.True(row.Attempts[1].IsRepeat);
    }

    [Fact]
    public void SingleExecutionIsNotReexecution()
    {
        var result = ReexecutionCalculator.Calculate([Attempt(1)], [Point()], Window);
        Assert.Equal(0, result.ReexecutedTestPoints); Assert.Equal(0, result.AdditionalAttempts); Assert.Empty(result.Points);
    }

    [Fact]
    public void RangeIncludesStartExcludesEndAndIgnoresOlderBaseline()
    {
        var events = new[] { Attempt(1, at: Start.AddTicks(-1)), Attempt(2, at: Start),
            Attempt(3, at: Window.To.AddTicks(-1)), Attempt(4, at: Window.To) };
        var result = ExecutionCalculator.Calculate(events, [Point()], Window, Start.AddDays(-10));
        Assert.Equal(2, result.Reexecution!.Points.Single().ExecutionCount);
        Assert.Equal(1, result.Reexecution.AdditionalAttempts);
        Assert.Equal(3, result.Points.Single().ExecutionCount); // legacy baseline retained
    }

    [Fact]
    public void MultiplePointsForSameCaseRemainSeparateAndAdditionalAttemptsSum()
    {
        var result = ReexecutionCalculator.Calculate([Attempt(1), Attempt(2), Attempt(3), Attempt(4, point: 2), Attempt(5, point: 2)], [Point(), Point(2)], Window);
        Assert.Equal(2, result.ReexecutedTestPoints); Assert.Equal(3, result.AdditionalAttempts);
        Assert.All(result.Points, p => Assert.Equal(100, p.TestCaseId));
    }

    [Fact]
    public void ConflictingResultIdentityFailsRatherThanChoosingOne()
    {
        Assert.Throws<InvalidDataException>(() => ReexecutionCalculator.Calculate(
            [Attempt(1), Attempt(1, "failed")], [Point()], Window));
    }

    [Fact]
    public void UnknownTimestampWithholdsCompleteTotals()
    {
        var result = ReexecutionCalculator.Calculate([Attempt(1), Attempt(2) with { CompletedAt = null }], [Point()], Window);
        Assert.Equal("Incomplete", result.State); Assert.Null(result.AdditionalAttempts); Assert.Null(result.ReexecutedTestPoints);
    }

    [Theory]
    [InlineData("0001-01-01T00:00:00Z")]
    [InlineData("0001-01-01T00:00:00")]
    [InlineData("0001-01-01T00:00:00.0000000+00:00")]
    [InlineData("invalid")]
    public void SentinelOrInvalidDateIsNotExecutionEvidence(string value)
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new { id = 1,
            results = new { outcome = "unspecified", lastResultDetails = new { dateCompleted = value } } }));
        var point = TestPointNormalizer.Normalize(json.RootElement, 1, 1, "Suite");
        Assert.Null(point.LatestExecutionDate);
        Assert.Equal(1, KpiCalculator.Calculate([point]).Pending);
        Assert.Null(TestPointNormalizer.ExecutionDate(value));
    }

    [Fact]
    public void RealDatePreservesInstantAndCompletedResultStillPreventsPending()
    {
        Assert.Equal(Start, TestPointNormalizer.ExecutionDate("2026-10-01T01:00:00+01:00"));
        var point = Point() with { Outcome = "unspecified", LatestResultId = 9,
            LatestExecutionDate = TestPointNormalizer.ExecutionDate("0001-01-01T00:00:00Z") };
        Assert.Equal(1, KpiCalculator.Calculate([point]).Unknown);
    }

    [Fact]
    public void TesterScopeCountsOnlyThatTestersAttempts()
    {
        var result = ReexecutionCalculator.Calculate([Attempt(1), Attempt(2) with { Performer = new("other", "Other") }], [Point()], Window, "tester");
        Assert.Equal(0, result.ReexecutedTestPoints); Assert.Equal(0, result.AdditionalAttempts);
    }
}
