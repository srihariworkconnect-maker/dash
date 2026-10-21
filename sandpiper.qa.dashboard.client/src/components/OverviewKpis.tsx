import { useEffect, useState } from 'react';
import { BackendRequestError, type BackendClient, type PlanAnalytics, type WorkspaceEvidence } from '../services/adoApi';
import type { ReportingContext } from '../app/ReportingContext';
import DataTable, { type DataRow } from './DataTable';
import ReexecutionDetails from './ReexecutionDetails';

function scopeProblem(c: ReportingContext): string | null {
    if (!c.baselineFrom) return 'Choose an explicit repeat baseline to load historical and linked-defect evidence.';
    if (![c.from, c.to, c.baselineFrom].every(v => /(Z|[+-]\d{2}:\d{2})$/i.test(v) && Number.isFinite(Date.parse(v)))) return 'Reporting dates and baseline require valid ISO dates with UTC offsets.';
    if (Date.parse(c.from) >= Date.parse(c.to) || Date.parse(c.baselineFrom) > Date.parse(c.from)) return 'Baseline must start at or before the reporting period, and From must precede To.';
    return null;
}
const rate = (value: number | null | undefined) => value == null ? 'Unavailable' : `${value.toFixed(1)}%`;
const evidenceLabel = (state?: string) => state === 'PermissionDenied' ? 'Permission denied' : state === 'Incomplete' ? 'Incomplete evidence' : 'Unavailable';

