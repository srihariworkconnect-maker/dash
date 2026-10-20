import type { AdoTestPlan, AdoTestSuite } from '../types/ado';

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
    executedApplicable: number;
    executionRate: number | null;
    primaryPassRate: number | null;
    secondaryPassRate: number | null;
    statuses: Record<string, number>;
}

export interface PlanAnalytics {
    planId: number;
    suiteId: number | null;
    metrics: QaMetrics;
    suites: { suiteId: number; suiteName: string; parentSuiteId: number | null; metrics: QaMetrics; subtreeMetrics: QaMetrics }[];
    points: NormalizedPoint[];
    sourceBaseUrl: string | null;
    reconciliation: { collectionComplete: boolean; validationState: string; unspecified: number;
        missingOutcome: number; unsupportedOutcomeType: number; missingCaseIdentity: number;
        rawOutcomeTypes: Record<string, number>; warnings: string[] };
    suiteCount: number;
    retrievedAt: string;
    rulesVersion: string;
    complete: boolean;
    connected: boolean;
}

export interface Identity { key: string; displayName: string }
export interface NormalizedPoint {
    id: number; planId: number; outcome: string | null; testCaseId: number | null;
    title: string | null; configurationId: number | null; configurationName: string | null;
    assignedTester: Identity | null; latestExecutedBy: Identity | null;
    latestRunId: number | null; latestResultId: number | null; latestExecutionDate: string | null;
    outcomeJsonType: string; rawOutcome: string | null; resultState: string | null; isActive: boolean | null;
    memberships: { suiteId: number; suitePath: string }[];
}

export interface ReportingWindow { from: string; to: string }
export interface ExecutionEvent {
    key: string; runId: number; resultId: number; pointId: number | null; outcome: string;
    state: string | null; completedAt: string | null; performer: Identity | null;
    bugIds: number[]; sourceUrl: string | null; qualifies: boolean; isRepeat: boolean; transition: string | null;
}
export interface ExecutionIntelligence {
    window: ReportingWindow; baseline: ReportingWindow; events: ExecutionEvent[];
    productivity: { testerKey: string; tester: string; day: string; assignedPoints: number; pendingAssigned: number;
        executionEvents: number; uniquePoints: number; passed: number; failed: number; blocked: number;
        repeatEvents: number; reExecutedPoints: number }[];
    daily: { day: string; executionEvents: number; uniquePoints: number; repeatEvents: number; passed: number; failed: number; blocked: number }[];
    points: { pointId: number; executionCount: number; repeatCount: number }[];
    reExecutedPoints: number | null; repeatEvents: number; uniquePoints: number;
    missingPointLinks: number; missingDates: number; ambiguousChronology: number;
    complete: boolean; attribution: string; limitations: string[];
}
export interface HistoryAnalytics {
    planId: number;
    suiteId: number | null;
    metrics: {
        reExecuted: number | null;
        linkedReExecuted: number;
        additionalExecutions: number;
        executions: number;
        unlinkedResults: number;
        undatedResults: number;
        complete: boolean;
        window: ReportingWindow;
    };
    scope: string;
    runs: number;
    retrievedAt: string;
}

export function resolveApiBase(development: boolean, embedded: boolean, configured?: string): string {
    if (!configured?.trim()) {
        if (embedded) throw new Error('The installed extension requires an approved HTTPS backend endpoint and authentication configuration. Connectivity is not configured.');
        return '/api';
    }
    const value = configured.trim().replace(/\/$/, '');
    if (value.startsWith('/') && !value.startsWith('//') && !embedded) return value;
    const url = new URL(value);
    if (url.username || url.password || url.search || url.hash) throw new Error('Invalid API base URL.');
    const loopback = ['localhost', '127.0.0.1', '[::1]'].includes(url.hostname);
    if (!development && loopback) throw new Error('Production builds cannot use a localhost backend.');
    if (url.protocol !== 'https:' && !(development && loopback && url.protocol === 'http:')) {
        throw new Error('The backend requires HTTPS outside local development.');
    }
    return value;
}

export function buildSuiteTree(suites: PlanAnalytics['suites']): AdoTestSuite[] {
    const nodes = new Map(suites.map(suite => [suite.suiteId, {
        id: suite.suiteId, name: suite.suiteName, children: [] as AdoTestSuite[],
    }]));
    const roots: AdoTestSuite[] = [];
    for (const suite of suites) {
        const node = nodes.get(suite.suiteId)!;
        const parent = suite.parentSuiteId == null ? undefined : nodes.get(suite.parentSuiteId);
        if (parent) parent.children.push(node);
        else roots.push(node);
    }
    return roots;
}

export function createBackendClient(baseUrl: string) {
    async function get<T>(path: string, signal: AbortSignal): Promise<T> {
        const response = await fetch(`${baseUrl}${path}`, {
            signal, credentials: 'omit',
            headers: { Accept: 'application/json', 'X-Sandpiper-Client': 'dashboard' },
        });
        if (!response.ok) {
            let trace = '';
            try {
                const problem = await response.json() as { traceId?: string };
                if (problem.traceId) trace = ` Reference: ${problem.traceId}`;
            } catch { /* Error pages may not be JSON. Never display upstream payloads. */ }
            throw new Error(`Backend request failed (${response.status}). Check backend configuration, sign-in and permissions.${trace}`);
        }
        if (!response.headers.get('content-type')?.includes('application/json')) {
            throw new Error('Expected backend JSON. Check the API endpoint and development proxy.');
        }
        return response.json() as Promise<T>;
    }
    return {
        health: (signal: AbortSignal) => get<{ status: string; adoConnected: boolean | null }>('/health', signal),
        plans: (signal: AbortSignal) => get<{ value: AdoTestPlan[] }>('/ado/testplans', signal),
        analytics: async (planId: number, suiteId: number | null, signal: AbortSignal, tester?: string) => {
            const query = new URLSearchParams();
            if (suiteId != null) query.set('suiteId', String(suiteId));
            if (tester) query.set('tester', tester);
            const suffix = query.size ? `?${query}` : '';
            const result = await get<PlanAnalytics>(`/ado/frontier/analytics/${planId}${suffix}`, signal);
            if (result.planId !== planId || (result.suiteId ?? null) !== suiteId || !result.complete || !result.connected) {
                throw new Error('Backend returned an incomplete or mismatched analytics scope.');
            }
            return result;
        },
        dsrPeriod: (signal: AbortSignal) => get<ReportingWindow>('/reporting/dsr-period', signal),
        period: (preset: string, signal: AbortSignal) => get<ReportingWindow>(`/reporting/period?preset=${encodeURIComponent(preset)}`, signal),
        activity: (planId: number, suiteId: number | null, from: string, to: string, baselineFrom: string, tester: string, signal: AbortSignal) => {
            const query = new URLSearchParams({ from, to, baselineFrom });
            if (suiteId != null) query.set('suiteId', String(suiteId));
            if (tester) query.set('tester', tester);
            return get<ExecutionIntelligence>(`/v2/activity/${planId}?${query}`, signal);
        },
        history: (planId: number, suiteId: number | null, window: ReportingWindow, signal: AbortSignal) => {
            const query = new URLSearchParams({ from: window.from, to: window.to });
            if (suiteId != null) query.set('suiteId', String(suiteId));
            return get<HistoryAnalytics>(`/ado/frontier/history/${planId}?${query}`, signal);
        },
    };
}

export type BackendClient = ReturnType<typeof createBackendClient>;
