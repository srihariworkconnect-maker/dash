using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sandpiper.QA.Dashboard.Server.Models;
using Sandpiper.QA.Dashboard.Server.Services.Analytics;
using Sandpiper.QA.Dashboard.Server.Services.Infrastructure;

namespace Sandpiper.QA.Dashboard.Server.Services;
public sealed class WorkspaceEvidenceService(FrontierAnalyticsService analytics, ExecutionHistoryService history,
    WorkItemEvidenceService workItems, AzureDevOpsClient ado, SnapshotCache cache,
    IOptions<QualityRules> rules, IOptions<AzureDevOpsOptions> options, ILogger<WorkspaceEvidenceService>? logger = null)
{
    public async Task<WorkspaceEvidence> LoadAsync(int planId, int? suiteId, ReportingWindow window,
        DateTimeOffset baselineFrom, string? tester, CancellationToken ct)
    {
        if (baselineFrom > window.From || window.From >= window.To) throw new ArgumentException("Baseline must start no later than the reporting period.");
        // Short-lived scoped cache so repeated Overview/TCM/Quality loads for the same selection
        // do not re-issue the uncached work-item/defect queries on every filter interaction.
        var scope = await ado.ScopeKeyAsync(ct);
        var key = $"{scope}:evidence:{planId}:{suiteId}:{window.From:O}:{window.To:O}:{baselineFrom:O}:{tester ?? ""}";
        return await cache.GetAsync(key, TimeSpan.FromSeconds(options.Value.CacheSeconds),
            token => CollectAsync(planId, suiteId, window, baselineFrom, tester, token), ct);
    }

    private async Task<WorkspaceEvidence> CollectAsync(int planId, int? suiteId, ReportingWindow window,
        DateTimeOffset baselineFrom, string? tester, CancellationToken ct)
    {
        var total = System.Diagnostics.Stopwatch.StartNew();
        logger?.LogInformation("Evidence collection started: plan {PlanId}; suite {SuiteId}", planId, suiteId);

        var stage = System.Diagnostics.Stopwatch.StartNew();
        var snapshot = await analytics.GetAnalyticsAsync(planId, ct, suiteId);
        logger?.LogInformation("Evidence stage 'analytics' completed in {ElapsedMs}ms: {Points} points, {Suites} suites",
            stage.ElapsedMilliseconds, snapshot.Points.Count, snapshot.SuiteCount);

        stage.Restart();
        var events = await history.ReadAsync(planId, ct);
        logger?.LogInformation("Evidence stage 'execution-history' completed in {ElapsedMs}ms: {Events} result events",
            stage.ElapsedMilliseconds, events.Count);

        stage.Restart();
        var activity = ExecutionCalculator.Calculate(events, snapshot.Points, window, baselineFrom, tester);
        logger?.LogInformation("Evidence stage 'activity-calculation' completed in {ElapsedMs}ms", stage.ElapsedMilliseconds);

        stage.Restart();
        var quality = await workItems.LoadAsync(snapshot, events, window, tester, ct);
        logger?.LogInformation("Evidence stage 'work-item-evidence' completed in {ElapsedMs}ms: available={Available}, {Defects} defects",
            stage.ElapsedMilliseconds, quality.Available, quality.Defects.Count);

        var result = new WorkspaceEvidence(snapshot, activity, QualityCalculator.Assess(snapshot, activity, quality, rules.Value)
            with { Blockers = BlockerCalculator.Calculate(snapshot, events) }, DateTimeOffset.UtcNow);
        logger?.LogInformation("Evidence collection completed for plan {PlanId} in {ElapsedMs}ms (serialization follows)",
            planId, total.ElapsedMilliseconds);
        return result;
    }
}
