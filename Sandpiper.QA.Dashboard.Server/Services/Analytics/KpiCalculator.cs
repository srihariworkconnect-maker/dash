using Sandpiper.QA.Dashboard.Server.Models;

namespace Sandpiper.QA.Dashboard.Server.Services.Analytics;

public static class KpiCalculator
{
    // Explicit policy retained for terminal outcomes, now with blocked and N/A.
    // Each raw category is also exposed separately in Statuses.
    private static readonly HashSet<string> Terminal =
        ["passed", "failed", "blocked", "notapplicable", "inconclusive", "timeout", "aborted", "error"];

    public static string Normalize(string? outcome) =>
        string.IsNullOrWhiteSpace(outcome) ? "missing" : outcome.Trim().ToLowerInvariant();

    public static QaMetrics Calculate(IEnumerable<TestPointSnapshot> points)
    {
        var unique = new Dictionary<int, TestPointSnapshot>();
        foreach (var point in points)
        {
            if (point.Id <= 0) throw new InvalidDataException("Invalid test-point identity.");
            if (unique.TryGetValue(point.Id, out var existing) &&
                Normalize(existing.Outcome) != Normalize(point.Outcome))
                throw new InvalidDataException("Conflicting outcomes for the same point; refresh the snapshot.");
            unique[point.Id] = point;
        }
        string[] known = ["passed", "failed", "blocked", "notapplicable", "none", "active",
            "notexecuted", "inprogress", "paused", "inconclusive", "timeout", "aborted",
            "error", "unspecified", "warning", "notimpacted", "missing"];
        var statuses = known.ToDictionary(key => key, _ => 0);
        foreach (var point in unique.Values)
        {
            var key = Normalize(point.Outcome);
            statuses[key] = statuses.GetValueOrDefault(key) + 1;
        }
        var passed = statuses["passed"];
        var failed = statuses["failed"];
        var blocked = statuses["blocked"];
        var na = statuses["notapplicable"];
        var inProgress = statuses["inprogress"] + statuses["paused"];
        var executed = statuses.Where(pair => Terminal.Contains(pair.Key)).Sum(pair => pair.Value);
        var otherCompleted = executed - passed - failed - blocked - na;
        var pending = unique.Values.Count(IsNotExecuted);
        var unknown = unique.Count - executed - inProgress - pending;
        var applicable = unique.Count - na;
        var executedApplicable = executed - na;
        return new(unique.Count, passed, failed, blocked, pending, na, inProgress,
            otherCompleted, unknown, executed, applicable, executedApplicable,
            Rate(executedApplicable, applicable), Rate(passed, executed),
            Rate(passed, passed + failed + blocked), statuses);
    }

    // A point is "pending" (not executed) when Azure DevOps reports a recognised not-run
    // outcome, or an unspecified/absent outcome with no recorded completed result. Malformed
    // outcome payloads (non-string JSON), and points that carry completed-result evidence
    // without a terminal outcome, stay "unknown" so genuinely unclassifiable source data is
    // never silently presented as Pending.
    private static bool IsNotExecuted(TestPointSnapshot point)
    {
        var key = Normalize(point.Outcome);
        if (key is "none" or "active" or "notexecuted") return true;
        if (key is not ("unspecified" or "missing")) return false;
        if (point is NormalizedPoint detailed)
        {
            if (detailed.OutcomeJsonType is not ("String" or "Null" or "Missing")) return false;
            if (detailed.LatestResultId is > 0 || detailed.LatestExecutionDate is not null ||
                string.Equals(detailed.ResultState, "completed", StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    private static double? Rate(int numerator, int denominator) =>
        denominator == 0 ? null : Math.Round(100d * numerator / denominator, 4);

    public static ReexecutionMetrics Reexecution(IEnumerable<ExecutionAttempt> attempts,
        IReadOnlySet<int> pointIds, ReportingWindow window)
    {
        if (window.From >= window.To) throw new ArgumentException("Reporting start must precede end.");
        var distinct = new Dictionary<(int, int), ExecutionAttempt>();
        foreach (var attempt in attempts)
        {
            if (attempt.RunId <= 0 || attempt.ResultId <= 0)
                throw new InvalidDataException("Missing execution-attempt identity.");
            var key = (attempt.RunId, attempt.ResultId);
            if (distinct.TryGetValue(key, out var existing) && existing != attempt)
                throw new InvalidDataException("Conflicting result records; refresh execution history.");
            distinct[key] = attempt;
        }
        var counts = new Dictionary<int, int>();
        int unlinked = 0, undated = 0;
        foreach (var attempt in distinct.Values)
        {
            var outcome = Normalize(attempt.Outcome);
            // Blocked requires a distinct persisted, completed result record, just like passed/failed.
            // N/A remains a status category, not evidence of an execution attempt.
            if (!string.Equals(attempt.State, "completed", StringComparison.OrdinalIgnoreCase) ||
                !Terminal.Contains(outcome) || outcome == "notapplicable") continue;
            if (attempt.PointId is > 0 && !pointIds.Contains(attempt.PointId.Value)) continue;
            if (attempt.CompletedAt == null) { undated++; continue; }
            if (attempt.CompletedAt < window.From || attempt.CompletedAt >= window.To) continue;
            if (attempt.PointId is not > 0) { unlinked++; continue; }
            counts[attempt.PointId.Value] = counts.GetValueOrDefault(attempt.PointId.Value) + 1;
        }
        var reExecuted = counts.Values.Count(count => count > 1);
        var complete = unlinked == 0 && undated == 0;
        return new(complete ? reExecuted : null, reExecuted,
            counts.Values.Sum(count => Math.Max(0, count - 1)), counts.Values.Sum(),
            unlinked, undated, complete, window);
    }
}
