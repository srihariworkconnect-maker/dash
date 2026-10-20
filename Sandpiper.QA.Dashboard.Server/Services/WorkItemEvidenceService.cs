using System.Text.Json;
using Sandpiper.QA.Dashboard.Server.Models;
using Sandpiper.QA.Dashboard.Server.Services.Analytics;

namespace Sandpiper.QA.Dashboard.Server.Services;

public sealed class WorkItemEvidenceService(AzureDevOpsClient ado)
{
    private static string? Field(JsonElement item, string key) => item.TryGetProperty("fields", out var fields) && fields.TryGetProperty(key, out var value)
        ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.ValueKind == JsonValueKind.Number ? value.ToString() : null : null;
    private static DateTimeOffset? Date(JsonElement item, string key) => DateTimeOffset.TryParse(Field(item, key),
        System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var date) ? date.ToUniversalTime() : null;
    private static SourceIdentity? Identity(JsonElement item, string key) => item.TryGetProperty("fields", out var fields) ? TestPointNormalizer.Identity(fields, key) : null;

    public async Task<QualityEvidence> LoadAsync(PlanAnalytics snapshot, IReadOnlyList<ExecutionEvent> events,
        ReportingWindow window, string? tester, CancellationToken ct)
    {
        try
        {
            // Inspect this environment's metadata BEFORE interpreting work-item states/fields.
            var fields = await ado.ListAsync("wit/fields", ct);
            var names = fields.Select(f => AzureDevOpsClient.Text(f, "referenceName")).Where(n => n != null).Cast<string>().ToHashSet();
            if (!new[] { "System.State", "System.CreatedBy", "System.CreatedDate", "System.WorkItemType" }.All(names.Contains))
                throw new InvalidDataException("Required work-item fields are absent from inspected schema.");
            var relationTypes = await ado.ListAsync("wit/workitemrelationtypes", ct);
            var requirementRelationAvailable = relationTypes.Any(r => AzureDevOpsClient.Text(r, "referenceName") == "Microsoft.VSTS.Common.TestedBy-Reverse");
            var types = await ado.ListAsync("wit/workitemtypes", ct);
            var bugType = types.FirstOrDefault(t => AzureDevOpsClient.Text(t, "name")?.Equals("Bug", StringComparison.OrdinalIgnoreCase) == true);
            if (bugType.ValueKind == JsonValueKind.Undefined) throw new InvalidDataException("Bug type not found in inspected process.");
            var typeName = AzureDevOpsClient.Text(bugType, "name")!;
            var states = await ado.ListAsync($"wit/workitemtypes/{Uri.EscapeDataString(typeName)}/states", ct);
            var categories = states.ToDictionary(s => AzureDevOpsClient.Text(s, "name") ?? "", s => AzureDevOpsClient.Text(s, "category") ?? "Unknown");
            var workCategories = await ado.ListAsync("wit/workitemtypecategories", ct);
            var requirementTypes = workCategories.Where(c => AzureDevOpsClient.Text(c, "referenceName") == "Microsoft.RequirementCategory")
                .Where(c => c.TryGetProperty("workItemTypes", out var array) && array.ValueKind == JsonValueKind.Array)
                .SelectMany(c => c.GetProperty("workItemTypes").EnumerateArray()).Select(t => AzureDevOpsClient.Text(t, "name")).ToHashSet();
            if (requirementTypes.Count == 0) throw new InvalidDataException("Requirement category is unavailable in inspected process.");
            var pointIds = snapshot.Points.Select(p => p.Id).ToHashSet();
            var scopedEvents = events.Where(e => e.PointId != null && pointIds.Contains(e.PointId.Value)).ToArray();
            var relationshipsComplete = scopedEvents.All(e => e.BugLinksAvailable) && !events.Any(e => e.PointId == null && e.BugIds.Count > 0);
            var bugIds = scopedEvents.SelectMany(e => e.BugIds).Distinct().ToArray();
            var caseIds = snapshot.Points.Where(p => p.TestCaseId != null).Select(p => p.TestCaseId!.Value).Distinct().ToArray();
            async Task<Dictionary<int, JsonElement>> Items(IEnumerable<int> ids)
            {
                Dictionary<int, JsonElement> result = [];
                foreach (var batch in ids.Distinct().Chunk(200))
                {
                    var raw = await ado.ListAsync("wit/workitems", ct, new() { ["ids"] = string.Join(",", batch), ["$expand"] = "Relations" });
                    foreach (var item in raw) result[AzureDevOpsClient.Number(item, "id")] = item;
                    if (batch.Any(id => !result.ContainsKey(id))) throw new InvalidDataException("Some work items are inaccessible.");
                }
                return result;
            }
            var items = await Items(caseIds.Concat(bugIds));
            var relatedIds = items.Where(pair => caseIds.Contains(pair.Key)).SelectMany(pair => Relations(pair.Value))
                .Where(r => r.Relation == "Microsoft.VSTS.Common.TestedBy-Reverse" && r.Id != null).Select(r => r.Id!.Value).Distinct();
            var requirements = await Items(relatedIds);
            var cases = caseIds.Select(id => new CaseEnrichment(id, Field(items[id], "Microsoft.VSTS.Common.Priority"),
                Relations(items[id]).Where(r => r.Relation == "Microsoft.VSTS.Common.TestedBy-Reverse" && r.Id != null &&
                    requirements.TryGetValue(r.Id.Value, out var target) && requirementTypes.Contains(Field(target, "System.WorkItemType"))).Select(r => r.Id!.Value).Distinct().ToArray(),
                Relations(items[id]).Where(r => r.Relation == "AttachedFile" && r.Url != null).Select(r => r.Url!).ToArray())).ToArray();
            var defects = new List<DefectEvidence>();
            foreach (var id in bugIds)
            {
                if (Field(items[id], "System.WorkItemType") != typeName) throw new InvalidDataException("Result bug relationship targets a different work-item type.");
                var rawRevisions = await ado.HistoryAsync($"wit/workitems/{id}/revisions", [], ct, "rev");
                var revisions = rawRevisions.Select(r => new WorkItemRevision(AzureDevOpsClient.Number(r, "rev"), Field(r, "System.State"),
                    categories.GetValueOrDefault(Field(r, "System.State") ?? "", "Unknown"), Date(r, "System.ChangedDate"), Identity(r, "System.ChangedBy"))).ToArray();
                var state = Field(items[id], "System.State");
                var bug = new DefectEvidence(id, Field(items[id], "System.Title"), Identity(items[id], "System.CreatedBy"), Date(items[id], "System.CreatedDate"),
                    Identity(items[id], "System.AssignedTo"), state, categories.GetValueOrDefault(state ?? "", "Unknown"),
                    names.Contains("Microsoft.VSTS.Common.Severity") ? Field(items[id], "Microsoft.VSTS.Common.Severity") : null,
                    names.Contains("Microsoft.VSTS.Common.Priority") ? Field(items[id], "Microsoft.VSTS.Common.Priority") : null,
                    revisions, scopedEvents.Where(e => e.BugIds.Contains(id) || e.PointId != null && scopedEvents.Any(f => f.PointId == e.PointId && f.BugIds.Contains(id))).ToArray(),
                    "Unknown", null, null, null, [], null);
                defects.Add(DefectVerificationEvaluator.Evaluate(bug, DateTimeOffset.UtcNow));
            }
            var actors = defects.SelectMany(b => new[] { b.CreatedBy, b.RetestedBy }.Concat(b.Revisions.Select(r => r.ChangedBy)))
                .Select(i => i ?? new SourceIdentity("unattributed", "Unattributed")).DistinctBy(i => i.Key).Where(i => string.IsNullOrEmpty(tester) || tester == i.Key);
            bool InWindow(DateTimeOffset? date) => date >= window.From && date < window.To;
            var activity = actors.Select(actor => new BugActivity(actor.Key, actor.DisplayName,
                defects.Count(b => (b.CreatedBy?.Key ?? "unattributed") == actor.Key && InWindow(b.CreatedAt)),
                defects.Sum(b => b.Revisions.Zip(b.Revisions.Skip(1)).Count(p => p.First.Category != "Resolved" && p.Second.Category == "Resolved" && (p.Second.ChangedBy?.Key ?? "unattributed") == actor.Key && InWindow(p.Second.ChangedAt))),
                defects.Sum(b => b.Revisions.Zip(b.Revisions.Skip(1)).Count(p => p.First.Category is "Resolved" or "Completed" && p.Second.Category is "Proposed" or "InProgress" && p.Second.ChangedBy?.Key == actor.Key && InWindow(p.Second.ChangedAt))),
                defects.Count(b => b.VerificationStatus == "Verified with evidence" && (b.RetestedBy?.Key ?? "unattributed") == actor.Key && InWindow(b.VerifiedAt)),
                defects.Sum(b => b.RelatedResults.Count(e => e.Qualifies && e.BugIds.Contains(b.BugId) && (e.Performer?.Key ?? "unattributed") == actor.Key && InWindow(e.CompletedAt) && b.Revisions.Any(r => r.Category == "Resolved" && r.ChangedAt < e.CompletedAt))))).ToArray();
            var unknownStates = defects.Any(b => b.Category == "Unknown");
            var risk = new List<string>();
            if (snapshot.Metrics.Unknown > 0) risk.Add($"{snapshot.Metrics.Unknown} current points have unclassified outcomes.");
            if (snapshot.Metrics.Blocked > 0) risk.Add($"{snapshot.Metrics.Blocked} current points are blocked; blocker start times require historical evidence.");
            if (defects.Any(b => b.Category is not ("Completed" or "Removed"))) risk.Add("Open result-linked defects remain; severity/priority is shown without assuming a custom threshold.");
            risk.Add("Readiness rules are not configured/approved; human sign-off is required.");
            return new(true, "Live schema inspected; corporate relationship/chronology validation still required", DateTimeOffset.UtcNow,
                names.Order().ToArray(), categories, cases, defects, activity,
                unknownStates || !relationshipsComplete ? null : defects.Count(b => b.Category is not ("Completed" or "Removed")),
                !requirementRelationAvailable || snapshot.Points.Any(p => p.TestCaseId == null) ? null : cases.Count(c => c.RequirementIds.Count == 0),
                "Not assessed — thresholds require approved configuration", risk, null,
                ["Defect scope is result-linked bugs for selected current test points, not all project defects.",
                 "Later pass is only a candidate unless explicit bug link, resolution and same-performer completion chronology agree.",
                 "Bug state categories are inspected, not guessed from English labels. Corporate evidence conventions remain unvalidated."])
                { RelationshipsComplete = relationshipsComplete };
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException)
        {
            return new(false, "Work-item schema/relationships unavailable or inconsistent; check WIT read permissions and process metadata.",
                DateTimeOffset.UtcNow, [], new Dictionary<string, string>(), [], [], [], null, null,
                "Not assessed", ["Defect evidence unavailable; no zero or readiness inference."], null, ["Awaiting corporate schema/relationship validation."]);
        }
    }

    private static IEnumerable<(string? Relation, int? Id, string? Url)> Relations(JsonElement item)
    {
        if (!item.TryGetProperty("relations", out var relations) || relations.ValueKind != JsonValueKind.Array) yield break;
        foreach (var relation in relations.EnumerateArray())
        {
            var url = AzureDevOpsClient.Text(relation, "url");
            var valid = Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https";
            int? id = valid && int.TryParse(uri!.Segments.Last().Trim('/'), out var n) && n > 0 ? n : null;
            yield return (AzureDevOpsClient.Text(relation, "rel"), id, valid ? url : null);
        }
    }
}
