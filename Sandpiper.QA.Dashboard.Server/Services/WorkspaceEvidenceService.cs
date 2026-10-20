using Microsoft.Extensions.Options;
using Sandpiper.QA.Dashboard.Server.Models;
using Sandpiper.QA.Dashboard.Server.Services.Analytics;
using Sandpiper.QA.Dashboard.Server.Services.Infrastructure;

namespace Sandpiper.QA.Dashboard.Server.Services;
public sealed class WorkspaceEvidenceService(FrontierAnalyticsService analytics, ExecutionHistoryService history,
    WorkItemEvidenceService workItems, AzureDevOpsClient ado, SnapshotCache cache,
    IOptions<QualityRules> rules, IOptions<AzureDevOpsOptions> options)
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
        var snapshot = await analytics.GetAnalyticsAsync(planId, ct, suiteId);
        var events = await history.ReadAsync(planId, ct);
        var activity = ExecutionCalculator.Calculate(events, snapshot.Points, window, baselineFrom, tester);
        var quality = await workItems.LoadAsync(snapshot, events, window, tester, ct);
        return new(snapshot, activity, QualityCalculator.Assess(snapshot, activity, quality, rules.Value)
            with { Blockers = BlockerCalculator.Calculate(snapshot, events) }, DateTimeOffset.UtcNow);
    }
}
