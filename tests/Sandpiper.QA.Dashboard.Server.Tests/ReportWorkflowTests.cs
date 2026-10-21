using System.Net;
using System.Net.Http.Json;
using System.Text;
using Azure.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sandpiper.QA.Dashboard.Server.Models;
using Sandpiper.QA.Dashboard.Server.Services;
using Sandpiper.QA.Dashboard.Server.Services.Reporting;
using Xunit;

namespace Sandpiper.QA.Dashboard.Server.Tests;
public class ReportWorkflowTests
{
    private sealed class Credential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext context, CancellationToken ct) => new("fixture", DateTimeOffset.MaxValue);
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct) => ValueTask.FromResult(GetToken(context, ct));
    }
    private sealed class Loopback : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        { app.Use((context, following) => { context.Connection.RemoteIpAddress = IPAddress.Loopback; return following(context); }); next(app); };
    }
    private sealed class AdoFixture : HttpMessageHandler
    {
        public int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref Requests);
            var path = request.RequestUri!.AbsolutePath.Split("/_apis/")[1];
            var firstPage = request.RequestUri.Query.Contains("%24skip=0");
            var body = path switch
            {
                "testplan/Plans/7/suites" => """{"value":[{"id":10,"name":"Synthetic story suite","suiteType":"requirementTestSuite","requirementId":77}]}""",
                "testplan/Plans/7/Suites/10/TestPoint" => """{"value":[{"id":1,"testCaseReference":{"id":20,"name":"Synthetic case"},"results":{"outcome":"passed","lastTestRunId":2,"lastResultId":3}}]}""",
                "test/runs" => firstPage ? """{"value":[{"id":2,"plan":{"id":7},"revision":1,"state":"Completed"}]}""" : """{"value":[]}""",
                "test/Runs/2/results" => firstPage ? """{"value":[{"id":3,"testPoint":{"id":1},"state":"Completed","outcome":"Passed","completedDate":"2026-10-19T12:00:00Z","runBy":{"id":"actor","displayName":"Synthetic actor"},"associatedBugs":[]}]}""" : """{"value":[]}""",
                "wit/fields" => """{"value":[{"referenceName":"System.State"},{"referenceName":"System.CreatedBy"},{"referenceName":"System.CreatedDate"},{"referenceName":"System.WorkItemType"}]}""",
                "wit/workitemrelationtypes" => """{"value":[]}""",
                "wit/workitemtypes" => """{"value":[{"name":"Bug"}]}""",
                "wit/workitemtypes/Bug/states" => """{"value":[{"name":"Active","category":"InProgress"}]}""",
                "wit/workitemtypecategories" => """{"value":[{"referenceName":"Microsoft.RequirementCategory","workItemTypes":[{"name":"User Story"}]}]}""",
                "wit/workitems" => """{"value":[{"id":20,"fields":{"System.WorkItemType":"Test Case"},"relations":[]}]}""",
                _ => throw new InvalidOperationException(path)
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    [Theory]
    [InlineData("DSR", false)]
    [InlineData("TCR", false)]
    [InlineData("DSR", true)]
    [InlineData("TCR", true)]
    public async Task FrozenWorkflowSharesCalculationsExportsAndMarksReimportUntrusted(string kind, bool filtered)
    {
        var handler = new AdoFixture();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development"); builder.ConfigureServices(services => {
                services.RemoveAll<TokenCredential>(); services.AddSingleton<TokenCredential>(new Credential());
                services.AddSingleton<IStartupFilter, Loopback>();
                services.Configure<AzureDevOpsOptions>(o => o.SuiteCategories = [new(7, 10, "Targeted Regression")]);
                services.AddHttpClient<AzureDevOpsClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
            });
        });
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Sandpiper-Client", "dashboard");
        var response = await client.PostAsJsonAsync("/api/v2/reports", new ReportRequest(7, null, null,
            "2026-10-19T00:00:00Z", "2026-10-20T00:00:00Z", "2026-10-18T00:00:00Z", kind,
            new Dictionary<string, string> { ["Objectives"] = "Synthetic workflow test" }) { Category = filtered ? "Targeted Regression" : null, Outcome = filtered ? "passed" : null });
        response.EnsureSuccessStatusCode(); var report = (await response.Content.ReadFromJsonAsync<ReportSnapshot>())!;
        Assert.Equal(1, report.CurrentSnapshot.Metrics.Passed); Assert.Single(report.Activity.Events);
        Assert.Equal("actor", report.Activity.Productivity.Single().TesterKey); Assert.True(report.Quality!.Available);
        Assert.Equal(0, report.Quality.TotalLinkedDefects); Assert.Equal(0, report.Quality.OpenLinkedDefects);
        Assert.Equal(1, report.CurrentSnapshot.CaseMetrics!.TotalTestCases); Assert.Equal(1, report.CurrentSnapshot.CaseMetrics.ExecutedTestCases);
        Assert.Contains("Draft", report.Status); Assert.Equal(report.Checksum, ReportStore.Hash(report));
        Assert.Equal(filtered ? "Targeted Regression" : null, report.Scope!.Category);
        Assert.Equal(filtered ? "passed" : null, report.Scope.Outcome);
        Assert.StartsWith("Unavailable", report.Scope.HistoricalAsOfStatus);
        Assert.Equal(77, Assert.Single(report.CurrentSnapshot.Suites).RequirementId);
        if (filtered)
        {
            const string query = "category=Targeted%20Regression&outcome=passed";
            var selected = (await client.GetFromJsonAsync<PlanAnalytics>($"/api/ado/frontier/analytics/7?{query}"))!;
            Assert.Equal("Targeted Regression", selected.AppliedCategory); Assert.Equal(1, selected.Total);
            const string period = "from=2026-10-19T00:00:00Z&to=2026-10-20T00:00:00Z&baselineFrom=2026-10-18T00:00:00Z";
            var scopeEvidence = (await client.GetFromJsonAsync<WorkspaceEvidence>($"/api/v2/evidence/7?{period}&{query}"))!;
            Assert.Equal("passed", scopeEvidence.Snapshot.AppliedOutcome); Assert.True(scopeEvidence.Quality.Available);
            var activity = (await client.GetFromJsonAsync<ExecutionIntelligence>($"/api/v2/activity/7?{period}&{query}"))!;
            Assert.Single(activity.Events); Assert.Equal(1, activity.Reexecution!.Diagnostics!.QualifyingExecutions);
        }
        var capturedRequests = handler.Requests;
        var excel = await client.GetAsync($"/api/v2/reports/{report.Id}/xlsx"); excel.EnsureSuccessStatusCode();
        Assert.Equal(new byte[] { 80, 75 }, (await excel.Content.ReadAsByteArrayAsync())[..2]);
        var evidence = (await client.GetFromJsonAsync<ReportSnapshot>($"/api/v2/reports/{report.Id}/json"))!;
        Assert.Equal(report.Checksum, evidence.Checksum);
        Assert.Equal(capturedRequests, handler.Requests); // reopening and exporting do not fetch live ADO
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(ReportService.Tables(report), ReportStore.Json),
            System.Text.Json.JsonSerializer.Serialize(ReportService.Tables(evidence), ReportStore.Json));
        if (OperatingSystem.IsWindows())
        {
            var pdf = await client.PostAsJsonAsync("/api/v2/exports/table?format=pdf", new ReportTable("Selected", ["ID"], [["1"]]));
            pdf.EnsureSuccessStatusCode(); Assert.StartsWith("%PDF-", Encoding.ASCII.GetString((await pdf.Content.ReadAsByteArrayAsync())[..8]));
            if (kind == "DSR" && filtered)
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v2/exports/table?format=pdf",
                    new ReportTable("Selected", ["Value"], Enumerable.Range(0, 1001).Select(_ => new string?[] { new string('x', 500) }).ToArray()))).StatusCode);
        }
        var importedResponse = await client.PostAsJsonAsync("/api/v2/reports/import", evidence); importedResponse.EnsureSuccessStatusCode();
        var imported = (await importedResponse.Content.ReadFromJsonAsync<ReportSnapshot>())!;
        Assert.NotEqual(report.Id, imported.Id); Assert.Contains("untrusted", imported.Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v2/reports/import", evidence with { Status = "Tampered" })).StatusCode);
    }
}
