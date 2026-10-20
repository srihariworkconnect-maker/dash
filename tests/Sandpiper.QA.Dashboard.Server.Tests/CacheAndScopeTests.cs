using System.Text.Json;
using Sandpiper.QA.Dashboard.Server.Models;
using Sandpiper.QA.Dashboard.Server.Services.Analytics;
using Sandpiper.QA.Dashboard.Server.Services.Infrastructure;
using Xunit;

namespace Sandpiper.QA.Dashboard.Server.Tests;

public class CacheAndScopeTests
{
    [Fact]
    public async Task CacheCoalescesLoadsButPartitionsKeysAndSupportsNestedRunLoads()
    {
        using var cache = new SnapshotCache(); var calls = 0;
        async Task<int> Load(CancellationToken ct) { Interlocked.Increment(ref calls); await Task.Delay(20, ct); return 7; }
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => cache.GetAsync("identity-a", TimeSpan.FromMinutes(1), Load, default)));
        Assert.All(results, value => Assert.Equal(7, value)); Assert.Equal(1, calls);
        Assert.Equal(9, await cache.GetAsync("identity-b", TimeSpan.FromMinutes(1),
            ct => cache.GetAsync("run-b", TimeSpan.FromMinutes(1), _ => Task.FromResult(9), ct), default));
        Assert.Equal(7, await cache.GetAsync("identity-a", TimeSpan.FromMinutes(1), _ => Task.FromResult(99), default));
        cache.Clear(); Assert.Equal(99, await cache.GetAsync("identity-a", TimeSpan.FromMinutes(1), _ => Task.FromResult(99), default));
    }

    [Fact]
    public async Task RefreshDuringLoadCannotRepopulateCacheWithOldEvidence()
    {
        using var cache = new SnapshotCache(); var loaded = new TaskCompletionSource<int>();
        var first = cache.GetAsync("source", TimeSpan.FromMinutes(1), _ => loaded.Task, default);
        cache.Clear(); loaded.SetResult(1); Assert.Equal(1, await first);
        Assert.Equal(2, await cache.GetAsync("source", TimeSpan.FromMinutes(1), _ => Task.FromResult(2), default));
    }

    [Fact]
    public async Task FailureAndCancellationNeverBecomeCachedZeroes()
    {
        using var cache = new SnapshotCache();
        await Assert.ThrowsAsync<InvalidDataException>(() => cache.GetAsync<int>("source", TimeSpan.FromMinutes(1), _ => throw new InvalidDataException(), default));
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.GetAsync("source", TimeSpan.FromMinutes(1), _ => Task.FromResult(0), cts.Token));
        Assert.Equal(3, await cache.GetAsync("source", TimeSpan.FromMinutes(1), _ => Task.FromResult(3), default));
    }

    [Fact]
    public void ScopeProjectionKeepsOwnSubtreeAndAssignmentSeparateWithoutMutatingCache()
    {
        using var json = JsonDocument.Parse("""{"id":1,"testCaseReference":{"id":8,"assignedTo":{"id":"tester","displayName":"Tester"}},"results":{"outcome":"passed"}}""");
        var point = TestPointNormalizer.Normalize(json.RootElement, 10, 2, "Root / Child") with {
            Memberships = [new(1, "Root"), new(2, "Root / Child")] };
        var metrics = KpiCalculator.Calculate([point]);
        var full = new PlanAnalytics(10, null, metrics, [new(1, "Root", null, metrics) { SubtreeMetrics = metrics }, new(2, "Child", 1, metrics) { SubtreeMetrics = metrics }], 2, DateTimeOffset.UtcNow)
            { Points = [point], Reconciliation = TestPointNormalizer.Reconcile([point]) };
        var child = ScopeProjector.Project(full, 2, "tester");
        Assert.Equal(1, child.Total); Assert.Single(child.Suites); Assert.Single(child.Points[0].Memberships);
        Assert.Equal(2, full.Points[0].Memberships.Count);
        Assert.Equal(0, ScopeProjector.Project(full, 1, "someone-else").Total);
        Assert.Throws<ArgumentException>(() => ScopeProjector.Project(full, 999, null));
    }

    [Fact]
    public void AuditKeepsOnlyBoundedRequestMetadata()
    {
        var audit = new AuditTrail();
        for (var i = 0; i < 510; i++) audit.Add(new(DateTimeOffset.UtcNow, "GET", "/api/v2/activity/{planId:int}", 200, $"trace-{i}", 1));
        Assert.Equal(500, audit.List().Count);
    }
}
