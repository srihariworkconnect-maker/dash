using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sandpiper.QA.Dashboard.Server.Services;
using Xunit;

namespace Sandpiper.QA.Dashboard.Server.Tests;

public class ApiContractTests
{
    private sealed class Credential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext context, CancellationToken ct) => new("fixture-only", DateTimeOffset.MaxValue);
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct) => ValueTask.FromResult(GetToken(context, ct));
    }
    private sealed class Fixture : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var suites = request.RequestUri!.AbsolutePath.EndsWith("/suites");
            var json = suites ? "{\"value\":[{\"id\":10,\"name\":\"fixture-suite\"}]}"
                : "{\"value\":[{\"id\":1,\"results\":{\"outcome\":\"passed\"}},{\"id\":2,\"results\":{\"outcome\":\"blocked\"}},{\"id\":3,\"results\":{\"outcome\":\"notApplicable\"}}]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
    private sealed class Loopback : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, following) => { context.Connection.RemoteIpAddress = IPAddress.Loopback; return following(context); });
            next(app);
        };
    }
    private static WebApplicationFactory<Program> Factory(string environment = "Development") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TokenCredential>();
                services.AddSingleton<TokenCredential>(new Credential());
                services.AddSingleton<IStartupFilter, Loopback>();
                services.AddHttpClient<AzureDevOpsClient>().ConfigurePrimaryHttpMessageHandler(() => new Fixture());
            });
        });

    [Theory]
    [InlineData(819259)]
    [InlineData(839718)]
    public async Task OriginalFrontierRoutesReturnSharedKpis(int planId)
    {
        await using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Sandpiper-Client", "dashboard");
        var response = await client.GetAsync($"/api/ado/frontier/analytics/{planId}");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(planId, body.GetProperty("planId").GetInt32());
        Assert.Equal(3, body.GetProperty("metrics").GetProperty("executed").GetInt32());
        Assert.Equal(50, body.GetProperty("metrics").GetProperty("secondaryPassRate").GetDouble());
        Assert.Equal(body.GetProperty("metrics").GetProperty("primaryPassRate").GetDouble(), body.GetProperty("passPercentage").GetDouble());
    }

    [Fact]
    public async Task HealthDoesNotClaimAdoValidationAndControllerActivates()
    {
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var health = await client.GetFromJsonAsync<JsonElement>("/api/health");
        Assert.Equal(JsonValueKind.Null, health.GetProperty("adoConnected").ValueKind);
        client.DefaultRequestHeaders.Add("X-Sandpiper-Client", "dashboard");
        var response = await client.GetAsync("/api/TestAnalytics/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await client.GetFromJsonAsync<JsonElement>("/api/auth/check");
        Assert.True(auth.GetProperty("authenticated").GetBoolean());
        Assert.False(auth.GetProperty("adoAuthorizationValidated").GetBoolean());
    }

    [Fact]
    public async Task UnsafeBrowserRequestsAreRejected()
    {
        await using var factory = Factory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/ado/testplans")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Sandpiper-Client", "dashboard");
        client.DefaultRequestHeaders.Add("Origin", "https://untrusted.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/ado/testplans")).StatusCode);
    }

    [Fact]
    public async Task ProductionFailsClosedUntilAuthenticationIsApproved()
    {
        await using var factory = Factory("Production");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Sandpiper-Client", "dashboard");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/ado/testplans")).StatusCode);
    }

    [Theory]
    [InlineData("2026-10-18T18:00:00", "2026-10-19T18:00:00Z")]
    [InlineData("2026-10-20T18:00:00Z", "2026-10-19T18:00:00Z")]
    public async Task InvalidWindowsFailBeforeHistoryIsReturned(string from, string to)
    {
        await using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Sandpiper-Client", "dashboard");
        var result = await client.GetAsync($"/api/ado/frontier/history/819259?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}");
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }
}
