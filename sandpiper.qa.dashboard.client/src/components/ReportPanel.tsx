import { useEffect, useState } from 'react';
import type { BackendClient, ReportSnapshot } from '../services/adoApi';
import type { ReportingContext } from '../app/ReportingContext';
import DataTable from './DataTable';

export default function ReportPanel({ api, context, kind }: { api: BackendClient; context: ReportingContext; kind: 'DSR' | 'TCR' }) {
    const [narrative, setNarrative] = useState<Record<string, string>>({ Objectives: '', Exclusions: '', 'Carry-forward items': '', 'Outstanding risks': '', 'Sign-off recommendation': 'Human review required; no automatic approval.' });
    const [trigger, setTrigger] = useState(0);
    const [report, setReport] = useState<ReportSnapshot | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [preservedId, setPreservedId] = useState('');
    const [saved, setSaved] = useState<{ id: string; kind: string; createdAt: string; status: string; planId: number }[]>([]);
    const [historyRefresh, setHistoryRefresh] = useState(0);
    useEffect(() => {
        const c = new AbortController();
        void api.reports(c.signal).then(rows => { if (!c.signal.aborted) setSaved(rows.filter(r => r.kind === kind && r.planId === context.planId)); })
            .catch(e => { if (!c.signal.aborted) setError(e instanceof Error ? e.message : 'Saved reports unavailable'); });
        return () => c.abort();
    }, [api, kind, context.planId, historyRefresh]);
    useEffect(() => {
        if (!preservedId) return;
        const c = new AbortController();
        void api.report(preservedId, c.signal).then(r => {
            if (r.id !== preservedId || r.kind !== kind || r.currentSnapshot.planId !== context.planId) throw new Error('Saved evidence returned a mismatched report scope.');
            if (!c.signal.aborted) setReport(r);
        })
            .catch(e => { if (!c.signal.aborted) setError(e instanceof Error ? e.message : 'Saved evidence unavailable'); });
        return () => c.abort();
    }, [api, preservedId, kind, context.planId]);
    // Freeze narrative at the explicit generation action; typing never triggers ADO reads.
    const [submitted, setSubmitted] = useState(narrative);
    useEffect(() => {
        if (!trigger || preservedId) return;
        const controller = new AbortController();
        void api.createReport({ ...context, kind, narrative: submitted }, controller.signal).then(data => {
            if (!controller.signal.aborted) { setReport(data); setHistoryRefresh(r => r + 1); }
        }).catch(cause => { if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : 'Report unavailable'); });
        return () => controller.abort();
    }, [api, context, kind, trigger, submitted, preservedId]);
    const download = async (format: string) => {
        if (!report) return;
        try {
            const blob = await api.exportReport(report.id, format, new AbortController().signal);
            const url = URL.createObjectURL(blob); const a = document.createElement('a');
            a.href = url; a.download = `Sandpiper-${kind}-${report.id}.${format}`; a.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
        } catch (cause) { setError(cause instanceof Error ? cause.message : 'Export unavailable'); }
    };
    const summary = report ? `${report.kind} — ${report.status}\nPlan ${report.currentSnapshot.planId}; scope ${report.currentSnapshot.suiteId ?? 'all suites'}\n${report.activity.window.from} to ${report.activity.window.to} (exclusive)\nCurrent collection: ${report.currentSnapshot.metrics.total} points; ${report.currentSnapshot.metrics.passed} passed, ${report.currentSnapshot.metrics.failed} failed, ${report.currentSnapshot.metrics.blocked} blocked.\nPeriod: ${report.activity.events.filter(e => e.qualifies).length} known qualifying records; ${report.activity.complete ? report.activity.repeatEvents : 'Incomplete'} repeat events.\nHistorical cutoff snapshot: unavailable. Bug/verification metrics require supporting evidence.\nEvidence ID ${report.id}; calculation ${report.calculationVersion}; checksum ${report.checksum}` : '';
    return <section className="cc-panel report-preview"><div className="section-title"><h2>{kind === 'DSR' ? 'Daily Status Report' : 'Test Completion Report'}</h2>
        <button className="primary-button" disabled={!context.planId || !context.from || !context.to || !context.baselineFrom} onClick={() => {
            setPreservedId(''); setError(null); setReport(null); setSubmitted({ ...narrative }); setTrigger(n => n + 1);
        }}>Generate evidence draft</button></div>
        <p className="definition">Uses the shared snapshot, execution and productivity engine. Provide explicit dates and a repeat baseline.
            Current state is not a historical cutoff snapshot. Every generated dataset is frozen; exports do not requery ADO.</p>
        <label>Preserved {kind} evidence<select value={preservedId} onChange={e => { setError(null); setReport(null); setPreservedId(e.target.value); }}>
            <option value="">Select a saved evidence snapshot</option>{saved.map(r => <option key={r.id} value={r.id}>{r.createdAt} · {r.status} · {r.id}</option>)}</select></label>
        <p>Saved evidence uses its original dates and filters, independently of the current toolbar. Import older evidence JSON through Audit History. No past state is inferred from current outcomes.</p>
        <details><summary>Report narrative and exclusions</summary><div className="report-narrative">{Object.entries(narrative).map(([key, value]) =>
            <label key={key}>{key}<textarea maxLength={4000} value={value} onChange={e => setNarrative({ ...narrative, [key]: e.target.value })} /></label>)}</div></details>
        {(trigger > 0 || preservedId) && !report && !error && <p role="status">{preservedId ? 'Opening preserved evidence…' : 'Collecting and freezing report evidence…'}</p>}
        {error && <p className="error" role="alert">{error}</p>}
        {report && <><div className="trust-banner">{report.status}<small>Snapshot {report.id} · Generated {report.createdAt} · {report.calculationVersion}</small></div>
            {preservedId && <p className="evidence-warning">Viewing preserved point state collected at {report.currentSnapshot.retrievedAt}. This preserves the recorded collection, not an independently reconstructed state at the requested cutoff.</p>}
            <div className="table-tools"><button onClick={() => void download('xlsx')}>Download Excel</button><button onClick={() => void download('pdf')}>Download PDF</button>
                <button onClick={() => void download('json')}>Preserve evidence bundle</button><button onClick={() => window.print()}>Print preview</button></div>
            <h3>Scope and collection metadata</h3><p>Plan {report.currentSnapshot.planId}; suite {report.currentSnapshot.suiteId ?? 'all'}.
                Period [{report.activity.window.from}, {report.activity.window.to}). UK timezone: Europe/London.</p>
            {report.scope && <p>Category: {report.scope.category ?? 'All'}; current outcome: {report.scope.outcome ?? 'All'}; baseline: {report.activity.baseline.from}.
                {' '}Current state collected: {report.scope.currentStateCollectedAt}. Historical as-of: {report.scope.historicalAsOfStatus}. {report.scope.membershipBasis}.</p>}
            <h3>Latest collected outcomes — not historical cutoff</h3><DataTable name={`${kind} snapshot`} rows={Object.entries(report.currentSnapshot.metrics.statuses).map(([status, count]) => ({ status, count }))}
                columns={[{ key: 'status', label: 'Point outcome' }, { key: 'count', label: 'Test points' }]} />
            <h3>Case-based coverage · latest collection</h3><p>Total Test Cases: {report.currentSnapshot.caseMetrics?.totalTestCases ?? 'Unavailable / incomplete'};
                {' '}Executed Test Cases: {report.currentSnapshot.caseMetrics?.executedTestCases ?? 'Unavailable / incomplete'}. Execution Coverage (case-based):
                {' '}{report.currentSnapshot.caseMetrics?.executionCoverage == null ? 'Unavailable' : `${report.currentSnapshot.caseMetrics.executionCoverage.toFixed(1)}%`}.</p>
            <p>Distinct re-executed case IDs in baseline: {report.activity.caseRetesting?.reExecutedTestCases ?? 'Unavailable / incomplete'}. Total scoped linked defects: {report.quality?.totalLinkedDefects ?? 'Unavailable / incomplete'}.</p>
            <h3>Daily activity and progress trend</h3><DataTable name={`${kind} daily`} rows={report.activity.daily.map(r => ({ ...r }))}
                columns={['day', 'executionEvents', 'uniquePoints', 'repeatEvents', 'passed', 'failed', 'blocked'].map(key => ({ key, label: key }))} />
            <h3>Tester productivity</h3><DataTable name={`${kind} testers`} rows={report.activity.productivity.map(r => ({ ...r }))}
                columns={['tester', 'day', 'executionEvents', 'uniquePoints', 'passed', 'failed', 'blocked', 'repeatEvents'].map(key => ({ key, label: key }))} />
            <h3>Re-execution and regression evidence</h3><p>{report.activity.reExecutedPoints ?? 'Incomplete'} baseline re-executed points;
                {' '}{report.activity.complete ? report.activity.repeatEvents : 'Incomplete'} period repeat events. Transitions are preserved in the execution worksheet.</p>
            <h3>Blocked testing and carry-forward</h3><p>{report.currentSnapshot.metrics.blocked} currently blocked points (not historical blockers).
                {' '}{report.narrative['Carry-forward items'] || 'No narrative supplied.'}</p>
            <h3>Defects, coverage and outstanding risks</h3><p>Bug activity, requirement coverage and confirmed verification require additional source evidence.
                Their absence is not a zero or an approval. {report.narrative['Outstanding risks']}</p>
            {report.quality && <><p className="trust-banner">{report.quality.schemaStatus}</p><p>Open result-linked defects: {report.quality.openLinkedDefects ?? 'Unavailable'}.
                Requirement-link gaps: {report.quality.requirementGaps ?? 'Unavailable'}. {report.quality.releaseReadiness}</p>
                <DataTable name={`${kind} defects`} rows={report.quality.defects.map(b => ({ ...b, key: b.bugId, evidence: b.verificationEvidence.join('; ') }))}
                    columns={['bugId', 'title', 'state', 'severity', 'priority', 'verificationStatus', 'retestedAt', 'verificationOutcome', 'evidence'].map(key => ({ key, label: key }))} />
                <DataTable name={`${kind} bug activity`} rows={report.quality.bugActivity.map(b => ({ ...b }))}
                    columns={['tester', 'day', 'raised', 'resolved', 'reopened', 'verifiedWithEvidence', 'verificationAttempts'].map(key => ({ key, label: key }))} />
            </>}
            <h3>Exclusions and recommendation</h3><p>{report.narrative.Exclusions || 'No exclusions supplied.'}</p><p>{report.narrative['Sign-off recommendation']}</p>
            <details><summary>Data limitations</summary><ul>{report.limitations.map(text => <li key={text}>{text}</li>)}</ul></details>
            <h3>Email-ready summary — draft only</h3><textarea className="email-summary" readOnly value={summary} aria-label="Email-ready report summary" />
            <button onClick={() => void navigator.clipboard.writeText(summary).catch(() => setError('Clipboard unavailable. Select and copy the summary manually.'))}>Copy summary</button>
            <p className="definition">No email is sent. Store downloaded evidence only in an approved corporate location; it contains test data and identities.</p>
        </>}
    </section>;
}
