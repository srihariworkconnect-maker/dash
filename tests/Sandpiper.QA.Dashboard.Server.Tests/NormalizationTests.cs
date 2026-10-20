using System.Text.Json;
using Sandpiper.QA.Dashboard.Server.Services.Analytics;
using Xunit;

namespace Sandpiper.QA.Dashboard.Server.Tests;
public class NormalizationTests
{
    [Theory]
    [InlineData("\"unspecified\"", "String", "unspecified", 1)]
    [InlineData("3", "Number", null, 0)]
    [InlineData("null", "Null", null, 1)]
    public void UnrunOutcomesArePendingButMalformedStaysUnknown(string raw, string type, string? expected, int expectedPending)
    {
        using var json = JsonDocument.Parse($"{{\"id\":1,\"results\":{{\"outcome\":{raw}}}}}");
        var point = TestPointNormalizer.Normalize(json.RootElement, 10, 20, "root / suite");
        Assert.Equal(type, point.OutcomeJsonType);
        Assert.Equal(raw, point.RawOutcome);
        Assert.Equal(expected, point.Outcome);
        var metrics = KpiCalculator.Calculate([point]);
        Assert.Equal(expectedPending, metrics.Pending);
        // Whatever the classification, a single point must land in exactly one category.
        Assert.Equal(1, metrics.Pending + metrics.Executed + metrics.InProgress + metrics.Unknown);
        Assert.Contains("Awaiting", TestPointNormalizer.Reconcile([point]).ValidationState);
    }

    [Fact]
    public void UnspecifiedOutcomeWithCompletedResultIsUnknownNotPending()
    {
        using var json = JsonDocument.Parse("""{"id":1,"results":{"outcome":"unspecified","lastResultId":7}}""");
        var point = TestPointNormalizer.Normalize(json.RootElement, 10, 20, "root / suite");
        var metrics = KpiCalculator.Calculate([point]);
        Assert.Equal(0, metrics.Pending);
        Assert.Equal(1, metrics.Unknown);
    }

    [Fact]
    public void PointCaseAndConfigurationIdentityAreSeparate()
    {
        using var json = JsonDocument.Parse("""{"id":1,"testCaseReference":{"id":2,"name":"Case","assignedTo":{"id":"identity","displayName":"Tester"}},"configuration":{"id":3,"name":"Browser"}}""");
        var point = TestPointNormalizer.Normalize(json.RootElement, 10, 20, "root");
        Assert.Equal(2, point.TestCaseId);
        Assert.Equal(3, point.ConfigurationId);
        Assert.Equal("identity", point.AssignedTester!.Key);
        Assert.Null(point.LatestExecutedBy);
        Assert.Equal("Missing", point.OutcomeJsonType);
    }

    [Fact]
    public void OverlappingMembershipMergesButConflictingConfigurationOrResultCannotHide()
    {
        using var json = JsonDocument.Parse("""{"id":1,"testCaseReference":{"id":10},"configuration":{"id":2},"results":{"outcome":"passed","lastResultId":7}}""");
        var first = TestPointNormalizer.Normalize(json.RootElement, 1, 1, "Root");
        var second = first with { Memberships = [new(2, "Root / Child")] };
        Assert.Equal(2, TestPointNormalizer.Merge([first, second]).Single().Memberships.Count);
        Assert.Throws<InvalidDataException>(() => TestPointNormalizer.Merge([first, second with { ConfigurationId = 3 }]));
        Assert.Throws<InvalidDataException>(() => TestPointNormalizer.Merge([first, second with { LatestResultId = 8 }]));
    }
}
