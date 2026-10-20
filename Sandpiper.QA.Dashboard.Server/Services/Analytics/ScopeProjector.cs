using Sandpiper.QA.Dashboard.Server.Models;

namespace Sandpiper.QA.Dashboard.Server.Services.Analytics;
public static class ScopeProjector
{
    public static PlanAnalytics Project(PlanAnalytics full, int? selected, string? tester)
    {
        if (selected != null && !full.Suites.Any(s => s.SuiteId == selected)) throw new ArgumentException("Suite is not in selected plan.");
        HashSet<int> Descendants(int root)
        {
            HashSet<int> ids = [root]; var changed = true;
            while (changed) { changed = false; foreach (var suite in full.Suites)
                if (suite.ParentSuiteId is { } parent && ids.Contains(parent)) changed |= ids.Add(suite.SuiteId); }
            return ids;
        }
        var included = selected == null ? full.Suites.Select(s => s.SuiteId).ToHashSet() : Descendants(selected.Value);
        var points = full.Points.Where(p => p.Memberships.Any(m => included.Contains(m.SuiteId)) &&
            (string.IsNullOrEmpty(tester) || (p.AssignedTester?.Key ?? "unattributed") == tester))
            .Select(p => p with { Memberships = p.Memberships.Where(m => included.Contains(m.SuiteId)).ToArray() }).ToArray();
        var suites = full.Suites.Where(s => included.Contains(s.SuiteId)).Select(s =>
        {
            var descendants = Descendants(s.SuiteId);
            return s with { Metrics = KpiCalculator.Calculate(points.Where(p => p.Memberships.Any(m => m.SuiteId == s.SuiteId))),
                SubtreeMetrics = KpiCalculator.Calculate(points.Where(p => p.Memberships.Any(m => descendants.Contains(m.SuiteId)))) };
        }).ToArray();
        return full with { SuiteId = selected, Points = points, Suites = suites, SuiteCount = suites.Length,
            Metrics = KpiCalculator.Calculate(points), Reconciliation = TestPointNormalizer.Reconcile(points),
            Assignments = points.GroupBy(p => p.AssignedTester?.Key ?? "unattributed").Select(g => {
                var metrics = KpiCalculator.Calculate(g);
                return new AssignedWorkload(g.Key, g.First().AssignedTester?.DisplayName ?? "Unassigned", metrics.Total, metrics.Pending);
            }).ToArray() };
    }
}
