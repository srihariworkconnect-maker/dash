import type { AdoTestPlan, AdoTestSuite } from '../types/ado';
export interface EvidenceAssessment { state: 'Verified' | 'Incomplete' | 'Unavailable' | 'PermissionDenied'; reasons: string[] }
export class BackendRequestError extends Error {
    status: number;
    constructor(status: number, message: string) { super(message); this.name = 'BackendRequestError'; this.status = status; }
}

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
    caseMetrics?: { totalTestCases: number | null; executedTestCases: number | null; executionCoverage: number | null;
        knownTestCases: number; knownExecutedTestCases: number; missingCaseMappings: number; unclassifiedCases?: number; state: string; testCaseIds: number[]; executedCaseIds: number[] };
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
    cacheAgeSeconds?: number;
    assignments?: { testerKey: string; tester: string; assignedNow: number; pendingNow: number }[];
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
export interface ReportSnapshot {
    id: string; kind: string; createdAt: string; calculationVersion: string; status: string; origin: string;
    checksum: string; currentSnapshot: PlanAnalytics; activity: ExecutionIntelligence;
    narrative: Record<string, string>; limitations: string[];
    quality?: QualityEvidence;
}
export interface DefectEvidence {
    linkedCaseIds?: number[];
    bugId: number; title: string | null; createdBy: Identity | null; createdAt: string | null;
    assignedTo: Identity | null; state: string | null; category: string | null; severity: string | null; priority: string | null;
    revisions: { revision: number; state: string | null; category: string | null; changedAt: string | null; changedBy: Identity | null }[];
    relatedResults: ExecutionEvent[]; verificationStatus: string; retestedBy: Identity | null; retestedAt: string | null;
    verificationOutcome: string | null; verificationEvidence: string[]; ageDays: number | null;
}
export interface QualityEvidence {
    totalLinkedDefects?: number | null; completedLinkedDefects?: number | null; evidence?: EvidenceAssessment;
    available: boolean; schemaStatus: string; retrievedAt: string; inspectedFields: string[];
    stateCategories: Record<string, string>; relationshipsComplete: boolean;
    cases: { caseId: number; priority: string | null; requirementIds: number[]; evidenceLinks: string[] }[];
    defects: DefectEvidence[]; bugActivity: { testerKey: string; tester: string; day?: string; raised: number; resolved: number; reopened: number; verifiedWithEvidence: number; verificationAttempts: number }[];
    openLinkedDefects: number | null; requirementGaps: number | null; releaseReadiness: string;
    riskReasons: string[]; forecast: Record<string, unknown> | null; limitations: string[];
    blockers?: { pointId: number; observedSince: string | null; observedAgeDays: number | null; evidenceStatus: string }[];
}
export interface WorkspaceEvidence { snapshot: PlanAnalytics; activity: ExecutionIntelligence; quality: QualityEvidence; retrievedAt: string }
export interface ExecutionEvent {
    key: string; runId: number; resultId: number; pointId: number | null; outcome: string;
    state: string | null; completedAt: string | null; performer: Identity | null;
    bugIds: number[]; sourceUrl: string | null; qualifies: boolean; isRepeat: boolean; transition: string | null;
}
export interface ExecutionIntelligence {
    evidence?: EvidenceAssessment;
    caseRetesting?: { reExecutedTestCases: number | null; knownReExecutedTestCases: number; missingCaseMappings: number; state: string;
        cases: { caseId: number; pointIds: number[]; attempts: ExecutionEvent[] }[] };
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
    async function get<T>(path: string, signal: AbortSignal, body?: unknown): Promise<T> {
        const response = await fetch(`${baseUrl}${path}`, {
            signal, credentials: 'omit',
            method: body == null ? 'GET' : 'POST', body: body == null ? undefined : JSON.stringify(body),
            headers: { Accept: 'application/json', 'X-Sandpiper-Client': 'dashboard', ...(body == null ? {} : { 'Content-Type': 'application/json' }) },
        });
        if (!response.ok) {
            let trace = '';
            try {
                const problem = await response.json() as { traceId?: string };
                if (problem.traceId) trace = ` Reference: ${problem.traceId}`;
            } catch { /* Error pages may not be JSON. Never display upstream payloads. */ }
            throw new BackendRequestError(response.status, `${response.status === 403 || response.status === 401 ? 'Permission denied' : 'Backend request failed'} (${response.status}). Check backend configuration, sign-in and permissions.${trace}`);
        }
        if (!response.headers.get('content-type')?.includes('application/json')) {
            throw new Error('Expected backend JSON. Check the API endpoint and development proxy.');
        }
        return response.json() as Promise<T>;
    }
    return {
        reportingSettings: (signal: AbortSignal) => get<{ timezone: string; dsrCutoff: string; cacheSeconds: number }>('/reporting/settings', signal),
        exportTable: async (body: { name: string; columns: string[]; rows: (string | null)[][] }, signal: AbortSignal) => {
            const response = await fetch(`${baseUrl}/v2/exports/table`, { method: 'POST', signal, credentials: 'omit',
                headers: { 'Content-Type': 'application/json', 'X-Sandpiper-Client': 'dashboard' }, body: JSON.stringify(body) });
            if (!response.ok) throw new Error(`Workbook export unavailable (${response.status}). Maximum 50,000 rows / 20 MB.`);
            return response.blob();
        },
        refresh: (signal: AbortSignal) => get<{ status: string }>('/v2/cache/refresh', signal, {}),
        audit: (signal: AbortSignal) => get<{ scope: string; entries: Record<string, unknown>[] }>('/v2/audit', signal),
        auth: (signal: AbortSignal) => get<{ authenticated: boolean; adoAuthorizationValidated?: boolean; reason?: string }>('/auth/check', signal),
        reconcile: (planId: number, suiteId: number | null, tester: string, counts: Record<string, number>, signal: AbortSignal) => {
            const query = new URLSearchParams();
            if (suiteId != null) query.set('suiteId', String(suiteId));
            if (tester) query.set('tester', tester);
            return get<{ status: string; differences: { category: string; source: number; reference: number; difference: number }[] }>(`/v2/reconcile/${planId}?${query}`, signal, counts);
        },
        createReport: (body: unknown, signal: AbortSignal) => get<ReportSnapshot>('/v2/reports', signal, body),
        evidence: async (planId: number, suiteId: number | null, from: string, to: string, baselineFrom: string, tester: string, signal: AbortSignal) => {
            const query = new URLSearchParams({ from, to, baselineFrom });
            if (suiteId != null) query.set('suiteId', String(suiteId));
            if (tester) query.set('tester', tester);
            const result = await get<WorkspaceEvidence>(`/v2/evidence/${planId}?${query}`, signal);
            if (result.snapshot?.planId !== planId || (result.snapshot?.suiteId ?? null) !== suiteId || !result.snapshot?.complete || !result.snapshot?.connected ||
                Date.parse(result.activity?.window?.from) !== Date.parse(from) || Date.parse(result.activity?.window?.to) !== Date.parse(to) ||
                Date.parse(result.activity?.baseline?.from) !== Date.parse(baselineFrom)) throw new Error('Backend returned mismatched evidence scope or reporting boundaries.');
            return result;
        },
        compare: (plans: number[], signal: AbortSignal) => get<{ scope: string; snapshots: PlanAnalytics[] }>(`/v2/compare?plans=${plans.join(',')}`, signal),
        importReport: (body: unknown, signal: AbortSignal) => get<ReportSnapshot>('/v2/reports/import', signal, body),
        reports: (signal: AbortSignal) => get<{ id: string; kind: string; createdAt: string; status: string; planId: number; checksum: string }[]>('/v2/reports', signal),
        report: (id: string, signal: AbortSignal) => get<ReportSnapshot>(`/v2/reports/${encodeURIComponent(id)}`, signal),
        exportReport: async (id: string, format: string, signal: AbortSignal) => {
            const response = await fetch(`${baseUrl}/v2/reports/${encodeURIComponent(id)}/${format}`, { signal,
                credentials: 'omit', headers: { 'X-Sandpiper-Client': 'dashboard' } });
            if (!response.ok) throw new Error(`Export unavailable (${response.status}). The snapshot may have expired or the PDF font is unavailable.`);
            return response.blob();
        },
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
