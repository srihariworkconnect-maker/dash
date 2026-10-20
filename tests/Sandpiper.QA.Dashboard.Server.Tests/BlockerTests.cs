using System.Text.Json;
using Sandpiper.QA.Dashboard.Server.Models;
using Sandpiper.QA.Dashboard.Server.Services.Analytics;
using Xunit;

namespace Sandpiper.QA.Dashboard.Server.Tests;
public class BlockerTests
{
    [Fact]
    public void AgingRequiresReconciledLatestResultAndRetainsLowerBoundWarning()
    {
        using var raw = JsonDocument.Parse("""{"id":1,"results":{"outcome":"blocked","lastTestRunId":2,"lastResultId":3}}""");
        var point = TestPointNormalizer.Normalize(raw.RootElement, 1, 1, "Suite");
        var now = DateTimeOffset.Parse("2026-10-20T12:00:00Z");
        var snapshot = new PlanAnalytics(1, null, KpiCalculator.Calculate([point]), [], 0, now) { Points = [point] };
        var result = new ExecutionEvent("2:3", 2, 3, 1, "blocked", "Completed", now.AddDays(-2), null, [], null, true);
        var blocker = BlockerCalculator.Calculate(snapshot, [result]).Single();
        Assert.Equal(2d, blocker.ObservedAgeDays); Assert.Contains("lower-bound", blocker.EvidenceStatus);
        Assert.Null(BlockerCalculator.Calculate(snapshot, [result with { ResultId = 4 }]).Single().ObservedAgeDays);
        Assert.Null(BlockerCalculator.Calculate(snapshot, [result with { CompletedAt = null }]).Single().ObservedAgeDays);
    }
}
