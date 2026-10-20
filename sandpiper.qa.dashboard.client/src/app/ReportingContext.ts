import type { NormalizedPoint, PlanAnalytics } from '../services/adoApi';

export interface ReportingContext {
    planId: number | null; suiteId: number | null; tester: string;
    preset: string; from: string; to: string; baselineFrom: string;
}
export const initialContext: ReportingContext = { planId: null, suiteId: null, tester: '', preset: 'Today', from: '', to: '', baselineFrom: '' };
export function changePlan(context: ReportingContext, planId: number): ReportingContext {
    return { ...context, planId, suiteId: null, tester: '' };
}
// Drill-through record selection only. All KPI totals/rates are calculated by the backend.
export function matchesOutcome(outcome: string, group: string): boolean {
    const status = outcome.trim().toLowerCase();
    if (!group) return true;
    if (group === 'pending') return ['none', 'active', 'notexecuted'].includes(status);
    if (group === 'executed') return ['passed', 'failed', 'blocked', 'notapplicable', 'inconclusive', 'timeout', 'aborted', 'error'].includes(status);
    if (group === 'applicable') return status !== 'notapplicable';
    if (group === 'secondary') return ['passed', 'failed', 'blocked'].includes(status);
    return status === group;
}
export function pointRows(points: NormalizedPoint[], planName: string) {
    return points.map(p => ({ key: p.id, pointId: p.id, plan: planName,
        suitePath: p.memberships.map(m => m.suitePath).join('; '), caseId: p.testCaseId,
        title: p.title, configuration: p.configurationName ?? p.configurationId,
        assigned: p.assignedTester?.displayName ?? 'Unassigned', performer: p.latestExecutedBy?.displayName,
        latestDate: p.latestExecutionDate, outcome: p.outcome ?? 'Missing',
        executionCount: null, repeatCount: null, bugIds: null, bugState: null, bugPriority: null,
        priority: null, requirement: null, evidence: null, rawOutcome: p.rawOutcome, outcomeType: p.outcomeJsonType }));
}
export function suiteRows(snapshot: PlanAnalytics) {
    return snapshot.suites.map(s => ({ key: s.suiteId, id: s.suiteId, name: s.suiteName,
        own: s.metrics.total, subtree: s.subtreeMetrics?.total, executed: s.subtreeMetrics?.executed,
        blocked: s.subtreeMetrics?.blocked, progress: s.subtreeMetrics?.executionRate }));
}
