using System.Text.Json;
using Sandpiper.QA.Dashboard.Server.Models;

namespace Sandpiper.QA.Dashboard.Server.Services.Analytics;

public static class TestPointNormalizer
{
    public static DateTimeOffset? ExecutionDate(string? value) =>
        DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal, out var date) && date.Year > 1
            ? date.ToUniversalTime() : null;

    public static SourceIdentity? Identity(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var item) || item.ValueKind != JsonValueKind.Object) return null;
        var key = AzureDevOpsClient.Text(item, "id") ?? AzureDevOpsClient.Text(item, "descriptor");
        var display = AzureDevOpsClient.Text(item, "displayName");
        // Names are not stable attribution keys. Unidentified activity stays unattributed.
        return string.IsNullOrWhiteSpace(key) ? null : new(key, display ?? "Unnamed identity");
    }

    public static NormalizedPoint Normalize(JsonElement point, int planId, int suiteId, string path)
    {
        var id = AzureDevOpsClient.Number(point, "id");
        if (id <= 0) throw new InvalidDataException("Missing test-point identity.");
        var result = point.TryGetProperty("results", out var r) && r.ValueKind == JsonValueKind.Object ? r : default;
        var hasOutcome = result.ValueKind == JsonValueKind.Object && result.TryGetProperty("outcome", out _);
        var outcome = hasOutcome ? result.GetProperty("outcome") : default;
        var type = hasOutcome ? outcome.ValueKind.ToString() : "Missing";
        var raw = hasOutcome ? outcome.GetRawText() : null;
        var value = outcome.ValueKind == JsonValueKind.String ? outcome.GetString() : null;
        var tc = point.TryGetProperty("testCaseReference", out var reference) && reference.ValueKind == JsonValueKind.Object ? reference : default;
        var config = point.TryGetProperty("configuration", out var c) && c.ValueKind == JsonValueKind.Object ? c : default;
        var detail = result.ValueKind == JsonValueKind.Object && result.TryGetProperty("lastResultDetails", out var d) && d.ValueKind == JsonValueKind.Object ? d : default;
        static int? Id(JsonElement item, string key) => item.ValueKind == JsonValueKind.Object && AzureDevOpsClient.Number(item, key) is > 0 and var n ? n : null;
        static string? Text(JsonElement item, string key) => item.ValueKind == JsonValueKind.Object ? AzureDevOpsClient.Text(item, key) : null;
        return new(id, value, planId, Id(tc, "id"), Text(tc, "name"), Id(config, "id"), Text(config, "name"),
            tc.ValueKind == JsonValueKind.Object ? Identity(tc, "assignedTo") : null,
            detail.ValueKind == JsonValueKind.Object ? Identity(detail, "runBy") : null,
            Id(result, "lastTestRunId"), Id(result, "lastResultId"),
            ExecutionDate(Text(detail, "dateCompleted")),
            type, raw, Text(result, "state"),
            point.TryGetProperty("isActive", out var active) && active.ValueKind is JsonValueKind.True or JsonValueKind.False ? active.GetBoolean() : null,
            [new(suiteId, path)]);
    }

    public static ReconciliationStatus Reconcile(IReadOnlyList<NormalizedPoint> points)
    {
        var unspecified = points.Count(p => KpiCalculator.Normalize(p.Outcome) == "unspecified");
        var unsupported = points.Count(p => p.OutcomeJsonType is not ("String" or "Null" or "Missing"));
        var missing = points.Count(p => p.OutcomeJsonType is "Missing" or "Null");
        return new(true, "Awaiting VDI / ADO Progress Report reconciliation", unspecified, missing, unsupported,
            points.Count(p => p.TestCaseId == null), points.GroupBy(p => p.OutcomeJsonType).ToDictionary(g => g.Key, g => g.Count()),
            ["Collection completeness is not corporate validation.",
             "Un-run points (none/active/notExecuted, or unspecified/absent outcome with no completed result) count as Pending; malformed outcome payloads and points carrying completed-result evidence without a terminal outcome remain Unknown.",
             "Current-state scope is not a historical cutoff snapshot. No authoritative DSR/TCR without preserved evidence."]);
    }

    public static NormalizedPoint[] Merge(IEnumerable<NormalizedPoint> source) => source.GroupBy(p => p.Id).Select(group =>
    {
        var first = group.First();
        if (group.Any(p => p.PlanId != first.PlanId || p.TestCaseId != first.TestCaseId || p.ConfigurationId != first.ConfigurationId ||
            p.AssignedTester?.Key != first.AssignedTester?.Key || p.LatestRunId != first.LatestRunId || p.LatestResultId != first.LatestResultId ||
            p.LatestExecutedBy?.Key != first.LatestExecutedBy?.Key || p.LatestExecutionDate != first.LatestExecutionDate ||
            KpiCalculator.Normalize(p.Outcome) != KpiCalculator.Normalize(first.Outcome) || p.OutcomeJsonType != first.OutcomeJsonType))
            throw new InvalidDataException("Point identity or latest execution changed during collection; refresh.");
        return first with { Memberships = group.SelectMany(p => p.Memberships).Distinct().ToArray() };
    }).ToArray();
}
