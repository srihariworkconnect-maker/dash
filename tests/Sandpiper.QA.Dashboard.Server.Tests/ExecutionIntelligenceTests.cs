using System.Text.Json;
using Sandpiper.QA.Dashboard.Server.Models;
using Sandpiper.QA.Dashboard.Server.Services.Analytics;
using Xunit;

namespace Sandpiper.QA.Dashboard.Server.Tests;
public class ExecutionIntelligenceTests
{
    [Fact]
    public void PriorDayExecutionMakesTodaysAttemptRepeatAndUsesActualPerformer()
    {
        using var json = JsonDocument.Parse("""{"id":1,"testCaseReference":{"id":10,"assignedTo":{"id":"assigned","displayName":"Assignee"}}}""");
        var point = TestPointNormalizer.Normalize(json.RootElement, 1, 1, "Suite");
        var start = DateTimeOffset.Parse("2026-10-19T17:00:00Z");
        var actual = new SourceIdentity("actual", "Performer");
        var prior = new ExecutionEvent("1:1", 1, 1, 1, "failed", "Completed", start.AddDays(-1), actual, [], null, true);
        var today = new ExecutionEvent("1:2", 1, 2, 1, "blocked", "Completed", start.AddHours(1), actual, [], null, true);
        var result = ExecutionCalculator.Calculate([prior, today, today], [point], new(start, start.AddDays(1)), start.AddDays(-2));
        Assert.Equal(1, result.RepeatEvents);
        Assert.Equal(1, result.ReExecutedPoints);
        Assert.Single(result.Events);
        Assert.Equal("actual", result.Productivity.Single().TesterKey);
        Assert.Equal(0, result.Productivity.Single().AssignedPoints);
    }

    [Fact]
    public void MissingLinksDoNotBecomeZeroReexecuted()
    {
        var start = DateTimeOffset.UtcNow;
        var record = new ExecutionEvent("1:1", 1, 1, null, "passed", "Completed", start, null, [], null, true);
        var result = ExecutionCalculator.Calculate([record], [], new(start, start.AddDays(1)), start);
        Assert.Null(result.ReExecutedPoints);
        Assert.Equal(1, result.MissingPointLinks);
    }

    [Fact]
    public void ExplicitUnattributedFilterIncludesUnknownPerformerWithoutUsingAssignee()
    {
        using var json = JsonDocument.Parse("""{"id":1,"results":{"outcome":"passed"}}""");
        var point = TestPointNormalizer.Normalize(json.RootElement, 1, 1, "Suite");
        var start = DateTimeOffset.Parse("2026-10-19T17:00:00Z");
        var record = new ExecutionEvent("1:1", 1, 1, 1, "passed", "Completed", start, null, [], null, true);
        var result = ExecutionCalculator.Calculate([record], [point], new(start, start.AddDays(1)), start, "unattributed");
        Assert.Single(result.Events); Assert.Equal("unattributed", result.Productivity.Single().TesterKey);
        Assert.Equal(1, result.Productivity.Single().AssignedPoints);
    }
}
