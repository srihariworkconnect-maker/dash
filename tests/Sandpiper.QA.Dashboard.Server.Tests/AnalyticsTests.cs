using Sandpiper.QA.Dashboard.Server.Models;
using Sandpiper.QA.Dashboard.Server.Services.Analytics;
using Xunit;

namespace Sandpiper.QA.Dashboard.Server.Tests;

public class AnalyticsTests
{
    [Fact]
    public void PrimaryIncludesBlockedAndNaButProgressExcludesNa()
    {
        var metrics = KpiCalculator.Calculate([
            new(1, "Passed"), new(2, "passed"), new(3, "failed"),
            new(4, "blocked"), new(5, "notApplicable"), new(6, "notExecuted")]);
        Assert.Equal(6, metrics.Total);
        Assert.Equal(5, metrics.Executed);
        Assert.Equal(40, metrics.PrimaryPassRate);
        Assert.Equal(50, metrics.SecondaryPassRate);
        Assert.Equal(5, metrics.Applicable);
        Assert.Equal(4, metrics.ExecutedApplicable);
        Assert.Equal(80, metrics.ExecutionRate);
    }

    [Fact]
    public void DuplicatePointsDoNotChangeTotals()
    {
        var metrics = KpiCalculator.Calculate([new(1, "passed"), new(1, "Passed")]);
        Assert.Equal(1, metrics.Total);
    }

    [Fact]
    public void ConflictingDuplicateOutcomesFailRatherThanInventSnapshot() =>
        Assert.Throws<InvalidDataException>(() => KpiCalculator.Calculate([new(1, "passed"), new(1, "failed")]));

    [Fact]
    public void EveryStatusRemainsVisibleAndUnknownIsNotExecuted()
    {
        var metrics = KpiCalculator.Calculate([new(1, "paused"), new(2, "inProgress"),
            new(3, "timeout"), new(4, null), new(5, "new-outcome"), new(6, "none")]);
        Assert.Equal(1, metrics.Statuses["paused"]);
        Assert.Equal(1, metrics.Statuses["inprogress"]);
        Assert.Equal(1, metrics.Statuses["timeout"]);
        Assert.Equal(1, metrics.Statuses["missing"]);
        Assert.Equal(1, metrics.Statuses["new-outcome"]);
        Assert.Equal(1, metrics.Executed);
        Assert.Equal(metrics.Total, metrics.Statuses.Values.Sum());
    }

    [Fact]
    public void EmptyAndNaOnlyProgressHasNoArtificialRate()
    {
        var empty = KpiCalculator.Calculate([]);
        Assert.Null(empty.PrimaryPassRate);
        Assert.Null(empty.SecondaryPassRate);
        Assert.Null(empty.ExecutionRate);
        var na = KpiCalculator.Calculate([new(1, "notApplicable")]);
        Assert.Equal(0, na.PrimaryPassRate);
        Assert.Null(na.SecondaryPassRate);
        Assert.Null(na.ExecutionRate);
    }

    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-10-18T17:00:00Z");
    private static readonly ReportingWindow Window = new(Start, Start.AddDays(1));

    [Fact]
    public void BlockedDistinctRecordedAttemptsQualifyButDuplicateRecordsDoNot()
    {
        var first = new ExecutionAttempt(1, 10, 100, "blocked", "Completed", Start);
        var result = KpiCalculator.Reexecution([first, first,
            new(1, 11, 100, "blocked", "Completed", Start.AddHours(1)),
            new(2, 10, 100, "passed", "Completed", Start.AddHours(2)),
            new(3, 10, 100, "blocked", "InProgress", Start.AddHours(3))], new HashSet<int> { 100 }, Window);
        Assert.Equal(1, result.ReExecuted);
        Assert.Equal(3, result.Executions);
        Assert.Equal(2, result.AdditionalExecutions);
    }

    [Fact]
    public void HistoryRespectsWindowAndPointScope()
    {
        var result = KpiCalculator.Reexecution([
            new(1, 1, 100, "passed", "Completed", Start.AddTicks(-1)),
            new(1, 2, 100, "passed", "Completed", Start),
            new(1, 3, 100, "failed", "Completed", Window.To),
            new(1, 4, 200, "passed", "Completed", Start),
            new(1, 5, 100, "notApplicable", "Completed", Start)], new HashSet<int> { 100 }, Window);
        Assert.Equal(1, result.Executions);
        Assert.Equal(0, result.ReExecuted);
    }

    [Fact]
    public void UnlinkedAndUndatedAttemptsWithholdCompleteTotal()
    {
        var result = KpiCalculator.Reexecution([
            new(1, 1, null, "blocked", "Completed", Start),
            new(1, 2, 100, "passed", "Completed", null)], new HashSet<int> { 100 }, Window);
        Assert.Null(result.ReExecuted);
        Assert.False(result.Complete);
        Assert.Equal(1, result.UnlinkedResults);
        Assert.Equal(1, result.UndatedResults);
    }

    [Theory]
    [InlineData("2026-03-29T18:00:00Z", 23)]
    [InlineData("2026-10-25T19:00:00Z", 25)]
    [InlineData("2026-10-19T18:00:00Z", 24)]
    public void UkCutoffUsesLocalCalendarAcrossDst(string now, int hours)
    {
        var window = ReportingClock.LastDsrPeriod(DateTimeOffset.Parse(now), new(18, 0));
        Assert.Equal(hours, (window.To - window.From).TotalHours);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        Assert.Equal(18, TimeZoneInfo.ConvertTime(window.From, zone).Hour);
        Assert.Equal(18, TimeZoneInfo.ConvertTime(window.To, zone).Hour);
    }
}
