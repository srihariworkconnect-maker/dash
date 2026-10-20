using Sandpiper.QA.Dashboard.Server.Models;

namespace Sandpiper.QA.Dashboard.Server.Services.Analytics;
public static class BlockerCalculator
{
    public static IReadOnlyList<BlockerEvidence> Calculate(PlanAnalytics snapshot, IReadOnlyList<ExecutionEvent> events) =>
        snapshot.Points.Where(p => KpiCalculator.Normalize(p.Outcome) == "blocked").Select(point =>
        {
            var history = events.Where(e => e.PointId == point.Id && e.Qualifies && e.CompletedAt <= snapshot.RetrievedAt)
                .DistinctBy(e => e.Key).OrderByDescending(e => e.CompletedAt).ToArray();
            if (history.Length == 0 || history[0].Outcome != "blocked" || point.LatestRunId != history[0].RunId || point.LatestResultId != history[0].ResultId ||
                events.Any(e => e.PointId == point.Id && e.Qualifies && e.CompletedAt == null))
                return new BlockerEvidence(point.Id, null, null, "Unknown — latest current result cannot be reconciled with dated history");
            var streak = history.TakeWhile(e => e.Outcome == "blocked").ToArray();
            var since = streak.Last().CompletedAt;
            if (history.GroupBy(e => e.CompletedAt).Any(g => g.Select(e => e.Outcome).Distinct().Count() > 1))
                return new BlockerEvidence(point.Id, null, null, "Unknown — equal-time outcomes do not establish blocker chronology");
            return new BlockerEvidence(point.Id, since, since == null ? null : Math.Round((snapshot.RetrievedAt - since.Value).TotalDays, 2),
                streak.Length == history.Length ? "Observed blocked since; lower-bound evidence only, prior state unavailable"
                : "Observed blocked streak after prior non-blocked result; overwritten or missing attempts cannot be reconstructed");
        }).ToArray();
}
