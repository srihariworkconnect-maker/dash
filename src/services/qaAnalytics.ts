import type { AdoTestSuite, AdoTestPoint } from '../types/ado';
import { getTestPoints, getTestRuns, getTestResults } from './adoApi';

export interface QaMetrics {
    total: number;
    passed: number;
    failed: number;
    blocked: number;
    pending: number;
    notApplicable: number;
    inProgress: number;
    otherCompleted: number;
    unknown: number;
    executed: number;
    applicable: number;
    executionRate: number;
    passRate: number;
}

const completedOutcomes = new Set([
    'passed', 'failed', 'inconclusive', 'timeout', 'aborted', 'error',
]);

export function calculateQaMetrics(points: AdoTestPoint[]): QaMetrics {
    const unique = new Map(points.map(point => [point.id, point]));
    const metrics: QaMetrics = {
        total: unique.size, passed: 0, failed: 0, blocked: 0, pending: 0,
        notApplicable: 0, inProgress: 0, otherCompleted: 0, unknown: 0,
        executed: 0, applicable: 0, executionRate: 0, passRate: 0,
    };
    for (const point of unique.values()) {
        switch (point.results?.outcome?.toLowerCase()) {
            case 'passed': metrics.passed++; break;
            case 'failed': metrics.failed++; break;
            case 'blocked': metrics.blocked++; break;
            case 'notapplicable': metrics.notApplicable++; break;
            case 'inprogress':
            case 'paused': metrics.inProgress++; break;
            case 'none':
            case 'notexecuted': metrics.pending++; break;
            case 'inconclusive':
            case 'timeout':
            case 'aborted':
            case 'error': metrics.otherCompleted++; break;
            default: metrics.unknown++; break;
        }
    }
    metrics.executed = metrics.passed + metrics.failed + metrics.otherCompleted;
    metrics.applicable = metrics.total - metrics.notApplicable;
    metrics.executionRate = metrics.applicable
        ? metrics.executed / metrics.applicable * 100 : 0;
    metrics.passRate = metrics.executed ? metrics.passed / metrics.executed * 100 : 0;
    return metrics;
}

export async function loadPlanMetrics(
    planId: number, suites: AdoTestSuite[], signal: AbortSignal,
    progress: (message: string) => void
): Promise<{ metrics: QaMetrics; points: AdoTestPoint[] }> {
    const ids = new Set<number>();
    function visit(suite: AdoTestSuite) {
        if (ids.has(suite.id)) return;
        ids.add(suite.id);
        suite.children?.forEach(visit);
    }
    suites.forEach(visit);
    const suiteIds = [...ids];
    const points = new Map<number, AdoTestPoint>();
    for (let index = 0; index < suiteIds.length; index += 4) {
        signal.throwIfAborted();
        const batch = await Promise.all(suiteIds.slice(index, index + 4).map(
            suiteId => getTestPoints(planId, suiteId, signal)
        ));
        for (const page of batch) for (const point of page) points.set(point.id, point);
        progress(`Loaded ${Math.min(index + 4, suiteIds.length)} of ${suiteIds.length} suites`);
    }
    const all = [...points.values()];
    return { metrics: calculateQaMetrics(all), points: all };
}

export interface HistoryMetrics {
    reExecuted: number;
    additionalExecutions: number;
    executions: number;
    runs: number;
    unlinkedResults: number;
}

export async function loadReexecution(
    planId: number, points: AdoTestPoint[], signal: AbortSignal,
    progress: (message: string) => void
): Promise<HistoryMetrics> {
    const pointIds = new Set(points.map(point => point.id));
    const runs = await getTestRuns(planId, signal);
    const counts = new Map<number, number>();
    const seen = new Set<string>();
    let unlinkedResults = 0;
    for (let index = 0; index < runs.length; index += 4) {
        const batch = runs.slice(index, index + 4);
        const pages = await Promise.all(batch.map(async run => ({
            run, results: await getTestResults(run.id, signal),
        })));
        for (const { run, results } of pages) {
            if (run.plan && Number(run.plan.id) !== planId) {
                throw new Error('Azure DevOps returned history for a different test plan.');
            }
            for (const result of results) {
                const key = `${run.id}:${result.id}`;
                if (seen.has(key)) continue;
                seen.add(key);
                if (result.state?.toLowerCase() !== 'completed' ||
                    !completedOutcomes.has(result.outcome?.toLowerCase() ?? '')) continue;
                const pointId = Number(result.testPoint?.id);
                if (!Number.isSafeInteger(pointId) || pointId <= 0) {
                    unlinkedResults++;
                    continue;
                }
                if (!pointIds.has(pointId)) continue;
                counts.set(pointId, (counts.get(pointId) ?? 0) + 1);
            }
        }
        progress(`Read ${Math.min(index + 4, runs.length)} of ${runs.length} test runs`);
    }
    const values = [...counts.values()];
    return {
        reExecuted: values.filter(count => count > 1).length,
        additionalExecutions: values.reduce((sum, count) => sum + Math.max(0, count - 1), 0),
        executions: values.reduce((sum, count) => sum + count, 0),
        runs: runs.length,
        unlinkedResults,
    };
}
