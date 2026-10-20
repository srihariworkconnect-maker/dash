using System.Security.Cryptography;
using System.Text.Json;
using Sandpiper.QA.Dashboard.Server.Models;
using Sandpiper.QA.Dashboard.Server.Services.Analytics;

namespace Sandpiper.QA.Dashboard.Server.Services.Reporting;

public sealed class ReportStore
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<Guid, (DateTimeOffset StoredAt, byte[] Bytes)> reports = [];
    private readonly object gate = new();
    public ReportSnapshot Put(ReportSnapshot report)
    {
        lock (gate)
        {
            foreach (var id in reports.Where(p => p.Value.StoredAt < DateTimeOffset.UtcNow.AddMinutes(-60)).Select(p => p.Key).ToArray()) reports.Remove(id);
            if (reports.Count >= 25) reports.Remove(reports.MinBy(p => p.Value.StoredAt).Key);
            reports[report.Id] = (DateTimeOffset.UtcNow, JsonSerializer.SerializeToUtf8Bytes(report, Json));
        }
        return report;
    }
    public ReportSnapshot? Get(Guid id)
    {
        lock (gate) return reports.TryGetValue(id, out var value) && value.StoredAt >= DateTimeOffset.UtcNow.AddMinutes(-60)
            ? JsonSerializer.Deserialize<ReportSnapshot>(value.Bytes, Json) : null;
    }
    public object List()
    {
        lock (gate) return reports.Values.Where(v => v.StoredAt >= DateTimeOffset.UtcNow.AddMinutes(-60)).Select(v =>
        {
            var r = JsonSerializer.Deserialize<ReportSnapshot>(v.Bytes, Json)!;
            return new { r.Id, r.Kind, r.CreatedAt, r.Status, planId = r.CurrentSnapshot.PlanId, r.Checksum };
        }).ToArray();
    }
    public static string Hash(ReportSnapshot report) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(report with { Checksum = "" }, Json)));
}

public sealed class ReportService(FrontierAnalyticsService analytics, WorkspaceEvidenceService evidence, ReportStore store)
{
    public async Task<ReportSnapshot> CreateAsync(ReportRequest request, CancellationToken ct)
    {
        if (request.Kind is not ("DSR" or "TCR")) throw new ArgumentException("Choose DSR or TCR.");
        var window = ReportingScope.Window(request.From, request.To);
        var baseline = ReportingScope.Instant(request.BaselineFrom);
        var bundle = await evidence.LoadAsync(request.PlanId, request.SuiteId, window, baseline, request.Tester, ct);
        var current = string.IsNullOrEmpty(request.Tester) ? bundle.Snapshot : await analytics.GetAnalyticsAsync(request.PlanId, ct, request.SuiteId, request.Tester);
        var activity = bundle.Activity;
        var narrative = (request.Narrative ?? []).Where(p => p.Key.Length <= 60).ToDictionary(p => p.Key, p => p.Value.Length <= 4000 ? p.Value : p.Value[..4000]);
        var report = new ReportSnapshot(Guid.NewGuid(), request.Kind, DateTimeOffset.UtcNow, "v2-2026-10-20.1",
            "Draft — awaiting corporate reconciliation and historical cutoff evidence", "Local ADO collection", "", current, activity, narrative,
            ["Current point snapshot is at collection time, NOT a reconstructed historical cutoff snapshot.",
             "Frozen export can reproduce this draft; authoritative historical DSR requires approved preserved data.",
             "Evidence bundle checksum detects changes; it is not a digital signature or corporate approval.",
             "Reports are held in memory for 60 minutes / at most 25 reports. Export the evidence bundle to approved storage.",
             "No report is emailed, release signed off, or infrastructure provisioned automatically.",
             "Defect, coverage and verification evidence may be unavailable and is not reported as zero."])
            { Quality = bundle.Quality };
        return store.Put(report with { Checksum = ReportStore.Hash(report) });
    }

