using Sandpiper.QA.Dashboard.Server.Models;
using Sandpiper.QA.Dashboard.Server.Services.Analytics;
using Xunit;

namespace Sandpiper.QA.Dashboard.Server.Tests;
public class DefectVerificationTests
{
    [Theory]
    [InlineData(false, false, "Verification candidate")]
    [InlineData(true, false, "Verification candidate")]
    [InlineData(true, true, "Verified with evidence")]
    public void PassRequiresExplicitLinkAndSamePerformerCompletion(bool link, bool closure, string expected)
    {
        var start = DateTimeOffset.Parse("2026-10-18T17:00:00Z");
        var actor = new SourceIdentity("actor", "Tester");
        var failed = new ExecutionEvent("1:1", 1, 1, 100, "failed", "Completed", start, actor, [10], null, true);
        var passed = new ExecutionEvent("1:2", 1, 2, 100, "passed", "Completed", start.AddHours(2), actor, link ? [10] : [], null, true);
        var revisions = new List<WorkItemRevision> { new(1, "Resolved", "Resolved", start.AddHours(1), null) };
        if (closure) revisions.Add(new(2, "Closed", "Completed", start.AddHours(3), actor));
        var bug = new DefectEvidence(10, "Bug", actor, start, null, "Closed", "Completed", null, null,
            revisions, [failed, passed], "Unknown", null, null, null, [], null);
        Assert.Equal(expected, DefectVerificationEvaluator.Evaluate(bug, start.AddDays(1)).VerificationStatus);
    }

    [Fact]
    public void EmptyQualityEvidenceCannotPassReadiness()
    {
        var report = ExportTests.Report();
        var quality = new QualityEvidence(false, "Unavailable", DateTimeOffset.UtcNow, [], new Dictionary<string, string>(),
            [], [], [], null, null, "Not assessed", [], null, []);
        var result = QualityCalculator.Assess(report.CurrentSnapshot, report.Activity, quality, new() { Enabled = true });
        Assert.StartsWith("Cannot assess", result.ReleaseReadiness);
        Assert.Null(result.Forecast);
    }

    [Fact]
    public void DisabledThresholdsAlwaysMeanNotAssessedAndCannotReusePriorAssessmentText()
    {
        var report = ExportTests.Report();
        var quality = new QualityEvidence(true, "Inspected", DateTimeOffset.UtcNow, [], new Dictionary<string, string>(),
            [], [], [], 0, 0, "Prior assessment text", [], null, []) { RelationshipsComplete = true };
        var result = QualityCalculator.Assess(report.CurrentSnapshot, report.Activity, quality, new());
        Assert.StartsWith("Not Assessed", result.ReleaseReadiness);
    }
}
