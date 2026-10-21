using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Microsoft.Extensions.Options;
using Sandpiper.QA.Dashboard.Server.Models;
using Sandpiper.QA.Dashboard.Server.Services;
using Sandpiper.QA.Dashboard.Server.Services.Analytics;
using Xunit;

namespace Sandpiper.QA.Dashboard.Server.Tests;
public class WorkItemEvidenceTests
{
    private sealed class Credential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext context, CancellationToken ct) => new("fixture", DateTimeOffset.MaxValue);
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct) => ValueTask.FromResult(GetToken(context, ct));
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(response(request)); }
    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK) { Content = new StringContent(value, Encoding.UTF8, "application/json") };
    private static WorkItemEvidenceService Service(Func<HttpRequestMessage, HttpResponseMessage> response)
    {
        var options = Options.Create(new AzureDevOpsOptions { Organization = "fixture", Project = "fixture" });
        return new(new AzureDevOpsClient(new HttpClient(new Handler(response)), new Credential(), options, new AdoRequestGate(options)));
    }
    [Fact]
    public async Task ForbiddenSchemaNeverBecomesZeroDefects()
    {
        var report = ExportTests.Report();
        var result = await Service(_ => new(HttpStatusCode.Forbidden)).LoadAsync(report.CurrentSnapshot, [], report.Activity.Window, null, default);
        Assert.False(result.Available); Assert.Null(result.OpenLinkedDefects);
        Assert.Null(result.TotalLinkedDefects); Assert.Equal("PermissionDenied", result.Evidence!.State);
    }
    [Fact]
    public async Task MetadataAndRevisionIdentityAreInspectedBeforeBugInterpretation()
    {
        var paths = new List<string>();
        var stateCategory = "InProgress";
        var service = Service(request =>
        {
            var path = request.RequestUri!.AbsolutePath.Split("/_apis/")[1]; paths.Add(path);
            return path switch
            {
                "wit/fields" => Json("""{"value":[{"referenceName":"System.State"},{"referenceName":"System.CreatedBy"},{"referenceName":"System.CreatedDate"},{"referenceName":"System.WorkItemType"}]}"""),
                "wit/workitemrelationtypes" => Json("""{"value":[{"referenceName":"Microsoft.VSTS.Common.TestedBy-Reverse"}]}"""),
                "wit/workitemtypes" => Json("""{"value":[{"name":"Bug"}]}"""),
                "wit/workitemtypes/Bug/states" => Json(JsonSerializer.Serialize(new { value = new[] { new { name = "Active", category = stateCategory } } })),
                "wit/workitemtypecategories" => Json("""{"value":[{"referenceName":"Microsoft.RequirementCategory","workItemTypes":[{"name":"User Story"}]}]}"""),
                "wit/workitems" => Json("""{"value":[{"id":20,"fields":{"System.WorkItemType":"Test Case"},"relations":[{"rel":"Microsoft.VSTS.Common.TestedBy-Reverse","url":"https://dev.azure.com/fixture/project/_apis/wit/workitems/10"}]},{"id":10,"fields":{"System.WorkItemType":"Bug","System.Title":"Fixture","System.State":"Active","System.CreatedDate":"2026-10-18T17:00:00Z","System.CreatedBy":{"id":"creator","displayName":"Creator"}}}]}"""),
                "wit/workitems/10/revisions" => request.RequestUri.Query.Contains("%24skip=0")
                    ? Json("""{"value":[{"id":10,"rev":1,"fields":{"System.State":"Active","System.ChangedDate":"2026-10-18T17:00:00Z"}}]}""") : Json("{\"value\":[]}"),
                _ => throw new InvalidOperationException(path)
            };
        });
        using var raw = JsonDocument.Parse("{\"id\":1,\"testCaseReference\":{\"id\":20},\"results\":{\"outcome\":\"failed\"}}");
        var point = TestPointNormalizer.Normalize(raw.RootElement, 1, 1, "Suite");
        var start = DateTimeOffset.Parse("2026-10-18T17:00:00Z");
        var snapshot = new PlanAnalytics(1, null, KpiCalculator.Calculate([point]), [], 0, start) { Points = [point] };
        var execution = new ExecutionEvent("1:1", 1, 1, 1, "failed", "Completed", start, null, [10], null, true) { BugLinksAvailable = true };
        var result = await service.LoadAsync(snapshot, [execution], new(start, start.AddDays(1)), null, default);
        Assert.True(result.Available); Assert.Equal(1, result.OpenLinkedDefects);
        Assert.Equal("creator", result.BugActivity.Single(a => a.Raised == 1).TesterKey);
        Assert.Contains("wit/workitemtypes/Bug/states", paths);
        Assert.Single(result.Defects.Single().Revisions);
        Assert.Equal(1, result.TotalLinkedDefects); Assert.Equal(0, result.CompletedLinkedDefects);
        Assert.Equal(new[] { 20 }, result.Defects.Single().LinkedCaseIds);
        var caseOnly = await service.LoadAsync(snapshot, [execution with { BugIds = [] }], new(start, start.AddDays(1)), null, default);
        Assert.Equal(1, caseOnly.TotalLinkedDefects); Assert.Empty(caseOnly.Defects.Single().RelatedResults);
        var duplicate = execution with { Key = "1:2", ResultId = 2 };
        Assert.Equal(1, (await service.LoadAsync(snapshot, [execution, duplicate], new(start, start.AddDays(1)), null, default)).TotalLinkedDefects);
        var sparse = await service.LoadAsync(snapshot, [execution with { BugLinksAvailable = false }], new(start, start.AddDays(1)), null, default);
        Assert.Null(sparse.TotalLinkedDefects); Assert.Null(sparse.OpenLinkedDefects); Assert.Equal("Incomplete", sparse.Evidence!.State);
        var empty = await service.LoadAsync(snapshot, [], new(start, start.AddDays(1)), null, default);
        Assert.Null(empty.TotalLinkedDefects); Assert.Null(empty.OpenLinkedDefects);
        stateCategory = "Unrecognized category";
        var unknownState = await service.LoadAsync(snapshot, [execution], new(start, start.AddDays(1)), null, default);
        Assert.Equal(1, unknownState.TotalLinkedDefects); Assert.Null(unknownState.OpenLinkedDefects); Assert.Null(unknownState.CompletedLinkedDefects);
    }

    [Fact]
    public async Task RelationTypesUseOrganizationScopeWhileProjectApisKeepProjectSegment()
    {
        var urls = new Dictionary<string, string>();
        var service = Service(request =>
        {
            var absolute = request.RequestUri!.AbsolutePath;
            var path = absolute.Split("/_apis/")[1];
            urls[path] = absolute;
            return path switch
            {
                "wit/fields" => Json("""{"value":[{"referenceName":"System.State"},{"referenceName":"System.CreatedBy"},{"referenceName":"System.CreatedDate"},{"referenceName":"System.WorkItemType"}]}"""),
                "wit/workitemrelationtypes" => Json("""{"value":[{"referenceName":"Microsoft.VSTS.Common.TestedBy-Reverse"}]}"""),
                "wit/workitemtypes" => Json("""{"value":[{"name":"Bug"}]}"""),
                "wit/workitemtypes/Bug/states" => Json("""{"value":[{"name":"Active","category":"InProgress"}]}"""),
                "wit/workitemtypecategories" => Json("""{"value":[{"referenceName":"Microsoft.RequirementCategory","workItemTypes":[{"name":"User Story"}]}]}"""),
                "wit/workitems" => Json("{\"value\":[]}"),
                _ => Json("{\"value\":[]}")
            };
        });
        using var raw = JsonDocument.Parse("{\"id\":1,\"results\":{\"outcome\":\"failed\"}}");
        var point = TestPointNormalizer.Normalize(raw.RootElement, 1, 1, "Suite") with { TestCaseId = 20 };
        var start = DateTimeOffset.Parse("2026-10-18T17:00:00Z");
        var snapshot = new PlanAnalytics(1, null, KpiCalculator.Calculate([point]), [], 0, start) { Points = [point] };
        await service.LoadAsync(snapshot, [], new(start, start.AddDays(1)), null, default);
        // Organization-scoped: no project segment before /_apis/.
        Assert.Equal("/fixture/_apis/wit/workitemrelationtypes", urls["wit/workitemrelationtypes"]);
        // Project-scoped calls retain the project segment.
        Assert.Equal("/fixture/fixture/_apis/wit/fields", urls["wit/fields"]);
    }
}
