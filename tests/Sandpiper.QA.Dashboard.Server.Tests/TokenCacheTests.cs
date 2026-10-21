using System.Net;
using System.Text;
using Azure.Core;
using Microsoft.Extensions.Options;
using Sandpiper.QA.Dashboard.Server.Services;
using Xunit;

namespace Sandpiper.QA.Dashboard.Server.Tests;

public class TokenCacheTests
{
    private static AccessToken Token(string value, TimeSpan validFor) => new(value, DateTimeOffset.UtcNow.Add(validFor));

    [Fact]
    public async Task ValidTokenIsReusedWithoutReacquiring()
    {
        using var cache = new AdoTokenCache();
        var calls = 0;
        Task<AccessToken> Refresh(CancellationToken _) { Interlocked.Increment(ref calls); return Task.FromResult(Token("a", TimeSpan.FromHours(1))); }
        var first = await cache.GetTokenAsync(Refresh, default);
        for (var i = 0; i < 50; i++) await cache.GetTokenAsync(Refresh, default);
        Assert.Equal(1, calls);
        Assert.Equal("a", first.Token);
    }

    [Fact]
    public async Task ConcurrentCallersTriggerASingleRefresh()
    {
        using var cache = new AdoTokenCache();
        var calls = 0;
        async Task<AccessToken> Refresh(CancellationToken ct) { Interlocked.Increment(ref calls); await Task.Delay(100, ct); return Token("a", TimeSpan.FromHours(1)); }
        var tasks = Enumerable.Range(0, 64).Select(_ => cache.GetTokenAsync(Refresh, default)).ToArray();
        var results = await Task.WhenAll(tasks);
        Assert.Equal(1, calls);
        Assert.All(results, t => Assert.Equal("a", t.Token));
    }

    [Fact]
    public async Task NearExpiryTokenInsideRefreshBufferIsReacquired()
    {
        using var cache = new AdoTokenCache();
        var calls = 0;
        // Tokens that expire within the 5-minute refresh buffer are never treated as reusable.
        Task<AccessToken> Refresh(CancellationToken _) { var n = Interlocked.Increment(ref calls); return Task.FromResult(Token($"t{n}", TimeSpan.FromMinutes(1))); }
        var a = await cache.GetTokenAsync(Refresh, default);
        var b = await cache.GetTokenAsync(Refresh, default);
        Assert.Equal(2, calls);
        Assert.NotEqual(a.Token, b.Token);
    }

    [Fact]
    public async Task LongLivedTokenAcquiredAfterANearExpiryOneIsThenReused()
    {
        using var cache = new AdoTokenCache();
        var calls = 0;
        Task<AccessToken> Refresh(CancellationToken _) { var n = Interlocked.Increment(ref calls); return Task.FromResult(Token($"t{n}", n == 1 ? TimeSpan.FromMinutes(1) : TimeSpan.FromHours(1))); }
        await cache.GetTokenAsync(Refresh, default); // near-expiry, not cached for reuse
        await cache.GetTokenAsync(Refresh, default); // long-lived, cached
        await cache.GetTokenAsync(Refresh, default); // reused
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task RefreshFailurePropagatesWithoutCachingThenRecovers()
    {
        using var cache = new AdoTokenCache();
        var calls = 0;
        Task<AccessToken> Refresh(CancellationToken _)
        {
            var n = Interlocked.Increment(ref calls);
            if (n == 1) throw new InvalidOperationException("refresh failed");
            return Task.FromResult(Token("ok", TimeSpan.FromHours(1)));
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetTokenAsync(Refresh, default));
        var recovered = await cache.GetTokenAsync(Refresh, default); // nothing cached -> retries and succeeds
        Assert.Equal("ok", recovered.Token);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task CancellationIsHonoredAndNoTokenIsAcquired()
    {
        using var cache = new AdoTokenCache();
        var calls = 0;
        Task<AccessToken> Refresh(CancellationToken _) { Interlocked.Increment(ref calls); return Task.FromResult(Token("a", TimeSpan.FromHours(1))); }
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.GetTokenAsync(Refresh, cts.Token));
        Assert.Equal(0, calls);
    }

    private sealed class CountingCredential : TokenCredential
    {
        public int Calls;
        public override AccessToken GetToken(TokenRequestContext context, CancellationToken ct)
        { Interlocked.Increment(ref Calls); return new("fixture", DateTimeOffset.UtcNow.AddHours(1)); }
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct) => ValueTask.FromResult(GetToken(context, ct));
    }
    private sealed class OkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"value\":[]}", Encoding.UTF8, "application/json") });
    }

    [Fact]
    public async Task AzureDevOpsClientReusesOneTokenAcrossManyCalls()
    {
        var options = Options.Create(new AzureDevOpsOptions { Organization = "fixture", Project = "fixture" });
        var credential = new CountingCredential();
        var client = new AzureDevOpsClient(new HttpClient(new OkHandler()), credential, options, new AdoRequestGate(options), null, new AdoTokenCache());
        for (var i = 0; i < 10; i++) await client.ListAsync("testplan/plans", default);
        Assert.Equal(1, credential.Calls);
    }
}
