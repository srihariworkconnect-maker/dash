using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sandpiper.QA.Dashboard.Server.Models;
using Sandpiper.QA.Dashboard.Server.Services.Analytics;
using Sandpiper.QA.Dashboard.Server.Services.Infrastructure;

namespace Sandpiper.QA.Dashboard.Server.Services;

public sealed class ExecutionHistoryService(AzureDevOpsClient ado, FrontierAnalyticsService analytics, SnapshotCache cache,
    ILogger<ExecutionHistoryService>? logger = null)
{
    public async Task<IReadOnlyList<ExecutionEvent>> ReadAsync(int planId, CancellationToken ct)
    {
        var key = await ado.ScopeKeyAsync(ct);
        return await cache.GetAsync($"{key}:events:{planId}", TimeSpan.FromSeconds(30), token => CollectAsync(planId, key, token), ct);
    }

    private async Task<IReadOnlyList<ExecutionEvent>> CollectAsync(int planId, string key, CancellationToken ct)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        var runs = await ado.HistoryAsync("test/runs", new() { ["planId"] = planId.ToString() }, ct);
        var planSnapshot = await analytics.GetAnalyticsAsync(planId, ct);
        var resolver = new ExecutionPointResolver(planSnapshot.Points);
        logger?.LogInformation("Execution history: fetched {Runs} runs for plan {PlanId} in {ElapsedMs}ms; reading per-run results (concurrency 4)",
            runs.Count, planId, elapsed.ElapsedMilliseconds);
        var events = new System.Collections.Concurrent.ConcurrentBag<ExecutionEvent>();
        var resultCalls = 0;
        await Parallel.ForEachAsync(runs, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (run, token) =>
        {
            if (run.TryGetProperty("plan", out var plan) && AzureDevOpsClient.Number(plan, "id") != planId)
                throw new InvalidDataException("Mismatched run plan.");
            var runId = AzureDevOpsClient.Number(run, "id");
            var revision = AzureDevOpsClient.Number(run, "revision");
            var closed = AzureDevOpsClient.Text(run, "state")?.Equals("Completed", StringComparison.OrdinalIgnoreCase) == true;
            var resultKey = $"{key}:run:{runId}:{revision}:{AzureDevOpsClient.Text(run, "lastUpdatedDate")}";
            var results = await cache.GetAsync(resultKey, closed && revision > 0 ? TimeSpan.FromSeconds(60) : TimeSpan.Zero,
                c => ado.HistoryAsync($"test/Runs/{runId}/results", new() { ["detailsToInclude"] = "WorkItems" }, c), token);
            System.Threading.Interlocked.Increment(ref resultCalls);
            foreach (var result in results)
            {
                var id = AzureDevOpsClient.Number(result, "id");
                var mapping = resolver.Resolve(result);
                var outcome = KpiCalculator.Normalize(AzureDevOpsClient.Text(result, "outcome"));
                var state = AzureDevOpsClient.Text(result, "state");
                var bugs = result.TryGetProperty("associatedBugs", out var b) && b.ValueKind == JsonValueKind.Array
                    ? b.EnumerateArray().Select(bug => AzureDevOpsClient.Number(bug, "id")).Where(n => n > 0).Distinct().ToArray() : [];
                var rawDate = AzureDevOpsClient.Text(result, "completedDate");
                events.Add(new($"{runId}:{id}", runId, id, mapping.PointId, outcome, state,
                    TestPointNormalizer.ExecutionDate(rawDate),
                    TestPointNormalizer.Identity(result, "runBy"), bugs,
                    AzureDevOpsClient.Text(result, "url"), ExecutionCalculator.Qualifies(outcome, state))
                    { PointMapping = mapping.Status,
                      BugLinksAvailable = result.TryGetProperty("associatedBugs", out var links) && links.ValueKind == JsonValueKind.Array });
            }
        });
        logger?.LogInformation("Execution history complete for plan {PlanId}: {Runs} runs, {ResultCalls} result fetches, {Events} events in {ElapsedMs}ms",
            planId, runs.Count, resultCalls, events.Count, elapsed.ElapsedMilliseconds);
        foreach (var group in events.GroupBy(e => e.PointMapping))
            logger?.LogInformation("Execution mapping: {MappingStatus}, {Records} records", group.Key, group.Count());
        return events.OrderBy(e => e.RunId).ThenBy(e => e.ResultId).ToList();
    }

    public async Task<ExecutionIntelligence> ActivityAsync(int planId, int? suiteId, ReportingWindow window,
        DateTimeOffset baselineFrom, string? tester, CancellationToken ct, string? category = null, string? outcome = null)
    {
        if (baselineFrom > window.From || window.From >= window.To) throw new ArgumentException("Baseline must start no later than the reporting period.");
        var snapshot = await analytics.GetAnalyticsAsync(planId, ct, suiteId, null, category, outcome);
        var events = await ReadAsync(planId, ct);
        var result = ExecutionCalculator.Calculate(events, snapshot.Points, window, baselineFrom, tester);
        LogDiagnostics(result);
        return result;
    }

    public void LogDiagnostics(ExecutionIntelligence activity)
    {
        foreach (var (scope, report) in new[] { ("Activity", activity.Reexecution), ("Baseline", activity.BaselineReexecution) })
        {
            if (report?.Diagnostics is not { } d) continue;
            logger?.LogInformation("Reexecution {Scope} [{From:O}, {To:O}): state {State}; source {Source}; unique results {Unique}; duplicates {Duplicates}; qualifying {Qualifying}; unique points {Points}; repeated points {Repeated}; additional attempts {Additional}",
                scope, report.Window.From, report.Window.To, report.State, d.SourceResults, d.UniqueResults,
                d.DuplicatesRemoved, d.QualifyingExecutions, d.UniqueTestPoints, d.RepeatedTestPoints, d.AdditionalAttempts);
            foreach (var exclusion in d.Excluded.Where(e => e.Value > 0))
                logger?.LogInformation("Reexecution {Scope} excluded: {Reason}, {Records} records", scope, exclusion.Key, exclusion.Value);
        }
    }
}
