using Sandpiper.QA.Dashboard.Server.Models;

namespace Sandpiper.QA.Dashboard.Server.Services.Analytics;

public static class BugActivityCalculator
{
    public static IReadOnlyList<BugActivity> Calculate(IEnumerable<DefectEvidence> defects, ReportingWindow window, string? tester)
    {
        var actions = new List<(SourceIdentity Actor, DateTimeOffset At, string Kind)>();
        void Add(SourceIdentity? actor, DateTimeOffset? at, string kind)
        {
            var identity = actor ?? new("unattributed", "Unattributed");
            if (at is { } date && date >= window.From && date < window.To && (string.IsNullOrEmpty(tester) || identity.Key == tester))
                actions.Add((identity, date, kind));
        }
        foreach (var bug in defects)
        {
            Add(bug.CreatedBy, bug.CreatedAt, "raised");
            var revisions = bug.Revisions.OrderBy(r => r.Revision).ToArray();
            foreach (var (prior, next) in revisions.Zip(revisions.Skip(1)))
            {
                if (prior.Category != "Resolved" && next.Category == "Resolved") Add(next.ChangedBy, next.ChangedAt, "resolved");
                if (prior.Category is "Resolved" or "Completed" && next.Category is "Proposed" or "InProgress") Add(next.ChangedBy, next.ChangedAt, "reopened");
            }
            if (bug.VerificationStatus == "Verified with evidence") Add(bug.RetestedBy, bug.VerifiedAt, "verified");
            foreach (var result in bug.RelatedResults.DistinctBy(e => e.Key).Where(e => e.Qualifies && e.BugIds.Contains(bug.BugId) &&
                revisions.Any(r => r.Category == "Resolved" && r.ChangedAt < e.CompletedAt)))
                Add(result.Performer, result.CompletedAt, "attempt");
        }
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        return actions.GroupBy(a => (a.Actor.Key, Day: TimeZoneInfo.ConvertTime(a.At, zone).ToString("yyyy-MM-dd")))
            .OrderBy(g => g.Key.Day).ThenBy(g => g.Key.Key)
            .Select(g => new BugActivity(g.Key.Key, g.First().Actor.DisplayName, g.Count(a => a.Kind == "raised"), g.Count(a => a.Kind == "resolved"),
                g.Count(a => a.Kind == "reopened"), g.Count(a => a.Kind == "verified"), g.Count(a => a.Kind == "attempt")) { Day = g.Key.Day }).ToArray();
    }
}
