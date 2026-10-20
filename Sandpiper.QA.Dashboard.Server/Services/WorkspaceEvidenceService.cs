using Microsoft.Extensions.Options;
using Sandpiper.QA.Dashboard.Server.Models;
using Sandpiper.QA.Dashboard.Server.Services.Analytics;

namespace Sandpiper.QA.Dashboard.Server.Services;
public sealed class WorkspaceEvidenceService(FrontierAnalyticsService analytics, ExecutionHistoryService history,
    WorkItemEvidenceService workItems, IOptions<QualityRules> rules)
{
    public async Task<WorkspaceEvidence> LoadAsync(int planId, int? suiteId, ReportingWindow window,
        DateTimeOffset baselineFrom, string? tester, CancellationToken ct)
    {
        if (baselineFrom > window.From || window.From >= window.To) throw new ArgumentException("Baseline must start no later than the reporting period.");
        var snapshot = await analytics.GetAnalyticsAsync(planId, ct, suiteId);
        var events = await history.ReadAsync(planId, ct);
        var activity = ExecutionCalculator.Calculate(events, snapshot.Points, window, baselineFrom, tester);
        var quality = await workItems.LoadAsync(snapshot, events, window, tester, ct);
        return new(snapshot, activity, QualityCalculator.Assess(snapshot, activity, quality, rules.Value)
            with { Blockers = BlockerCalculator.Calculate(snapshot, events) }, DateTimeOffset.UtcNow);
    }
}
