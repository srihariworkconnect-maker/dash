using System.Text.Json;
using Sandpiper.QA.Dashboard.Server.Services.Analytics;
using Xunit;

namespace Sandpiper.QA.Dashboard.Server.Tests;
public class NormalizationTests
{
    [Theory]
    [InlineData("\"unspecified\"", "String", "unspecified")]
    [InlineData("3", "Number", null)]
    [InlineData("null", "Null", null)]
    public void OutcomeEvidenceIsNotGuessed(string raw, string type, string? expected)
    {
        using var json = JsonDocument.Parse($"{{\"id\":1,\"results\":{{\"outcome\":{raw}}}}}");
        var point = TestPointNormalizer.Normalize(json.RootElement, 10, 20, "root / suite");
        Assert.Equal(type, point.OutcomeJsonType);
        Assert.Equal(raw, point.RawOutcome);
        Assert.Equal(expected, point.Outcome);
        Assert.Equal(0, KpiCalculator.Calculate([point]).Pending);
        Assert.Contains("Awaiting", TestPointNormalizer.Reconcile([point]).ValidationState);
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
