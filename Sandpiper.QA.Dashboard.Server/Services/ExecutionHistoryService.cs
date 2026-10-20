using System.Text.Json;
using Sandpiper.QA.Dashboard.Server.Models;
using Sandpiper.QA.Dashboard.Server.Services.Analytics;
using Sandpiper.QA.Dashboard.Server.Services.Infrastructure;

namespace Sandpiper.QA.Dashboard.Server.Services;

public sealed class ExecutionHistoryService(AzureDevOpsClient ado, FrontierAnalyticsService analytics, SnapshotCache cache)
{
    public async Task<IReadOnlyList<ExecutionEvent>> ReadAsync(int planId, CancellationToken ct)
    {
        var key = await ado.ScopeKeyAsync(ct);
        return await cache.GetAsync($"{key}:events:{planId}", TimeSpan.FromSeconds(30), token => CollectAsync(planId, key, token), ct);
    }

    private async Task<IReadOnlyList<ExecutionEvent>> CollectAsync(int planId, string key, CancellationToken ct)
    {
        var runs = await ado.HistoryAsync("test/runs", new() { ["planId"] = planId.ToString() }, ct);
        var events = new System.Collections.Concurrent.ConcurrentBag<ExecutionEvent>();
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
            foreach (var result in results)
            {
                var id = AzureDevOpsClient.Number(result, "id");
                var point = result.TryGetProperty("testPoint", out var p) && p.ValueKind == JsonValueKind.Object ? AzureDevOpsClient.Number(p, "id") : 0;
                var outcome = KpiCalculator.Normalize(AzureDevOpsClient.Text(result, "outcome"));
                var state = AzureDevOpsClient.Text(result, "state");
                var bugs = result.TryGetProperty("associatedBugs", out var b) && b.ValueKind == JsonValueKind.Array
                    ? b.EnumerateArray().Select(bug => AzureDevOpsClient.Number(bug, "id")).Where(n => n > 0).Distinct().ToArray() : [];
                var rawDate = AzureDevOpsClient.Text(result, "completedDate");
                events.Add(new($"{runId}:{id}", runId, id, point > 0 ? point : null, outcome, state,
                    DateTimeOffset.TryParse(rawDate, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AssumeUniversal, out var date) ? date.ToUniversalTime() : null,
                    TestPointNormalizer.Identity(result, "runBy"), bugs,
                    AzureDevOpsClient.Text(result, "url"), ExecutionCalculator.Qualifies(outcome, state))
                    { BugLinksAvailable = result.TryGetProperty("associatedBugs", out var links) && links.ValueKind == JsonValueKind.Array });
            }
        });
        return events.OrderBy(e => e.RunId).ThenBy(e => e.ResultId).ToList();
    }

    public async Task<ExecutionIntelligence> ActivityAsync(int planId, int? suiteId, ReportingWindow window,
        DateTimeOffset baselineFrom, string? tester, CancellationToken ct)
    {
        if (baselineFrom > window.From || window.From >= window.To) throw new ArgumentException("Baseline must start no later than the reporting period.");
        var snapshot = await analytics.GetAnalyticsAsync(planId, ct, suiteId);
        var events = await ReadAsync(planId, ct);
        return ExecutionCalculator.Calculate(events, snapshot.Points, window, baselineFrom, tester);
    }
}
