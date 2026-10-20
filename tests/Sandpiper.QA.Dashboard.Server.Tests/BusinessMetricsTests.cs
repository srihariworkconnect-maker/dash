using System.Text.Json;
using Sandpiper.QA.Dashboard.Server.Models;
using Sandpiper.QA.Dashboard.Server.Services.Analytics;
using Xunit;

namespace Sandpiper.QA.Dashboard.Server.Tests;
public class BusinessMetricsTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-10-19T00:00:00Z");
    private static NormalizedPoint Point(int id, int? caseId, string outcome = "passed")
    {
        using var raw = JsonDocument.Parse(JsonSerializer.Serialize(new { id, results = new { outcome } }));
        return TestPointNormalizer.Normalize(raw.RootElement, 1, 1, "Root") with { TestCaseId = caseId };
    }
    private static ExecutionEvent Attempt(int result, int point, string outcome = "failed") =>
        new($"1:{result}", 1, result, point, outcome, "Completed", Start.AddHours(result), new("actor", "Executor"), [], null, true) { BugLinksAvailable = true };

    [Fact]
    public void CasesDeduplicateAcrossSuitesAndConfigurationsWithoutAggregatingPassFail()
    {
        var one = Point(1, 1001); var two = Point(2, 1001, "failed") with { ConfigurationId = 2, Memberships = [new(2, "Root / Child")] };
        var pending = Point(3, 1002, "notexecuted");
        var samePointOtherSuite = one with { Memberships = [new(3, "Another suite")] };
        var result = CaseCalculator.Current([one, two, pending, samePointOtherSuite]);
        Assert.Equal(2, result.TotalTestCases); Assert.Equal(1, result.ExecutedTestCases); Assert.Equal(50d, result.ExecutionCoverage);
        Assert.Equal(new[] { 1001 }, result.ExecutedCaseIds); Assert.Equal(3, KpiCalculator.Calculate([one, two, pending, samePointOtherSuite]).Total);
    }

    [Fact]
    public void CaseExecutionUsesExistingExecutedStatusSetIncludingBlockedAndNa()
    {
        var result = CaseCalculator.Current([Point(1, 10, "blocked"), Point(2, 20, "notApplicable"), Point(3, 30, "notexecuted")]);
        Assert.Equal(3, result.TotalTestCases); Assert.Equal(2, result.ExecutedTestCases);
        Assert.Equal(66.6667, result.ExecutionCoverage);
    }

    [Fact]
    public void UnspecifiedWithoutResultEvidenceUsesDocumentedPendingPolicy()
    {
        var point = Point(1, 10, "unspecified");
        var metrics = CaseCalculator.Current([point]);
        Assert.Equal(1, metrics.TotalTestCases);
        Assert.Equal(0, metrics.ExecutedTestCases);
        Assert.Equal(0d, metrics.ExecutionCoverage);
        Assert.Equal(0, metrics.UnclassifiedCases);
        var history = ExecutionCalculator.Calculate([], [point], new(Start, Start.AddDays(1)), Start);
        Assert.True(history.Complete);
        Assert.Equal(0, history.CaseRetesting!.ReExecutedTestCases);
    }

    [Fact]
    public void UnspecifiedWithResultEvidenceDoesNotClaimZeroExecutedCasesOrZeroHistoricalRepeats()
    {
        var point = Point(1, 10, "unspecified") with { LatestResultId = 7 };
        var metrics = CaseCalculator.Current([point]); Assert.Equal(1, metrics.TotalTestCases);
        Assert.Null(metrics.ExecutedTestCases); Assert.Null(metrics.ExecutionCoverage); Assert.Equal(1, metrics.UnclassifiedCases);
        var history = ExecutionCalculator.Calculate([], [point], new(Start, Start.AddDays(1)), Start);
        Assert.False(history.Complete); Assert.Null(history.CaseRetesting!.ReExecutedTestCases);
    }

    [Fact]
    public void MissingCaseMappingWithholdsTotalsAndKeepsObservedEvidence()
    {
        var result = CaseCalculator.Current([Point(1, null), Point(2, 10)]);
        Assert.Null(result.TotalTestCases); Assert.Null(result.ExecutedTestCases); Assert.Null(result.ExecutionCoverage);
        Assert.Equal(1, result.KnownTestCases); Assert.Equal("Incomplete", result.State);
    }

    [Fact]
    public void TwoReexecutedConfigurationsOfSameCaseCountOneCaseAndDeduplicateAttempts()
    {
        var points = new[] { Point(1, 1001), Point(2, 1001), Point(3, 1002, "notexecuted") };
        var attempts = new[] { Attempt(1, 1), Attempt(2, 1), Attempt(3, 1, "passed"), Attempt(4, 2), Attempt(5, 2, "blocked") };
        var activity = ExecutionCalculator.Calculate([..attempts, attempts[4]], points, new(Start, Start.AddDays(1)), Start);
        Assert.True(activity.Complete); Assert.Equal(2, activity.ReExecutedPoints); Assert.Equal(3, activity.RepeatEvents);
        Assert.Equal(1, activity.CaseRetesting!.ReExecutedTestCases); Assert.Equal(5, activity.CaseRetesting.Cases.Single().Attempts.Count);
        Assert.Equal(new[] { 1, 2 }, activity.CaseRetesting.Cases.Single().PointIds);
        Assert.Equal(2, CaseCalculator.Current(points).TotalTestCases); Assert.Equal(3, KpiCalculator.Calculate(points).Total);
    }

    [Fact]
    public void PriorDayAttemptIsKeptForCaseDrillAndTodaysRepeatDoesNotInflateTotals()
    {
        var activity = ExecutionCalculator.Calculate([Attempt(1, 1), Attempt(25, 1, "passed")], [Point(1, 10)],
            new(Start.AddDays(1), Start.AddDays(2)), Start);
        Assert.Single(activity.Events); Assert.Equal(1, activity.RepeatEvents);
        Assert.Equal(1, activity.CaseRetesting!.ReExecutedTestCases); Assert.Equal(2, activity.CaseRetesting.Cases.Single().Attempts.Count);
    }

    [Fact]
    public void MissingMappingDoesNotTurnKnownRepeatPointIntoZeroCases()
    {
        var activity = ExecutionCalculator.Calculate([Attempt(1, 1), Attempt(2, 1)], [Point(1, null)], new(Start, Start.AddDays(1)), Start);
        Assert.Equal(1, activity.ReExecutedPoints); Assert.Null(activity.CaseRetesting!.ReExecutedTestCases);
        Assert.Equal(1, activity.CaseRetesting.MissingCaseMappings); Assert.Equal("Incomplete", activity.CaseRetesting.State);
    }

    [Fact]
    public void EmptyHistoryCannotVerifyZeroWhenCurrentExecutionHasNoMatchingRetainedEvidence()
    {
        var point = Point(1, 10) with { LatestRunId = 1, LatestResultId = 8, LatestExecutionDate = Start.AddHours(1) };
        var activity = ExecutionCalculator.Calculate([], [point], new(Start, Start.AddDays(1)), Start);
        Assert.False(activity.Complete); Assert.Null(activity.ReExecutedPoints); Assert.Null(activity.CaseRetesting!.ReExecutedTestCases);
        Assert.Contains(activity.Evidence!.Reasons, reason => reason.Contains("latest-result"));
    }

    [Fact]
    public void SuccessfullyCollectedEmptyScopeCanVerifyZeroButZeroDenominatorHasNoRate()
    {
        var activity = ExecutionCalculator.Calculate([], [Point(1, 10, "notexecuted")], new(Start, Start.AddDays(1)), Start);
        Assert.True(activity.Complete); Assert.Equal(0, activity.CaseRetesting!.ReExecutedTestCases);
        var empty = CaseCalculator.Current([]); Assert.Equal(0, empty.TotalTestCases); Assert.Null(empty.ExecutionCoverage);
    }
}
