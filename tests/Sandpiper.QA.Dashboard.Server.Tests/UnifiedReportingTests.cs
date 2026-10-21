using Sandpiper.QA.Dashboard.Server.Models;
using Sandpiper.QA.Dashboard.Server.Services;
using Sandpiper.QA.Dashboard.Server.Services.Analytics;
using Xunit;

namespace Sandpiper.QA.Dashboard.Server.Tests;

public class UnifiedReportingTests
{
    private static NormalizedPoint Point(int id, int suite, string outcome = "passed") => new(id, outcome, 37,
        100, "Synthetic", id, "Config", new("assigned", "Assignee"), null, null, null, null,
        "String", null, null, true, [new(suite, $"Suite {suite}")]);
    private static PlanAnalytics Full()
    {
        var points = new[] { Point(1, 70), Point(2, 100, "failed"), Point(3, 20) with { Memberships = [new(20, "Root"), new(100, "Leaf")] } };
        var metrics = KpiCalculator.Calculate(points);
        var suites = Enumerable.Range(1, 100).Select(i => new SuiteAnalytics(i, $"Suite {i}", i == 1 ? null : i - 1, metrics)).ToArray();
        return new(37, null, metrics, suites, suites.Length, DateTimeOffset.UtcNow) { Points = points };
    }

    [Fact]
    public void DeepHierarchyCategoryInheritanceChildOverrideAndOverlapNeverDoubleCount()
    {
        var source = Full();
        var classified = SuiteCategoryClassifier.Apply(source, [new(37, 1, "Functional"), new(37, 90, "Targeted Regression")]);
        var targeted = ScopeProjector.Project(classified, 50, null, "Targeted Regression");
        Assert.Equal(2, targeted.Total); Assert.Equal(1, targeted.CaseMetrics!.TotalTestCases);
        Assert.Equal(51, targeted.SuiteCount); // arbitrary depth; no folder-name dependency
        Assert.All(targeted.Points, p => Assert.All(p.Memberships, m => Assert.Equal("Targeted Regression", m.TestingCategory)));
        var functional = ScopeProjector.Project(classified, 50, null, "Functional");
        Assert.Single(functional.Points); Assert.Equal(1, functional.Points[0].Id);
        var failed = ScopeProjector.Project(classified, null, null, "Targeted Regression", "failed");
        Assert.Single(failed.Points); Assert.Equal(2, failed.Points[0].Id); Assert.Equal(1, failed.Metrics.Failed);
        Assert.Equal(3, ScopeProjector.Project(classified, null, null).Total);
        Assert.Null(source.Points[0].TestingCategories); // cached source never mutated
    }

    [Fact]
    public void CategoryMustMatchMembershipInsideSelectedSubtree()
    {
        var classified = SuiteCategoryClassifier.Apply(Full(), [new(37, 1, "Functional"), new(37, 90, "Regression")]);
        var selected = ScopeProjector.Project(classified, 90, null, "Functional");
        Assert.Empty(selected.Points); // overlapping point's Functional membership is outside this subtree
    }

    [Fact]
    public void CategoryRulesArePlanScopedAndCannotSilentlyUseMissingSuites()
    {
        var otherPlan = SuiteCategoryClassifier.Apply(Full(), [new(38, 1, "Other Plan")]);
        Assert.All(otherPlan.Points, p => Assert.Equal("Unclassified", Assert.Single(p.TestingCategories!)));
        Assert.Throws<InvalidDataException>(() => SuiteCategoryClassifier.Apply(Full(), [new(37, 999, "Regression")]));
        Assert.Throws<InvalidDataException>(() => SuiteCategoryClassifier.Apply(Full(), [new(37, 1, "A"), new(37, 1, "B")]));
    }

    [Fact]
    public void ClassificationCanBeLimitedToAnExactSuite()
    {
        var result = SuiteCategoryClassifier.Apply(Full(), [new(37, 1, "Root", false)]);
        Assert.All(result.Points, p => Assert.Contains("Unclassified", p.TestingCategories!));
    }

    [Theory]
    [InlineData("2026-11-15T12:00:00Z", "Last Month", "2026-10-01T17:00:00Z", "2026-11-01T18:00:00Z")]
    [InlineData("2026-05-15T12:00:00Z", "Last 2 Months", "2026-03-01T18:00:00Z", "2026-05-01T17:00:00Z")]
    [InlineData("2028-03-15T12:00:00Z", "Last Month", "2028-02-01T18:00:00Z", "2028-03-01T18:00:00Z")]
    public void PreviousCompleteCalendarMonthsRespectUkDstAndCutoff(string now, string preset, string from, string to)
    {
        var window = ReportingClock.Preset(DateTimeOffset.Parse(now), preset, new(18, 0));
        Assert.Equal(DateTimeOffset.Parse(from), window.From); Assert.Equal(DateTimeOffset.Parse(to), window.To);
    }
}
