using Sandpiper.QA.Dashboard.Server.Models;

namespace Sandpiper.QA.Dashboard.Server.Services.Analytics;
public static class HistoryEvidenceEvaluator
{
    public static EvidenceAssessment Assess(IReadOnlyList<NormalizedPoint> points, IReadOnlyList<ExecutionEvent> events, ReportingWindow? scope = null)
    {
        var missing = 0; var unknown = 0;
        var indexed = events.GroupBy(e => (e.RunId, e.ResultId)).ToDictionary(g => g.Key, g => g.First());
        foreach (var point in points.Where(p => ExecutionCalculator.Qualifies(p.Outcome, "Completed") || KpiCalculator.Calculate([p]).Unknown > 0))
        {
            if (scope != null && point.LatestExecutionDate is { } at && (at < scope.From || at >= scope.To)) continue;
            if (point.LatestRunId is > 0 && point.LatestResultId is > 0)
            {
                if (!indexed.TryGetValue((point.LatestRunId.Value, point.LatestResultId.Value), out var result) || result.PointId != point.Id ||
                    !result.Qualifies || result.CompletedAt == null) missing++;
            }
            else if (!events.Any(e => e.PointId == point.Id && e.Qualifies && e.CompletedAt != null)) unknown++;
        }
        var reasons = new List<string>();
        if (missing > 0) reasons.Add($"{missing} current latest-result references are absent, undated or inconsistent in retained history.");
        if (unknown > 0) reasons.Add($"{unknown} executed points have neither reconcilable result references nor dated retained attempts.");
        return new(reasons.Count == 0 ? "Verified" : "Incomplete", reasons);
    }
}
