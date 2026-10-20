using Sandpiper.QA.Dashboard.Server.Models;

namespace Sandpiper.QA.Dashboard.Server.Services.Analytics;

public static class ExecutionCalculator
{
    public static bool Qualifies(string? outcome, string? state) =>
        string.Equals(state, "completed", StringComparison.OrdinalIgnoreCase) &&
        KpiCalculator.Normalize(outcome) is "passed" or "failed" or "blocked" or "timeout" or "error" or "aborted" or "inconclusive";

    public static ExecutionIntelligence Calculate(IEnumerable<ExecutionEvent> source,
        IReadOnlyList<NormalizedPoint> points, ReportingWindow window, DateTimeOffset baselineFrom, string? tester = null)
    {
        if (baselineFrom > window.From || window.From >= window.To) throw new ArgumentException("Baseline must start no later than the activity window.");
        var ids = points.Select(p => p.Id).ToHashSet();
        var all = source.GroupBy(e => e.Key).Select(g =>
        {
            var first = g.First();
            if (g.Any(e => e.RunId != first.RunId || e.ResultId != first.ResultId || e.PointId != first.PointId ||
                e.Outcome != first.Outcome || e.CompletedAt != first.CompletedAt || e.Performer?.Key != first.Performer?.Key))
                throw new InvalidDataException("Conflicting execution identities.");
            return first with { IsRepeat = false, Transition = null };
        }).Where(e => e.PointId == null || ids.Contains(e.PointId.Value)).ToList();
        var missingDates = all.Count(e => e.Qualifies && e.CompletedAt == null);
        var baseline = all.Where(e => e.CompletedAt >= baselineFrom && e.CompletedAt < window.To).ToList();
        var missingLinks = baseline.Count(e => e.Qualifies && e.PointId == null);
        var annotated = new Dictionary<string, ExecutionEvent>();
        var ambiguous = 0;
        foreach (var group in baseline.Where(e => e.Qualifies && e.PointId != null).GroupBy(e => e.PointId))
        {
            ExecutionEvent? previous = null;
            foreach (var current in group.OrderBy(e => e.CompletedAt).ThenBy(e => e.RunId).ThenBy(e => e.ResultId))
            {
                string? transition = null;
                if (previous != null && previous.CompletedAt == current.CompletedAt) ambiguous++;
                else if (previous != null && previous.Outcome is "passed" or "failed" && current.Outcome is "passed" or "failed")
                    transition = $"{previous.Outcome} → {current.Outcome}";
                annotated[current.Key] = current with { IsRepeat = previous != null, Transition = transition };
                previous = current;
            }
        }
        var events = baseline.Where(e => e.CompletedAt >= window.From && (string.IsNullOrEmpty(tester) || e.Performer?.Key == tester))
            .Select(e => annotated.GetValueOrDefault(e.Key, e)).OrderByDescending(e => e.CompletedAt).ToList();
        var qualifying = events.Where(e => e.Qualifies && e.PointId != null).ToList();
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        string Day(ExecutionEvent e) => TimeZoneInfo.ConvertTime(e.CompletedAt!.Value, zone).ToString("yyyy-MM-dd");
        var productivity = qualifying.GroupBy(e => (Key: e.Performer?.Key ?? "unattributed", Day: Day(e)))
            .Select(g => new TesterActivity(g.Key.Key, g.First().Performer?.DisplayName ?? "Unattributed", g.Key.Day,
                points.Count(p => p.AssignedTester?.Key == g.Key.Key),
                points.Count(p => p.AssignedTester?.Key == g.Key.Key && KpiCalculator.Normalize(p.Outcome) is "none" or "active" or "notexecuted"),
                g.Count(), g.Select(e => e.PointId).Distinct().Count(), g.Count(e => e.Outcome == "passed"),
                g.Count(e => e.Outcome == "failed"), g.Count(e => e.Outcome == "blocked"),
                g.Count(e => e.IsRepeat), g.Where(e => e.IsRepeat).Select(e => e.PointId).Distinct().Count())).ToList();
        var daily = qualifying.GroupBy(Day).OrderBy(g => g.Key).Select(g => new DailyActivity(g.Key, g.Count(),
            g.Select(e => e.PointId).Distinct().Count(), g.Count(e => e.IsRepeat), g.Count(e => e.Outcome == "passed"),
            g.Count(e => e.Outcome == "failed"), g.Count(e => e.Outcome == "blocked"))).ToList();
        var pointCounts = baseline.Where(e => e.Qualifies && e.PointId != null && (string.IsNullOrEmpty(tester) || e.Performer?.Key == tester))
            .Select(e => annotated[e.Key]).GroupBy(e => e.PointId!.Value)
            .Select(g => new PointActivity(g.Key, g.Count(), g.Count(e => e.IsRepeat))).ToList();
        var complete = missingDates == 0 && missingLinks == 0;
        return new(window, new(baselineFrom, window.To), events, productivity, daily, pointCounts,
            complete ? pointCounts.Count(p => p.ExecutionCount > 1) : null,
            qualifying.Count(e => e.IsRepeat), qualifying.Select(e => e.PointId).Distinct().Count(),
            missingLinks, missingDates, ambiguous, complete, "Actual result runBy identity; assignment is separate current state.",
            ["Awaiting VDI validation; retained result identities are not an audit of overwritten attempts.",
             "Current point membership is used. Missing identity is unattributed; missing history is not zero.",
             "Assigned/pending counts are current state, not historical daily assignment.",
             "Equal-time records count as distinct attempts but do not establish transition chronology."]);
    }
}