export default function OverviewKpis({ api, context, snapshot, evidence, onLoaded, drill }: {
    api: BackendClient; context: ReportingContext; snapshot: PlanAnalytics; evidence: WorkspaceEvidence | null;
    onLoaded: (data: WorkspaceEvidence) => void; drill: (status: string) => void;
}) {
    const problem = scopeProblem(context);
    const [data, setData] = useState<WorkspaceEvidence | null>(evidence);
    const [phase, setPhase] = useState(evidence ? 'Ready' : problem ? 'Idle' : 'Loading');
    const [message, setMessage] = useState(problem ?? '');
    const [retry, setRetry] = useState(0);
    const [details, setDetails] = useState<string | null>(null);
    useEffect(() => {
        if (problem || !context.planId || (evidence && retry === 0)) return;
        const c = new AbortController();
        const timer = setTimeout(() => {
            setData(null); setPhase('Loading'); setMessage('');
            void api.evidence(context.planId!, context.suiteId, context.from, context.to, context.baselineFrom, context.tester, c.signal, context.category, context.outcome)
                .then(result => { if (!c.signal.aborted) { setData(result); setPhase('Ready'); onLoaded(result); } })
                .catch(error => { if (!c.signal.aborted) {
                    setPhase(error instanceof BackendRequestError && [401, 403].includes(error.status) ? 'PermissionDenied' : 'Unavailable');
                    setMessage(error instanceof Error ? error.message : 'Evidence request failed.');
                } });
        }, 300);
        return () => { clearTimeout(timer); c.abort(); };
        // Parent result notification/evidence reuse is not a request dependency. The component is keyed by scope/refresh.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [api, context, problem, retry]);
    const cm = snapshot.caseMetrics, m = snapshot.metrics, q = data?.quality, activity = data?.activity;
    const unavailable = phase === 'Loading' ? 'Loading' : phase === 'PermissionDenied' ? 'Permission denied' : phase === 'Idle' ? 'Awaiting baseline' : 'Unavailable';
    const readiness = q?.releaseReadiness.replace(/^not assessed/i, 'Not Assessed') ?? (phase === 'Idle' ? 'Not Assessed' : unavailable);
    const historyState = activity?.reexecution?.state;
    const historyValue = historyState === 'Verified' && activity?.reexecution?.reexecutedTestPoints != null
        ? activity.reexecution.reexecutedTestPoints : data ? evidenceLabel(historyState) : unavailable;
    const additionalValue = historyState === 'Verified' && activity?.reexecution?.additionalAttempts != null
        ? activity.reexecution.additionalAttempts : data ? evidenceLabel(historyState) : unavailable;
    const defectValue = (value: number | null | undefined) => q?.available && q.relationshipsComplete && q.evidence?.state === 'Verified' && value != null
        ? value : q ? evidenceLabel(q.evidence?.state ?? (q.available ? 'Incomplete' : 'Unavailable')) : unavailable;
    const card = (label: string, value: string | number, unit: string, formula: string, action: () => void, loading = false) =>
        <button key={label} className={`compact-kpi business-kpi ${loading ? 'kpi-loading' : ''}`} title={formula} onClick={action} aria-busy={loading}>
            <small>{label}</small><b>{value}</b><span>{unit}</span><span className="kpi-action">View evidence →</span></button>;
    const caseValue = (value: number | null | undefined) => value != null ? value : cm?.state === 'Incomplete' ? 'Incomplete evidence' : 'Unavailable';
    const caseIds = details === 'executedCases' ? cm?.executedCaseIds : cm?.testCaseIds;
    const grouped = new Map<number, PlanAnalytics['points']>();
    if (details === 'cases' || details === 'executedCases') for (const p of snapshot.points) if (p.testCaseId != null) {
        const group = grouped.get(p.testCaseId) ?? []; group.push(p); grouped.set(p.testCaseId, group);
    }
    const executedIds = new Set(cm?.executedCaseIds ?? []);
    const caseRows = details === 'cases' || details === 'executedCases' ? (caseIds ?? []).map(id => ({ caseId: id, title: grouped.get(id)?.[0]?.title,
        pointIds: grouped.get(id)?.map(p => p.id).join(', '),
        executed: executedIds.has(id) ? 'At least one executed point' : 'No known executed point',
        outcomes: [...new Set(grouped.get(id)?.map(p => p.outcome ?? 'Missing'))].join(', ') })) : [];
    const defects = (q?.defects ?? []).filter(b => details !== 'openDefects' && details !== 'completedDefects' ||
        (details === 'openDefects' ? !['Completed', 'Removed'].includes(b.category ?? 'Unknown') : b.category === 'Completed'));
    return <div className="overview-groups">
        <section aria-label="Test Coverage"><h3>Test Coverage</h3><div className="compact-kpis">
            {card('Total Test Cases', caseValue(cm?.totalTestCases), 'distinct ADO case IDs', 'Distinct test case IDs across selected suites/configurations. Missing mapping withholds the complete count.', () => setDetails('cases'))}
            {card('Total Test Points', m.total, 'distinct point IDs', 'Distinct test point identities; re-execution does not increase this total.', () => drill(''))}
            {card('Executed Test Cases', caseValue(cm?.executedTestCases), 'distinct ADO case IDs', 'Distinct cases with at least one point in the existing executed-status set, including Blocked and N/A.', () => setDetails('executedCases'))}
            {card('Execution Coverage', cm?.state === 'Incomplete' ? 'Incomplete evidence' : rate(cm?.executionCoverage), 'case-based', 'Executed Test Cases / Total Test Cases. This is not a case-level pass/fail outcome.', () => setDetails('cases'))}
        </div>{cm?.unclassifiedCases != null && cm.unclassifiedCases > 0 && <p className="evidence-warning">{cm.unclassifiedCases} cases have unclassified outcomes without a known executed point; executed-case/coverage totals are withheld.</p>}</section>
        <section aria-label="Execution Status"><h3>Execution Status <small>Current test points</small></h3><div className="compact-kpis">
            {([['Passed', m.passed, 'passed'], ['Failed', m.failed, 'failed'], ['Blocked', m.blocked, 'blocked'], ['Pending', m.pending, 'pending'], ['Not Applicable', m.notApplicable, 'notapplicable']] as const)
                .map(([label, value, status]) => card(label, value, 'test points', `${label} current test points. No case-level outcome is inferred.`, () => drill(status)))}
            {card('Executed Points', m.executed, 'test points', 'Terminal point statuses, including Blocked and N/A; not execution attempts.', () => drill('executed'))}
            {card('Point Execution Progress', rate(m.executionRate), 'point-based · applicable scope', 'Executed applicable points / applicable points; N/A excluded from numerator and denominator.', () => drill('applicable'))}
            {card('Primary Pass Rate', rate(m.primaryPassRate), 'point-based', 'Passed points / Executed points, including Blocked and N/A in Executed.', () => drill('executed'))}
            {card('Secondary Pass Rate', rate(m.secondaryPassRate), 'point-based', 'Passed / (Passed + Failed + Blocked) points; excludes N/A and pending.', () => drill('secondary'))}
        </div></section>
        <section aria-label="Quality and Retesting"><h3>Quality and Retesting</h3><div className="compact-kpis">
            {card('Total Defects', defectValue(q?.totalLinkedDefects), 'distinct scoped linked Bugs', 'Distinct Bug IDs from inspected test-case relationships and result associatedBugs; not project-wide bugs. Incomplete links withhold totals.', () => setDetails('defects'), phase === 'Loading')}
            {card('Open Defects', defectValue(q?.openLinkedDefects), 'scoped linked Bugs', 'Inspected state categories other than Completed/Removed. Unknown categories withhold this count.', () => setDetails('openDefects'), phase === 'Loading')}
            {card('Completed Defects', defectValue(q?.completedLinkedDefects), 'Completed state category', 'Scoped linked Bugs whose inspected category is Completed. Removed is a separate category.', () => setDetails('completedDefects'), phase === 'Loading')}
            {card('Re-executed Test Cases', historyValue, 'ADO terminology · distinct test points', 'Distinct test points with at least two qualifying recorded attempts in the selected reporting range, regardless of previous outcome.', () => setDetails('retests'), phase === 'Loading')}
            {card('Total Re-execution Attempts', additionalValue, 'additional executions · selected range', 'Sum of execution count minus one for each repeated point in the selected range. Missing evidence withholds totals.', () => setDetails('retests'), phase === 'Loading')}
            {card('Release Readiness', readiness, 'assessment · never release approval', 'Only explicitly enabled, approved server thresholds can produce readiness indicators. No automatic sign-off.', () => setDetails('readiness'), phase === 'Loading')}
        </div><p className="definition">Current outcomes and historical baseline activity use different evidence. “Verified” means source-evidence checks passed for retained accessible data; corporate reconciliation is still required.</p>
            {context.tester && <p className="definition">Tester scope: current case/point cards use assignment; historical cards use the actual executor. Defect totals cover the selected plan/subtree across all testers; daily Bug activity uses its recorded actor.</p>}
            {(message || problem) && <p role={phase === 'PermissionDenied' || phase === 'Unavailable' ? 'alert' : 'status'}>{message || problem}</p>}
            {phase !== 'Idle' && <button onClick={() => { setData(null); setPhase('Loading'); setRetry(n => n + 1); }}>Reload KPI evidence</button>}
            {activity?.evidence?.reasons.map(reason => <p className="evidence-warning" key={reason}>{reason}</p>)}
            {q?.evidence?.reasons.map(reason => <p className="evidence-warning" key={reason}>{reason}</p>)}
        </section>
        {details && <aside className="detail-panel" aria-label="KPI evidence"><button onClick={() => setDetails(null)}>Close KPI evidence ×</button>
            <h2>{details === 'retests' ? 'Re-executed Test Cases' : details.includes('Defects') || details === 'defects' ? 'Scoped linked defects' : details === 'readiness' ? 'Release Readiness' : 'Test-case coverage evidence'}</h2>
            {['cases', 'executedCases'].includes(details) && <><p>{cm?.missingCaseMappings ?? 'Unknown'} point-to-case mappings missing. No case-level pass/fail policy is inferred. Rows are known mapped evidence.</p>
                <DataTable name="case KPI evidence" rows={caseRows} columns={['caseId', 'title', 'pointIds', 'executed', 'outcomes'].map(key => ({ key, label: key }))} /></>}
            {['defects', 'openDefects', 'completedDefects'].includes(details) && <><p>{q?.schemaStatus ?? (message || 'Evidence not loaded.')} Counts are withheld when relationships or state categories are incomplete. Known records are shown below.</p>
                <DataTable name="defect KPI evidence" rows={defects.map(b => ({ ...b, createdByName: b.createdBy?.displayName ?? 'Unattributed', assignedName: b.assignedTo?.displayName,
                    points: [...new Set(b.relatedResults.map(r => r.pointId).filter(id => id != null))].join(', '), cases: b.linkedCaseIds?.join(', ') }))}
                    columns={['bugId', 'title', 'state', 'category', 'priority', 'severity', 'createdByName', 'assignedName', 'points', 'cases'].map(key => ({ key, label: key,
                        ...(key === 'bugId' ? { render: (row: DataRow) => snapshot.sourceBaseUrl ? <a href={`${snapshot.sourceBaseUrl}/_workitems/edit/${Number(row.bugId)}`} target="_blank" rel="noreferrer">{String(row.bugId)} ↗</a> : String(row.bugId) } : {}) }))} /></>}
            {details === 'retests' && <ReexecutionDetails report={activity?.reexecution} baseline={activity?.baselineReexecution} periodRepeats={activity?.repeatEvents} complete={activity?.complete} />}
            {details === 'readiness' && <><p>{readiness}</p>{message && <p>{message}</p>}<ul>{q?.riskReasons.map(r => <li key={r}>{r}</li>)}</ul><p>Thresholds remain disabled unless explicitly configured and approved. This dashboard never grants release approval.</p></>}
        </aside>}
    </div>;
}
