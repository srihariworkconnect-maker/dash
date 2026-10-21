using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Microsoft.Extensions.Options;
using Sandpiper.QA.Dashboard.Server.Models;
using Sandpiper.QA.Dashboard.Server.Services;
using Sandpiper.QA.Dashboard.Server.Services.Analytics;
using Sandpiper.QA.Dashboard.Server.Services.Infrastructure;
using Xunit;

namespace Sandpiper.QA.Dashboard.Server.Tests;

public class AutomaticHistoryTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-10-01T00:00:00Z");
    private static NormalizedPoint Point(int id, int suite = 11, int configuration = 4) => new(id, "notexecuted", 1,
        50, "Synthetic case", configuration, "Synthetic configuration", null, null, null, null, null,
        "String", null, null, true, [new(suite, "Synthetic suite")]);
    private static JsonDocument Result(string json) => JsonDocument.Parse(json);

    [Fact]
    public void ExactIdentityResolvesConfigurationAndSuiteWithoutGuessingByCase()
    {
        var resolver = new ExecutionPointResolver([Point(1), Point(2, configuration: 5), Point(3, suite: 12)]);
        using var result = Result("""{"testCase":{"id":50},"testSuite":{"id":11},"configuration":{"id":5}}""");
        Assert.Equal((2, "ResolvedByIdentity"), resolver.Resolve(result.RootElement));
        using var missing = Result("""{"testCase":{"id":50}}""");
        Assert.Equal((null, "MissingIdentity"), resolver.Resolve(missing.RootElement));
        using var conflict = Result("""{"testPoint":{"id":1},"configuration":{"id":5}}""");
        Assert.Equal((null, "ConflictingIdentity"), resolver.Resolve(conflict.RootElement));
        using var unknown = Result("""{"testPoint":{"id":99},"testCase":{"id":50},"testSuite":{"id":11},"configuration":{"id":4}}""");
        Assert.Equal((99, "UnknownPoint"), resolver.Resolve(unknown.RootElement));
    }

    [Fact]
    public void AmbiguousIdentityRemainsIncompleteAndNeverChoosesFirstPoint()
    {
        using var result = Result("""{"testCase":{"id":50},"testSuite":{"id":11},"configuration":{"id":4}}""");
        Assert.Equal((null, "AmbiguousIdentity"), new ExecutionPointResolver([Point(1), Point(2)]).Resolve(result.RootElement));
    }

    [Fact]
    public void UnknownSourcePointCannotSilentlyBecomeVerifiedZero()
    {
        var attempt = new ExecutionEvent("1:1", 1, 1, 99, "passed", "Completed", Start.AddHours(1), null, [], null, true)
            { PointMapping = "UnknownPoint" };
        var activity = ExecutionCalculator.Calculate([attempt], [Point(1)], new(Start, Start.AddDays(1)), Start);
        Assert.False(activity.Complete); Assert.Null(activity.ReExecutedPoints);
        Assert.Null(activity.Reexecution!.ReexecutedTestPoints); Assert.Equal("Incomplete", activity.Reexecution.State);
        Assert.Equal(1, activity.Reexecution.Diagnostics!.Excluded["UnknownPointIdentity"]);
        Assert.Equal(1, activity.Reexecution.Diagnostics.SourceResults);
    }

    [Fact]
    public void DiagnosticsReconcileAllRecordsAndPreserveBaselineVersusPeriod()
    {
        ExecutionEvent E(int id, int point, DateTimeOffset at, string outcome = "passed", string state = "Completed") =>
            new($"1:{id}", 1, id, point, outcome, state, at, null, [], null, ExecutionCalculator.Qualifies(outcome, state));
        var prior = E(1, 1, Start.AddDays(-1)); var current = E(2, 1, Start.AddHours(1));
        var records = new[] { prior, current, current, E(3, 2, Start.AddHours(1)),
            E(4, 1, Start.AddDays(1)), E(5, 1, Start, "notexecuted"), E(6, 1, Start, "passed", "InProgress") };
        var activity = ExecutionCalculator.Calculate(records, [Point(1)], new(Start, Start.AddDays(1)), Start.AddDays(-2));
        Assert.Equal(0, activity.Reexecution!.ReexecutedTestPoints);
        Assert.Equal(1, activity.BaselineReexecution!.ReexecutedTestPoints);
        Assert.Equal(1, activity.RepeatEvents);
        var d = activity.Reexecution.Diagnostics!;
        Assert.Equal(7, d.SourceResults); Assert.Equal(6, d.UniqueResults); Assert.Equal(1, d.DuplicatesRemoved);
        Assert.Equal(1, d.QualifyingExecutions); Assert.Equal(1, d.UniqueTestPoints);
        Assert.Equal(d.UniqueResults, d.QualifyingExecutions + d.Excluded.Values.Sum());
        Assert.Equal(1, d.Excluded["OutsideSelectedPoints"]); Assert.Equal(1, d.Excluded["BeforeStart"]);
        Assert.Equal(1, d.Excluded["AtOrAfterEnd"]); Assert.Equal(1, d.Excluded["NonQualifyingOutcome"]);
        Assert.Equal(1, d.Excluded["NotCompleted"]);
    }

    [Fact]
    public void LegacyEndpointTreatsSentinelAsMissingHistoryRatherThanZero()
    {
        var result = KpiCalculator.Reexecution([new(1, 1, 1, "passed", "Completed",
            TestPointNormalizer.ExecutionDate("0001-01-01T00:00:00Z"))], new HashSet<int> { 1 }, new(Start, Start.AddDays(1)));
        Assert.False(result.Complete); Assert.Null(result.ReExecuted); Assert.Equal(1, result.UndatedResults);
    }

    private sealed class Credential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext context, CancellationToken ct) => new("synthetic", DateTimeOffset.MaxValue);
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct) => ValueTask.FromResult(GetToken(context, ct));
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(response(request));
    }
    private static HttpResponseMessage Json(string items) => new(HttpStatusCode.OK)
        { Content = new StringContent("{\"value\":" + items + "}", Encoding.UTF8, "application/json") };

    [Theory]
    [InlineData(819259, false, false)]
    [InlineData(839718, false, false)]
    [InlineData(123456, true, false)]
    [InlineData(123456, false, true)]
    public async Task AutomaticallyDiscoversRunsResolvesPointsAndFiltersSuiteSubtree(int planId, bool differentConfiguration, bool denied)
    {
        using var cache = new SnapshotCache();
        var options = Options.Create(new AzureDevOpsOptions { Organization = "fixture", Project = "fixture", CacheSeconds = 30 });
        var runRequests = 0;
        var client = new AzureDevOpsClient(new HttpClient(new Handler(request =>
        {
            var uri = request.RequestUri!; var path = uri.AbsolutePath;
            if (path.EndsWith("/suites")) return Json("""[{"id":10,"name":"Root"},{"id":11,"name":"Child","parentSuite":{"id":10}},{"id":12,"name":"Sibling"}]""");
            if (path.EndsWith("/11/TestPoint")) return Json("""[{"id":1,"testCaseReference":{"id":50},"configuration":{"id":4},"results":{"outcome":"notExecuted"}},{"id":2,"testCaseReference":{"id":50},"configuration":{"id":5},"results":{"outcome":"notExecuted"}}]""");
            if (path.EndsWith("/12/TestPoint")) return Json("""[{"id":3,"testCaseReference":{"id":50},"configuration":{"id":4},"results":{"outcome":"notExecuted"}}]""");
            if (path.EndsWith("/TestPoint")) return Json("[]");
            if (path.EndsWith("/test/runs"))
            {
                if (denied) return new(HttpStatusCode.Forbidden);
                Assert.Contains($"planId={planId}", uri.Query); runRequests++;
                return uri.Query.Contains("%24skip=0") ? Json("""[{"id":1},{"id":2}]""") : Json("[]");
            }
            if (path.EndsWith("/results"))
            {
                if (!uri.Query.Contains("%24skip=0")) return Json("[]");
                var configuration = differentConfiguration && path.Contains("/2/results") ? 5 : 4;
                return Json($$"""[{"id":1,"testCase":{"id":50},"testSuite":{"id":11},"configuration":{"id":{{configuration}}},"outcome":"Passed","state":"Completed","completedDate":"2026-10-01T01:00:00Z"},{"id":2,"testPoint":{"id":3},"outcome":"Failed","state":"Completed","completedDate":"2026-10-01T02:00:00Z"}]""");
            }
            throw new InvalidOperationException("Unexpected fixture endpoint.");
        })), new Credential(), options, new AdoRequestGate(options));
        var analytics = new FrontierAnalyticsService(client, options, cache);
        var service = new ExecutionHistoryService(client, analytics, cache);
        if (denied)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => service.ActivityAsync(planId, 10, new(Start, Start.AddDays(1)), Start, null, default));
            return; // No success result or zero-valued report is returned on upstream failure.
        }
        var activity = await service.ActivityAsync(planId, 10, new(Start, Start.AddDays(1)), Start, null, default);
        Assert.True(activity.Complete);
        Assert.Equal(differentConfiguration ? 0 : 1, activity.Reexecution!.ReexecutedTestPoints);
        Assert.Equal(differentConfiguration ? 0 : 1, activity.Reexecution.AdditionalAttempts);
        Assert.Equal(2, activity.Reexecution.Diagnostics!.QualifyingExecutions);
        Assert.Equal(2, activity.Reexecution.Diagnostics.Excluded["OutsideSelectedPoints"]);
        Assert.Equal(2, runRequests);
    }
}
