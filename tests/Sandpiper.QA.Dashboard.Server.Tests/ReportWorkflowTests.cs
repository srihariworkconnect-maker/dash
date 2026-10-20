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
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath.Split("/_apis/")[1];
            var firstPage = request.RequestUri.Query.Contains("%24skip=0");
            var body = path switch
            {
                "testplan/Plans/7/suites" => """{"value":[{"id":10,"name":"Synthetic suite"}]}""",
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
    [InlineData("DSR")]
    [InlineData("TCR")]
    public async Task FrozenWorkflowSharesCalculationsExportsAndMarksReimportUntrusted(string kind)
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development"); builder.ConfigureServices(services => {
                services.RemoveAll<TokenCredential>(); services.AddSingleton<TokenCredential>(new Credential());
                services.AddSingleton<IStartupFilter, Loopback>();
                services.AddHttpClient<AzureDevOpsClient>().ConfigurePrimaryHttpMessageHandler(() => new AdoFixture());
            });
        });
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Sandpiper-Client", "dashboard");
        var response = await client.PostAsJsonAsync("/api/v2/reports", new ReportRequest(7, null, null,
            "2026-10-19T00:00:00Z", "2026-10-20T00:00:00Z", "2026-10-18T00:00:00Z", kind,
            new Dictionary<string, string> { ["Objectives"] = "Synthetic workflow test" }));
        response.EnsureSuccessStatusCode(); var report = (await response.Content.ReadFromJsonAsync<ReportSnapshot>())!;
        Assert.Equal(1, report.CurrentSnapshot.Metrics.Passed); Assert.Single(report.Activity.Events);
        Assert.Equal("actor", report.Activity.Productivity.Single().TesterKey); Assert.True(report.Quality!.Available);
        Assert.Equal(0, report.Quality.TotalLinkedDefects); Assert.Equal(0, report.Quality.OpenLinkedDefects);
        Assert.Equal(1, report.CurrentSnapshot.CaseMetrics!.TotalTestCases); Assert.Equal(1, report.CurrentSnapshot.CaseMetrics.ExecutedTestCases);
        Assert.Contains("Draft", report.Status); Assert.Equal(report.Checksum, ReportStore.Hash(report));
        var excel = await client.GetAsync($"/api/v2/reports/{report.Id}/xlsx"); excel.EnsureSuccessStatusCode();
        Assert.Equal(new byte[] { 80, 75 }, (await excel.Content.ReadAsByteArrayAsync())[..2]);
        var evidence = (await client.GetFromJsonAsync<ReportSnapshot>($"/api/v2/reports/{report.Id}/json"))!;
        Assert.Equal(report.Checksum, evidence.Checksum);
        var importedResponse = await client.PostAsJsonAsync("/api/v2/reports/import", evidence); importedResponse.EnsureSuccessStatusCode();
        var imported = (await importedResponse.Content.ReadFromJsonAsync<ReportSnapshot>())!;
        Assert.NotEqual(report.Id, imported.Id); Assert.Contains("untrusted", imported.Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v2/reports/import", evidence with { Status = "Tampered" })).StatusCode);
    }
}
