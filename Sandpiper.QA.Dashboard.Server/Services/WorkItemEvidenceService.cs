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
            string Category(string? raw) => new[] { "Proposed", "InProgress", "Resolved", "Completed", "Removed" }
                .FirstOrDefault(c => c.Equals(raw, StringComparison.OrdinalIgnoreCase)) ?? "Unknown";
            var categories = states.ToDictionary(s => AzureDevOpsClient.Text(s, "name") ?? "", s => Category(AzureDevOpsClient.Text(s, "category")));
            var workCategories = await ado.ListAsync("wit/workitemtypecategories", ct);
            var requirementTypes = workCategories.Where(c => AzureDevOpsClient.Text(c, "referenceName") == "Microsoft.RequirementCategory")
                .Where(c => c.TryGetProperty("workItemTypes", out var array) && array.ValueKind == JsonValueKind.Array)
                .SelectMany(c => c.GetProperty("workItemTypes").EnumerateArray()).Select(t => AzureDevOpsClient.Text(t, "name")).ToHashSet();
            if (requirementTypes.Count == 0) throw new InvalidDataException("Requirement category is unavailable in inspected process.");
            var pointIds = snapshot.Points.Select(p => p.Id).ToHashSet();
            var scopedEvents = events.Where(e => e.PointId != null && pointIds.Contains(e.PointId.Value)).ToArray();
            var historyReview = HistoryEvidenceEvaluator.Assess(snapshot.Points, events);
            var relationshipsComplete = historyReview.State == "Verified" && scopedEvents.All(e => e.BugLinksAvailable) &&
                !events.Any(e => e.PointId == null) && snapshot.Points.All(p => p.TestCaseId > 0);
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
            var supportedRelations = relationTypes.Select(r => AzureDevOpsClient.Text(r, "referenceName")).ToHashSet();
            bool ScopeRelation(string? name) => (name is "Microsoft.VSTS.Common.TestedBy-Reverse" or "System.LinkTypes.Related") && supportedRelations.Contains(name);
            relationshipsComplete &= caseIds.All(id => Relations(items[id]).All(r =>
                r.Relation is not ("Microsoft.VSTS.Common.TestedBy-Reverse" or "System.LinkTypes.Related") || supportedRelations.Contains(r.Relation)));
            var relatedIds = items.Where(pair => caseIds.Contains(pair.Key)).SelectMany(pair => Relations(pair.Value))
                .Where(r => ScopeRelation(r.Relation) && r.Id != null).Select(r => r.Id!.Value).Distinct();
            var requirements = await Items(relatedIds);
            var caseBugs = caseIds.ToDictionary(id => id, id => Relations(items[id]).Where(r => ScopeRelation(r.Relation) && r.Id != null &&
                requirements.TryGetValue(r.Id.Value, out var target) && Field(target, "System.WorkItemType") == typeName).Select(r => r.Id!.Value).Distinct().ToArray());
            bugIds = bugIds.Concat(caseBugs.Values.SelectMany(ids => ids)).Distinct().ToArray();
            foreach (var pair in requirements) items.TryAdd(pair.Key, pair.Value);
            relationshipsComplete &= caseIds.All(id => items[id].TryGetProperty("relations", out var relations) && relations.ValueKind == JsonValueKind.Array);
            relationshipsComplete &= caseIds.All(id => Relations(items[id]).Where(r => ScopeRelation(r.Relation)).All(r => r.Id > 0));
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
                    "Unknown", null, null, null, [], null) { LinkedCaseIds = caseBugs.Where(p => p.Value.Contains(id)).Select(p => p.Key).ToArray() };
                defects.Add(DefectVerificationEvaluator.Evaluate(bug, DateTimeOffset.UtcNow));
            }
            var activity = BugActivityCalculator.Calculate(defects, window, tester);
            var unknownStates = defects.Any(b => b.Category == "Unknown");
            var risk = new List<string>();
            if (snapshot.Metrics.Unknown > 0) risk.Add($"{snapshot.Metrics.Unknown} current points have unclassified outcomes.");
            if (snapshot.Metrics.Blocked > 0) risk.Add($"{snapshot.Metrics.Blocked} current points are blocked; blocker start times require historical evidence.");
            if (defects.Any(b => b.Category is not ("Completed" or "Removed"))) risk.Add("Open result-linked defects remain; severity/priority is shown without assuming a custom threshold.");
            risk.Add("Readiness indicators require explicitly approved configuration; human sign-off is required.");
            return new(true, "Live schema inspected; corporate relationship/chronology validation still required", DateTimeOffset.UtcNow,
                names.Order().ToArray(), categories, cases, defects, activity,
                unknownStates || !relationshipsComplete ? null : defects.Count(b => b.Category is not ("Completed" or "Removed")),
                !requirementRelationAvailable || snapshot.Points.Any(p => p.TestCaseId == null) ? null : cases.Count(c => c.RequirementIds.Count == 0),
                "Not Assessed — thresholds require approved configuration", risk, null,
                ["Defect scope is bugs explicitly linked to selected cases or their recorded results, not all project defects.",
                 "Later pass is only a candidate unless explicit bug link, resolution and same-performer completion chronology agree.",
                 "Bug state categories are inspected, not guessed from English labels. Corporate evidence conventions remain unvalidated."])
                { RelationshipsComplete = relationshipsComplete,
                  TotalLinkedDefects = relationshipsComplete ? defects.Select(b => b.BugId).Distinct().Count() : null,
                  CompletedLinkedDefects = relationshipsComplete && !unknownStates ? defects.Count(b => b.Category == "Completed") : null,
                  Evidence = new(relationshipsComplete ? "Verified" : "Incomplete", relationshipsComplete ? [] :
                    [..historyReview.Reasons, "Check result associatedBugs collections, point/case identities and case relation collections; missing evidence is not an empty list."]) };
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException)
        {
            return new(false, "Work-item schema/relationships unavailable or inconsistent; check WIT read permissions and process metadata.",
                DateTimeOffset.UtcNow, [], new Dictionary<string, string>(), [], [], [], null, null,
                "Not Assessed", ["Defect evidence unavailable; no zero or readiness inference."], null, ["Awaiting corporate schema/relationship validation."])
                { Evidence = new(ex is HttpRequestException { StatusCode: System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.Unauthorized }
                    ? "PermissionDenied" : "Unavailable", ["WIT schema or relationship retrieval failed; check permissions and process metadata."]) };
        }
    }

    private IEnumerable<(string? Relation, int? Id, string? Url)> Relations(JsonElement item)
    {
        if (!item.TryGetProperty("relations", out var relations) || relations.ValueKind != JsonValueKind.Array) yield break;
        foreach (var relation in relations.EnumerateArray())
        {
            var url = AzureDevOpsClient.Text(relation, "url");
            var valid = Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.UserInfo.Length == 0;
            int? id = valid ? ado.WorkItemId(url) : null;
            yield return (AzureDevOpsClient.Text(relation, "rel"), id, valid ? url : null);
        }
    }
}