    public ReportSnapshot Import(ReportSnapshot report)
    {
        if (report.Kind is not ("DSR" or "TCR") || report.CurrentSnapshot?.Metrics == null || report.CurrentSnapshot.Points == null ||
            report.CurrentSnapshot.Suites == null || report.Activity?.Window == null || report.Activity.Baseline == null ||
            report.Activity.Events == null || report.Activity.Points == null || report.Activity.Productivity == null || report.Activity.Daily == null ||
            report.Narrative == null || report.Limitations == null || report.CurrentSnapshot.Points.Count > 50000 || report.Activity.Events.Count > 100000)
            throw new ArgumentException("Incomplete or oversized evidence structure.");
        if (report.CalculationVersion != "v2-2026-10-20.1" || report.Checksum != ReportStore.Hash(report))
            throw new ArgumentException("Evidence checksum or schema version is invalid.");
        // Imports never establish trusted source/validation, even if the original claimed it.
        var copy = report with { Id = Guid.NewGuid(), Status = "Draft — imported evidence, untrusted source",
            Origin = "User-imported evidence; not authenticated ADO provenance", Checksum = "" };
        return store.Put(copy with { Checksum = ReportStore.Hash(copy) });
    }

    public static IReadOnlyList<ReportTable> Tables(ReportSnapshot r)
    {
        static string S(object? v) => v?.ToString() ?? "Unavailable";
        var m = r.CurrentSnapshot.Metrics;
        return [
            new("Metadata", ["Field", "Value"], [
                ["Report", r.Kind], ["Status", r.Status], ["ID", r.Id.ToString()], ["Scope", $"Plan {r.CurrentSnapshot.PlanId}; suite {r.CurrentSnapshot.SuiteId?.ToString() ?? "all"}"],
                ["Created UTC", r.CreatedAt.ToString("O")], ["Current source UTC", r.CurrentSnapshot.RetrievedAt.ToString("O")],
                ["Period from", r.Activity.Window.From.ToString("O")], ["Period to exclusive", r.Activity.Window.To.ToString("O")],
                ["Baseline", r.Activity.Baseline.From.ToString("O")], ["Timezone", "Europe/London"], ["Rules", r.CalculationVersion], ["Checksum", r.Checksum],
                ..r.Narrative.Select(p => new string?[] { p.Key, p.Value }), ..r.Limitations.Select(text => new string?[] { "Limitation", text })]),
            new("Execution snapshot", ["Category", "Count"], [..m.Statuses.Select(p => new string?[] { p.Key, S(p.Value) }),
                ["Total unique points", S(m.Total)], ["Primary pass %", S(m.PrimaryPassRate)], ["Secondary pass %", S(m.SecondaryPassRate)],
                ["Progress %", S(m.ExecutionRate)], ["Re-executed baseline points", S(r.Activity.ReExecutedPoints)], ["Period repeat events", S(r.Activity.RepeatEvents)]]),
            new("Live TCM", ["Point ID", "Case ID", "Title", "Suite path", "Configuration", "Assigned", "Latest performer", "Latest date UTC", "Latest outcome", "Baseline executions", "Baseline repeats", "Plan ID", "Bug IDs", "Bug state", "Bug severity / priority", "Test priority", "Requirements", "Evidence"],
                r.CurrentSnapshot.Points.Select(p => { var count = r.Activity.Points.SingleOrDefault(a => a.PointId == p.Id);
                    var testCase = r.Quality?.Cases.FirstOrDefault(c => c.CaseId == p.TestCaseId);
                    var defects = r.Quality?.Defects.Where(b => b.RelatedResults.Any(e => e.PointId == p.Id && e.BugIds.Contains(b.BugId))).ToArray() ?? [];
                    return new string?[] {
                    S(p.Id), S(p.TestCaseId), p.Title, string.Join("; ", p.Memberships.Select(s => s.SuitePath)), p.ConfigurationName,
                    p.AssignedTester?.DisplayName, p.LatestExecutedBy?.DisplayName, p.LatestExecutionDate?.ToString("O"), p.Outcome,
                    r.Activity.Complete ? S(count?.ExecutionCount ?? 0) : "Incomplete", r.Activity.Complete ? S(count?.RepeatCount ?? 0) : "Incomplete",
                    S(p.PlanId), r.Quality?.RelationshipsComplete == true ? string.Join(", ", defects.Select(b => b.BugId)) : "Unavailable",
                    string.Join("; ", defects.Select(b => $"{b.BugId}: {b.State}")), string.Join("; ", defects.Select(b => $"{b.BugId}: {b.Severity} / {b.Priority}")),
                    testCase?.Priority, testCase == null ? null : string.Join(", ", testCase.RequirementIds), testCase == null ? null : string.Join("; ", testCase.EvidenceLinks) }; }).ToList()),
            new("Period activity", ["Run:Result", "Point", "Outcome", "Completed UTC", "Actual performer", "Repeat", "Transition", "Result bug IDs"],
                r.Activity.Events.Select(e => new string?[] { e.Key, S(e.PointId), e.Outcome, e.CompletedAt?.ToString("O"), e.Performer?.DisplayName ?? "Unattributed", S(e.IsRepeat), e.Transition, string.Join(",", e.BugIds) }).ToList()),
            new("Productivity", ["Actual performer", "UK day", "Assigned now", "Pending assigned now", "Events", "Unique points", "Passed", "Failed", "Blocked", "Repeat events", "Repeated points"],
                r.Activity.Productivity.Select(p => new string?[] { p.Tester, p.Day, S(p.AssignedPoints), S(p.PendingAssigned), S(p.ExecutionEvents), S(p.UniquePoints), S(p.Passed), S(p.Failed), S(p.Blocked), S(p.RepeatEvents), S(p.ReExecutedPoints) }).ToList()),
            new("Daily trend", ["UK day", "Execution events", "Unique points", "Repeat events", "Passed", "Failed", "Blocked"],
                r.Activity.Daily.Select(d => new string?[] { d.Day, S(d.ExecutionEvents), S(d.UniquePoints), S(d.RepeatEvents), S(d.Passed), S(d.Failed), S(d.Blocked) }).ToList()),
            new("Defects and risks", ["Section", "Evidence"], [["Schema", r.Quality?.SchemaStatus ?? "Unavailable"],
                ["Open linked defects", S(r.Quality?.OpenLinkedDefects)], ["Requirement gaps", S(r.Quality?.RequirementGaps)],
                ["Readiness", r.Quality?.ReleaseReadiness ?? "Not assessed"],
                ["Historical cutoff snapshot", "Unavailable — current collection state shown separately"], ["Sign-off", "Human decision required; no automatic release approval"],
                ..(r.Quality?.RiskReasons ?? []).Select(reason => new string?[] { "Risk", reason })]),
            new("Defect queue", ["Bug ID", "Title", "Created By", "Created UTC", "Assigned", "State", "Severity", "Priority", "Verification", "Retested By", "Retested UTC", "Outcome", "Evidence"],
                r.Quality?.Defects.Select(b => new string?[] { S(b.BugId), b.Title, b.CreatedBy?.DisplayName, b.CreatedAt?.ToString("O"), b.AssignedTo?.DisplayName,
                    b.State, b.Severity, b.Priority, b.VerificationStatus, b.RetestedBy?.DisplayName, b.RetestedAt?.ToString("O"), b.VerificationOutcome,
                    string.Join("; ", b.VerificationEvidence) }).ToList() ?? []),
            new("Bug activity", ["Created By / actor", "UK day", "Raised", "Resolved", "Reopened", "Verified with evidence", "Verification attempts"],
                r.Quality?.BugActivity.Select(b => new string?[] { b.Tester, b.Day, S(b.Raised), S(b.Resolved), S(b.Reopened), S(b.VerifiedWithEvidence), S(b.VerificationAttempts) }).ToList() ?? []),
            new("Blocker aging", ["Point ID", "Observed since UTC", "Observed age days", "Evidence status"],
                r.Quality?.Blockers?.Select(b => new string?[] { S(b.PointId), b.ObservedSince?.ToString("O"), S(b.ObservedAgeDays), b.EvidenceStatus }).ToList() ?? [])
        ];
    }
}
