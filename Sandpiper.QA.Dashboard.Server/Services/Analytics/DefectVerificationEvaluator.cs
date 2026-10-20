using Sandpiper.QA.Dashboard.Server.Models;

namespace Sandpiper.QA.Dashboard.Server.Services.Analytics;

public static class DefectVerificationEvaluator
{
    public static DefectEvidence Evaluate(DefectEvidence bug, DateTimeOffset now)
    {
        var failed = bug.RelatedResults.Where(e => e.Qualifies && e.Outcome == "failed" && e.BugIds.Contains(bug.BugId) && e.PointId != null && e.CompletedAt != null).OrderBy(e => e.CompletedAt).ToList();
        var revisions = bug.Revisions.Where(r => r.ChangedAt != null && r.ChangedAt <= now).OrderBy(r => r.Revision).ToList();
        var resolved = revisions.LastOrDefault(r => r.Category?.Equals("Resolved", StringComparison.OrdinalIgnoreCase) == true);
        var reopened = revisions.Zip(revisions.Skip(1)).LastOrDefault(pair =>
            pair.First.Category is "Resolved" or "Completed" && pair.Second.Category is "Proposed" or "InProgress");
        var status = "Missing verification evidence";
        var evidence = new List<string>();
        ExecutionEvent? retest = null;
        DateTimeOffset? verifiedAt = null;
        if (reopened.Second != null && (resolved == null || reopened.Second.ChangedAt > resolved.ChangedAt))
        { status = "Reopened"; evidence.Add($"Observed revision {reopened.Second.Revision}: {reopened.First.State} → {reopened.Second.State}"); }
        else if (failed.Count > 0 && resolved?.ChangedAt is { } resolvedAt && failed.Any(e => e.CompletedAt < resolvedAt))
        {
            var points = failed.Where(e => e.CompletedAt < resolvedAt).Select(e => e.PointId).ToHashSet();
            retest = bug.RelatedResults.Where(e => e.Qualifies && points.Contains(e.PointId) && e.CompletedAt > resolvedAt && e.CompletedAt <= now).OrderBy(e => e.CompletedAt).LastOrDefault();
            evidence.Add($"Failed result explicitly links bug; resolved revision {resolved.Revision} at {resolvedAt:O}");
            if (retest == null) status = "Awaiting retest";
            else if (retest.Outcome == "failed" && retest.BugIds.Contains(bug.BugId))
            { status = "Failed again"; evidence.Add($"Linked failed result {retest.Key}"); }
            else if (retest.Outcome == "passed")
            {
                status = "Verification candidate";
                var closure = revisions.LastOrDefault(r => r.Category == "Completed" && r.ChangedAt > retest.CompletedAt &&
                    r.ChangedBy?.Key != null && r.ChangedBy.Key == retest.Performer?.Key);
                var ambiguous = bug.RelatedResults.Count(e => e.Qualifies && e.PointId == retest.PointId && e.CompletedAt == retest.CompletedAt) > 1;
                if (!ambiguous && retest.BugIds.Contains(bug.BugId) && retest.Performer != null && closure != null && bug.Category == "Completed")
                { status = "Verified with evidence"; verifiedAt = closure.ChangedAt; evidence.Add($"Passed result {retest.Key} explicitly links bug; same performer completed revision {closure.Revision} after that result."); }
                else evidence.Add("Later pass alone is insufficient: explicit bug link, performer identity and subsequent same-actor completion are required.");
            }
        }
        return bug with { VerificationStatus = status, RetestedBy = retest?.Performer,
            RetestedAt = retest?.CompletedAt, VerificationOutcome = retest?.Outcome,
            VerificationEvidence = evidence, VerifiedAt = verifiedAt,
            AgeDays = bug.CreatedAt is { } created && created <= now ? Math.Round((now - created).TotalDays, 1) : null };
    }
}
