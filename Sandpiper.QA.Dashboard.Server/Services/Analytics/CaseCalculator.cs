using Sandpiper.QA.Dashboard.Server.Models;

namespace Sandpiper.QA.Dashboard.Server.Services.Analytics;
public static class CaseCalculator
{
    public static CaseMetrics Current(IEnumerable<NormalizedPoint> source)
    {
        var points = TestPointNormalizer.Merge(source);
        var missing = points.Count(p => p.TestCaseId is null or <= 0);
        var cases = points.Where(p => p.TestCaseId > 0).GroupBy(p => p.TestCaseId).Select(g => new { Id = g.Key!.Value, Metrics = KpiCalculator.Calculate(g) }).ToArray();
        // This is executed CURRENT point status, including N/A, not a qualifying history attempt.
        var executed = cases.Count(g => g.Metrics.Executed > 0);
        var uncertain = cases.Count(g => g.Metrics.Executed == 0 && g.Metrics.Unknown > 0);
        return new(missing == 0 ? cases.Length : null, missing == 0 && uncertain == 0 ? executed : null,
            missing == 0 && uncertain == 0 && cases.Length > 0 ? Math.Round(100d * executed / cases.Length, 4) : null,
            cases.Length, executed, missing, missing == 0 && uncertain == 0 ? "Verified" : "Incomplete") {
                UnclassifiedCases = uncertain,
                TestCaseIds = cases.Select(g => g.Id).Order().ToArray(),
                ExecutedCaseIds = cases.Where(g => g.Metrics.Executed > 0).Select(g => g.Id).Order().ToArray() };
    }

    public static CaseRetesting Retesting(IReadOnlyList<NormalizedPoint> points, IReadOnlyList<PointActivity> counts,
        IReadOnlyList<ExecutionEvent> baseline, bool historyComplete)
    {
        var map = points.ToDictionary(p => p.Id, p => p.TestCaseId);
        var missing = counts.Count(p => !map.TryGetValue(p.PointId, out var caseId) || caseId is null or <= 0);
        var cases = counts.Where(p => p.ExecutionCount > 1 && map.GetValueOrDefault(p.PointId) > 0)
            .GroupBy(p => map[p.PointId]!.Value).Select(g => {
                var ids = g.Select(p => p.PointId).ToHashSet();
                return new ReexecutedCase(g.Key, ids.Order().ToArray(), baseline.Where(e => e.Qualifies && e.PointId != null && ids.Contains(e.PointId.Value))
                    .DistinctBy(e => e.Key).OrderBy(e => e.CompletedAt).ToArray());
            }).OrderBy(c => c.CaseId).ToArray();
        var complete = historyComplete && missing == 0;
        return new(complete ? cases.Length : null, cases.Length, missing, cases, complete ? "Verified" : "Incomplete");
    }
}
