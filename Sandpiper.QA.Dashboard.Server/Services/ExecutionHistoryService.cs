using System.Text.Json;
using Sandpiper.QA.Dashboard.Server.Models;
using Sandpiper.QA.Dashboard.Server.Services.Analytics;

namespace Sandpiper.QA.Dashboard.Server.Services;

public sealed class ExecutionHistoryService(AzureDevOpsClient ado, FrontierAnalyticsService analytics)
{
    public async Task<IReadOnlyList<ExecutionEvent>> ReadAsync(int planId, CancellationToken ct)
    {
        var runs = await ado.HistoryAsync("test/runs", new() { ["planId"] = planId.ToString() }, ct);
        var events = new System.Collections.Concurrent.ConcurrentBag<ExecutionEvent>();
        await Parallel.ForEachAsync(runs, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (run, token) =>
        {
            if (run.TryGetProperty("plan", out var plan) && AzureDevOpsClient.Number(plan, "id") != planId)
                throw new InvalidDataException("Mismatched run plan.");
            var runId = AzureDevOpsClient.Number(run, "id");
            var results = await ado.HistoryAsync($"test/Runs/{runId}/results", new() { ["detailsToInclude"] = "WorkItems" }, token);
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
                    AzureDevOpsClient.Text(result, "url"), ExecutionCalculator.Qualifies(outcome, state)));
            }
        });
        return events.OrderBy(e => e.RunId).ThenBy(e => e.ResultId).ToList();
    }

    public async Task<ExecutionIntelligence> ActivityAsync(int planId, int? suiteId, ReportingWindow window,
        DateTimeOffset baselineFrom, string? tester, CancellationToken ct)
    {
        var snapshot = await analytics.GetAnalyticsAsync(planId, ct, suiteId);
        var events = await ReadAsync(planId, ct);
        return ExecutionCalculator.Calculate(events, snapshot.Points, window, baselineFrom, tester);
    }
}
