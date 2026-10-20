using Sandpiper.QA.Dashboard.Server.Models;
using Sandpiper.QA.Dashboard.Server.Services.Analytics;
using Xunit;

namespace Sandpiper.QA.Dashboard.Server.Tests;

public class ReadinessTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-10-19T17:00:00Z");

    private static (PlanAnalytics Snapshot, ExecutionIntelligence Activity) Scope(IEnumerable<TestPointSnapshot> points)
    {
        var snapshot = new PlanAnalytics(1, null, KpiCalculator.Calculate(points), [], 0, Start);
        var activity = ExecutionCalculator.Calculate([], [], new(Start, Start.AddDays(1)), Start);
        return (snapshot, activity);
    }

    private static QualityEvidence Quality(int? openDefects) => new(true, "Live schema inspected", Start, [],
        new Dictionary<string, string>(), [], [], [], openDefects, 0, "seed", [], null, []) { RelationshipsComplete = true };

    [Fact]
    public void DisabledRulesNeverSilentlyEnableAGate()
    {
        var (snapshot, activity) = Scope(Enumerable.Range(1, 100).Select(i => new TestPointSnapshot(i, "passed")));
        var result = QualityCalculator.Assess(snapshot, activity, Quality(0), new() { Enabled = false });
        Assert.StartsWith("Not Assessed", result.ReleaseReadiness);
    }

    [Fact]
    public void AllIndicatorsMetIsGreenWithHumanReview()
    {
        var points = Enumerable.Range(1, 100).Select(i => new TestPointSnapshot(i, i == 100 ? "failed" : "passed"));
        var (snapshot, activity) = Scope(points);
        var result = QualityCalculator.Assess(snapshot, activity, Quality(0), new() { Enabled = true });
        Assert.StartsWith("Green", result.ReleaseReadiness);
        Assert.Contains("human review required", result.ReleaseReadiness);
    }

    [Fact]
    public void OpenDefectsOverThresholdIsRed()
    {
        var (snapshot, activity) = Scope(Enumerable.Range(1, 100).Select(i => new TestPointSnapshot(i, "passed")));
        var result = QualityCalculator.Assess(snapshot, activity, Quality(1), new() { Enabled = true });
        Assert.StartsWith("Red", result.ReleaseReadiness);
    }

    [Fact]
    public void LowCoverageWithPassingAndNoDefectsIsAmber()
    {
        var points = Enumerable.Range(1, 100).Select(i => new TestPointSnapshot(i, i <= 90 ? "passed" : "notExecuted"));
        var (snapshot, activity) = Scope(points);
        var result = QualityCalculator.Assess(snapshot, activity, Quality(0), new() { Enabled = true });
        Assert.StartsWith("Amber", result.ReleaseReadiness);
    }

    [Fact]
    public void IncompleteEvidenceCannotBeAssessedEvenWhenEnabled()
    {
        var (snapshot, activity) = Scope(Enumerable.Range(1, 100).Select(i => new TestPointSnapshot(i, "passed")));
        var withheld = Quality(null); // open-defect total unavailable
        var result = QualityCalculator.Assess(snapshot, activity, withheld, new() { Enabled = true });
        Assert.StartsWith("Cannot assess", result.ReleaseReadiness);
    }
}
