using System.Net;
using System.Text;
using Azure.Core;
using Microsoft.Extensions.Options;
using Sandpiper.QA.Dashboard.Server.Services;
using Xunit;

namespace Sandpiper.QA.Dashboard.Server.Tests;

public class AdoIntegrationTests
{
    private sealed class Credential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext context, CancellationToken ct) => new("test-fixture", DateTimeOffset.MaxValue);
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct) => ValueTask.FromResult(GetToken(context, ct));
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(respond(request));
    }
    private static HttpResponseMessage Json(string value, string? token = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(value, Encoding.UTF8, "application/json") };
        if (token != null) response.Headers.Add("x-ms-continuationtoken", token);
        return response;
    }
    private static AzureDevOpsClient Client(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var options = Options.Create(new AzureDevOpsOptions { Organization = "fixture", Project = "fixture" });
        return new(new HttpClient(new Handler(handler)), new Credential(), options, new AdoRequestGate(options));
    }

    [Fact]
    public async Task ContinuationIsEncodedInQueryAndNotARequestHeader()
    {
        var calls = 0;
        var client = Client(request =>
        {
            calls++;
            Assert.False(request.Headers.Contains("x-ms-continuationtoken"));
            if (calls == 1) return Json("{\"value\":[{\"id\":1}]}", "a+b/=");
            Assert.Contains("continuationToken=a%2Bb%2F%3D", request.RequestUri!.Query);
            return Json("{\"value\":[{\"id\":2}]}");
        });
        var result = await client.ListAsync("testplan/plans", default);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task RepeatedTokenFailsWithoutReturningPartialTotals()
    {
        var client = Client(_ => Json("{\"value\":[]}", "repeat"));
        await Assert.ThrowsAsync<InvalidDataException>(() => client.ListAsync("testplan/plans", default));
    }

    [Theory]
    [InlineData(819259)]
    [InlineData(839718)]
    [InlineData(999999)]
    public async Task ExistingAndAdditionalPlansPreserveHierarchyAndDeduplicate(int planId)
    {
        var client = Client(request => request.RequestUri!.AbsolutePath.EndsWith("/suites")
            ? Json("{\"value\":[{\"id\":10,\"name\":\"root\"},{\"id\":11,\"name\":\"child\",\"parentSuite\":{\"id\":10}}]}")
            : Json("{\"value\":[{\"id\":1,\"results\":{\"outcome\":\"blocked\"}}]}"));
        var service = new FrontierAnalyticsService(client, Options.Create(new AzureDevOpsOptions()));
        var result = await service.GetAnalyticsAsync(planId);
        Assert.Equal(1, result.Metrics.Total);
        Assert.Equal(1, result.Metrics.Executed);
        Assert.Equal(10, result.Suites.Single(suite => suite.SuiteId == 11).ParentSuiteId);
        var subtree = await service.GetAnalyticsAsync(planId, default, 11);
        Assert.Single(subtree.Suites);
        Assert.Equal(1, subtree.Metrics.Total);
    }

    [Fact]
    public async Task OffsetPaginationContinuesAfterShortPage()
    {
        var calls = 0;
        var client = Client(request =>
        {
            calls++;
            Assert.Contains($"%24skip={calls - 1}", request.RequestUri!.Query);
            return calls <= 2 ? Json($"{{\"value\":[{{\"id\":{calls}}}]}}") : Json("{\"value\":[]}");
        });
        var result = await client.HistoryAsync("test/runs", [], default);
        Assert.Equal(2, result.Count);
    }
}
