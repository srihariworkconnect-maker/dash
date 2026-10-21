using Sandpiper.QA.Dashboard.Server.Models;

namespace Sandpiper.QA.Dashboard.Server.Services.Analytics;

// Selected reporting range only; independent of the legacy repeat baseline used by DSR/TCR.
public static class ReexecutionCalculator
{
    public static ReexecutionReport Calculate(IEnumerable<ExecutionEvent> source,
        IReadOnlyList<NormalizedPoint> points, ReportingWindow window, string? tester = null)
    {
        if (window.From >= window.To) throw new ArgumentException("Reporting start must precede end.");
        var map = TestPointNormalizer.Merge(points).ToDictionary(p => p.Id);
        var raw = source.ToArray();
        var events = raw.GroupBy(e => (e.RunId, e.ResultId)).Select(g =>
        {
            var first = g.First();
            if (first.RunId <= 0 || first.ResultId <= 0 || g.Any(e => e.PointId != first.PointId ||
                e.Outcome != first.Outcome || e.State != first.State || e.CompletedAt != first.CompletedAt ||
                e.Performer?.Key != first.Performer?.Key || e.PointMapping != first.PointMapping))
                throw new InvalidDataException("Conflicting or missing execution identity.");
            return first with { Key = $"{first.RunId}:{first.ResultId}",
                Qualifies = ExecutionCalculator.Qualifies(first.Outcome, first.State) };
        }).ToArray();
        var excluded = new Dictionary<string, int>();
        void Exclude(string reason) => excluded[reason] = excluded.GetValueOrDefault(reason) + 1;
        var undated = 0; var unlinked = 0; var unknownPoint = 0;
        var scoped = new List<ExecutionEvent>();
        foreach (var e in events)
        {
            if (!string.Equals(e.State, "completed", StringComparison.OrdinalIgnoreCase)) { Exclude("NotCompleted"); continue; }
            if (!e.Qualifies) { Exclude("NonQualifyingOutcome"); continue; }
            if (e.PointId is > 0 && !map.ContainsKey(e.PointId.Value) && e.PointMapping != "UnknownPoint")
            { Exclude("OutsideSelectedPoints"); continue; }
            if (e.CompletedAt == null || e.CompletedAt.Value.Year <= 1) { undated++; Exclude("MissingOrSentinelDate"); continue; }
            if (e.CompletedAt < window.From) { Exclude("BeforeStart"); continue; }
            if (e.CompletedAt >= window.To) { Exclude("AtOrAfterEnd"); continue; }
            if (e.PointMapping == "UnknownPoint") { unknownPoint++; Exclude("UnknownPointIdentity"); continue; }
            if (e.PointId is not > 0) { unlinked++; Exclude(e.PointMapping ?? "MissingPointLink"); continue; }
            if (!string.IsNullOrEmpty(tester) && (e.Performer?.Key ?? "unattributed") != tester)
            { Exclude("DifferentExecutor"); continue; }
            scoped.Add(e);
        }
        var review = HistoryEvidenceEvaluator.Assess(points, events, window);
        var rows = scoped.GroupBy(e => e.PointId!.Value).Where(g => g.Count() >= 2).OrderBy(g => g.Key).Select(g =>
            {
                var point = map[g.Key];
                var attempts = g.OrderBy(e => e.CompletedAt).ThenBy(e => e.RunId).ThenBy(e => e.ResultId)
                    .Select((e, i) => e with { IsRepeat = i > 0, Transition = null }).ToArray();
                return new ReexecutionPoint(g.Key, point.TestCaseId, point.Title, point.Memberships,
                    attempts.Length, attempts.Length - 1, attempts);
            }).ToArray();
        var reasons = new List<string>(review.Reasons);
        if (undated > 0) reasons.Add($"{undated} qualifying results lack a valid execution timestamp.");
        if (unlinked > 0) reasons.Add($"{unlinked} qualifying results lack a test-point link.");
        if (unknownPoint > 0) reasons.Add($"{unknownPoint} qualifying results reference points absent from current plan membership; historical scope cannot be reconciled.");
        var complete = reasons.Count == 0;
        var additional = rows.Sum(p => p.ReexecutionCount);
        return new(window, complete ? rows.Length : null, complete ? additional : null,
            rows.Length, additional, complete ? "Verified" : "Incomplete", reasons, rows) {
                Diagnostics = new(raw.Length, events.Length, raw.Length - events.Length, scoped.Count,
                    scoped.Select(e => e.PointId).Distinct().Count(), rows.Length, additional, excluded,
                    events.GroupBy(e => e.PointMapping ?? "LegacyDirectOrMissing").ToDictionary(g => g.Key, g => g.Count())) };
    }
}
