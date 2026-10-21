using System.Net;
using System.Text;
using Azure.Core;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Sandpiper.QA.Dashboard.Server.Services;
using Xunit;

namespace Sandpiper.QA.Dashboard.Server.Tests;

public class AdoIntegrationTests
{
    private sealed class SafeLogger : ILogger<AzureDevOpsClient>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, error));
    }

    [Fact]
    public async Task FailureDiagnosticsDoNotLogUpstreamBodiesOrAccessTokens()
    {
        var options = Options.Create(new AzureDevOpsOptions { Organization = "fixture", Project = "fixture" });
        var logger = new SafeLogger();
        var client = new AzureDevOpsClient(new HttpClient(new Handler(_ => new(HttpStatusCode.Forbidden)
            { Content = new StringContent("CONFIDENTIAL_SYNTHETIC_ERROR_CONTENT") })),
            new Credential(), options, new AdoRequestGate(options), logger);
        await Assert.ThrowsAsync<HttpRequestException>(() => client.ListAsync("test/runs", default));
        var logged = string.Join("\n", logger.Messages);
        Assert.Contains("403", logged);
        Assert.DoesNotContain("CONFIDENTIAL_SYNTHETIC_ERROR_CONTENT", logged);
        Assert.DoesNotContain("test-fixture", logged);
    }

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

    [Theory]
    [InlineData("https://dev.azure.com/fixture/_apis/wit/workItems/10", 10)]
    [InlineData("https://fixture.visualstudio.com/project/_apis/wit/workItems/10", 10)]
    [InlineData("https://dev.azure.com/another-org/_apis/wit/workItems/10", null)]
    [InlineData("https://untrusted.example/fixture/_apis/wit/workItems/10", null)]
    [InlineData("https://dev.azure.com/fixture/_apis/wit/attachments/10", null)]
    [InlineData("https://user:password@dev.azure.com/fixture/_apis/wit/workItems/10", null)]
    public void RelationshipIdentityRequiresConfiguredOrganizationAndActualWorkItemPath(string url, int? expected)
    {
        Assert.Equal(expected, Client(_ => throw new InvalidOperationException("No network is needed to parse a relationship")).WorkItemId(url));
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

    private sealed class HangingCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext context, CancellationToken ct) =>
            GetTokenAsync(context, ct).AsTask().GetAwaiter().GetResult();
        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct)
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new("never", DateTimeOffset.MaxValue);
        }
    }

    [Fact]
    public async Task StalledTokenAcquisitionFailsExplicitlyInsteadOfHangingForever()
    {
        var options = Options.Create(new AzureDevOpsOptions { Organization = "fixture", Project = "fixture", TokenTimeoutSeconds = 1 });
        var client = new AzureDevOpsClient(new HttpClient(new Handler(_ => Json("{\"value\":[]}"))),
            new HangingCredential(), options, new AdoRequestGate(options));
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        await Assert.ThrowsAsync<TimeoutException>(() => client.ListAsync("testplan/plans", default));
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(15), "Token acquisition must fail under the configured bound, not hang.");
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

    [Fact]
    public async Task ResultPaginationDeduplicatesOverlappingPages()
    {
        var calls = 0;
        var client = Client(request =>
        {
            Assert.EndsWith("/test/Runs/7/results", request.RequestUri!.AbsolutePath);
            return ++calls switch
            {
                1 => Json("""{"value":[{"id":1},{"id":2}]}"""),
                2 => Json("""{"value":[{"id":2},{"id":3}]}"""),
                _ => Json("""{"value":[]}""")
            };
        });
        var results = await client.HistoryAsync("test/Runs/7/results", [], default);
        Assert.Equal(3, results.Count); Assert.Equal(3, calls);
    }

    [Fact]
    public async Task ChangingResultAcrossPagesFailsWithoutPartialHistory()
    {
        var calls = 0;
        var client = Client(_ => ++calls == 1
            ? Json("""{"value":[{"id":1,"outcome":"Failed"}]}""")
            : Json("""{"value":[{"id":1,"outcome":"Passed"}]}"""));
        await Assert.ThrowsAsync<InvalidDataException>(() => client.HistoryAsync("test/Runs/7/results", [], default));
    }
}
