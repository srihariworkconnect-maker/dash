import { useEffect, useState } from 'react';
import { buildSuiteTree, type BackendClient, type PlanAnalytics, type WorkspaceEvidence } from '../services/adoApi';
import type { AdoTestPlan } from '../types/ado';
import { initialContext, changePlan, pointRows, suiteRows, matchesOutcome } from './ReportingContext';
import DataTable, { type Column, type DataRow } from '../components/DataTable';
import TestSuiteTree from '../components/TestSuiteTree';
import ActivityPanel from '../components/ActivityPanel';
import ReportPanel from '../components/ReportPanel';
import QualityPanel from '../components/QualityPanel';
import ComparisonPanel from '../components/ComparisonPanel';
import OperationsPanel from '../components/OperationsPanel';
import { TableExportContext } from '../components/TableExportContext';
import '../v2.css';

const modules = ['Overview', 'Test Explorer', 'Live TCM', 'Productivity Tracker', 'DSR Center', 'TCR Center', 'Execution Intelligence', 'Quality Intelligence'];
const secondary = ['Data Reconciliation', 'Audit History', 'Settings'];
const percent = (n: number | null | undefined) => n == null ? 'Unavailable' : `${n.toFixed(1)}%`;
const errorText = (error: unknown) => error instanceof Error ? error.message : 'Request unavailable';
const tcmColumns: Column[] = [
    ['pointId', 'Point ID'], ['caseId', 'Case ID'], ['title', 'Test case title'], ['suitePath', 'Suite path'],
    ['configuration', 'Configuration'], ['assigned', 'Assigned tester'], ['performer', 'Latest executed by'],
    ['outcome', 'Latest outcome'], ['latestDate', 'Latest execution date'], ['plan', 'Test plan'],
    ['executionCount', 'Execution count'], ['repeatCount', 'Repeat count'], ['bugIds', 'Linked bug IDs'],
    ['bugState', 'Bug state'], ['bugPriority', 'Bug severity / priority'], ['priority', 'Test priority'],
    ['requirement', 'Requirement links'], ['evidence', 'Evidence'], ['rawOutcome', 'Raw outcome'], ['outcomeType', 'JSON type'],
].map(([key, label]) => ({ key, label }));

export default function CommandCenter({ api }: { api: BackendClient }) {
    const [context, setContext] = useState(initialContext);
    const [module, setModule] = useState('Overview');
    const [collapsed, setCollapsed] = useState(false);
    const [plans, setPlans] = useState<AdoTestPlan[]>([]);
    const [snapshot, setSnapshot] = useState<PlanAnalytics | null>(null);
    const [catalog, setCatalog] = useState<PlanAnalytics | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [refresh, setRefresh] = useState(0);
    const [planSearch, setPlanSearch] = useState('');
    const [suiteSearch, setSuiteSearch] = useState('');
    const [testerSearch, setTesterSearch] = useState('');
    const [statusFilter, setStatusFilter] = useState('');
    const [detail, setDetail] = useState<DataRow | null>(null);
    const [evidence, setEvidence] = useState<{ scope: string; value: WorkspaceEvidence } | null>(null);
    const [cutoff, setCutoff] = useState<string | null>(null);
    useEffect(() => {
        const c = new AbortController();
        void api.reportingSettings(c.signal).then(s => { if (!c.signal.aborted) setCutoff(s.dsrCutoff); })
            .catch(() => { /* The source request surfaces connection errors; never guess configured cutoff. */ });
        return () => c.abort();
    }, [api]);
    useEffect(() => {
        const controller = new AbortController();
        void api.plans(controller.signal).then(data => {
            if (controller.signal.aborted) return;
            setPlans(data.value);
            setContext(current => data.value.some(p => p.id === current.planId) ? current : changePlan(current, data.value[0]?.id ?? 0));
        }).catch(cause => { if (!controller.signal.aborted) setError(errorText(cause)); });
        return () => controller.abort();
    }, [api, refresh]);
    useEffect(() => {
        if (!context.planId) return;
        const controller = new AbortController();
        void api.analytics(context.planId, context.suiteId, controller.signal, context.tester).then(data => {
            if (controller.signal.aborted) return;
            setSnapshot(data); setError(null);
        }).catch(cause => { if (!controller.signal.aborted) setError(errorText(cause)); });
        return () => controller.abort();
    }, [api, context.planId, context.suiteId, context.tester, refresh]);
    useEffect(() => {
        if (!context.planId) return;
        const c = new AbortController();
        void api.analytics(context.planId, null, c.signal).then(data => { if (!c.signal.aborted) setCatalog(data); })
            .catch(cause => { if (!c.signal.aborted) setError(errorText(cause)); });
        return () => c.abort();
    }, [api, context.planId, refresh]);
    useEffect(() => {
        if (context.preset === 'Custom Range' || context.preset === 'Release-to-Date') return;
        const controller = new AbortController();
        void api.period(context.preset, controller.signal).then(window => {
            if (!controller.signal.aborted) setContext(c => ({ ...c, ...window }));
        }).catch(cause => { if (!controller.signal.aborted) setError(errorText(cause)); });
        return () => controller.abort();
    }, [api, context.preset]);
    useEffect(() => {
        const handler = () => { void api.refresh(new AbortController().signal).then(() => { setSnapshot(null); setEvidence(null); setRefresh(r => r + 1); }).catch(e => setError(errorText(e))); };
        window.addEventListener('ado-widget-refresh', handler);
        return () => window.removeEventListener('ado-widget-refresh', handler);
    }, [api]);
    const selectedPlan = plans.find(p => p.id === context.planId);
    const testers = new Map((catalog?.points ?? []).flatMap(p => [p.assignedTester, p.latestExecutedBy]).filter(t => t != null).map(t => [t.key, t]));
    for (const event of evidence?.value.activity.events ?? []) if (event.performer) testers.set(event.performer.key, event.performer);
    const m = snapshot?.metrics;
    const scopedEvidence = evidence?.scope === JSON.stringify(context) ? evidence.value : null;
    const rows = pointRows(snapshot?.points ?? [], selectedPlan?.name ?? '').map(row => {
        if (!scopedEvidence) return row;
        const activity = scopedEvidence.activity.points.find(p => p.pointId === row.pointId);
        const testCase = scopedEvidence.quality.cases.find(c => c.caseId === row.caseId);
        const defects = scopedEvidence.quality.defects.filter(b => b.relatedResults.some(r => r.pointId === row.pointId && r.bugIds.includes(b.bugId)));
        return { ...row, executionCount: scopedEvidence.activity.complete ? activity?.executionCount ?? 0 : null,
            repeatCount: scopedEvidence.activity.complete ? activity?.repeatCount ?? 0 : null,
            bugIds: scopedEvidence.quality.relationshipsComplete ? defects.map(b => b.bugId).join(', ') || 'None linked' : null,
            bugState: defects.map(b => `${b.bugId}: ${b.state}`).join('; ') || null,
            bugPriority: defects.map(b => `${b.bugId}: ${b.severity ?? 'Unavailable'} / ${b.priority ?? 'Unavailable'}`).join('; ') || null,
            priority: testCase?.priority ?? null, requirement: testCase?.requirementIds.join(', ') || null,
            evidence: testCase?.evidenceLinks.join('; ') || null };
    });
    const displayed = rows.filter(row => matchesOutcome(row.outcome, statusFilter));
    const linkedColumns = tcmColumns.map(c => c.key === 'caseId' ? { ...c, render: (row: DataRow) =>
        row.caseId && snapshot?.sourceBaseUrl ? <a href={`${snapshot.sourceBaseUrl}/_workitems/edit/${row.caseId}`} target="_blank" rel="noreferrer">{String(row.caseId)} ↗</a> : 'Unavailable' } : c);
    const invalidate = () => { setSnapshot(null); setDetail(null); };
    const drill = (status: string) => { setStatusFilter(status); setModule('Live TCM'); };
    const changeModule = (name: string) => { setModule(name); setDetail(null); setStatusFilter(''); };
    return <TableExportContext.Provider value={api.exportTable}><div className={`command-center ${collapsed ? 'nav-collapsed' : ''}`}>
        <aside className="cc-nav"><button aria-label="Toggle navigation" className="nav-toggle" onClick={() => setCollapsed(!collapsed)}>☰</button>
            <div className="cc-brand"><b>S</b>{!collapsed && <span>Sandpiper<small>QA Command Center V2</small></span>}</div>
            <nav aria-label="Main navigation">{[...modules, ...secondary].map((name, i) => <button key={name} title={name} className={module === name ? 'selected' : ''}
                onClick={() => changeModule(name)}><span>{String(i + 1).padStart(2, '0')}</span>{!collapsed && name}</button>)}</nav>
            {!collapsed && <p className="nav-foot">ADO is the source of truth<br />Live data · no demo fallback</p>}</aside>
        <div className="cc-body"><header className="global-toolbar"><div className="scope-heading"><strong>{module}</strong>
            <span>{selectedPlan?.name ?? 'Select plan'} / {context.suiteId == null ? 'All suites' : catalog?.suites.find(s => s.suiteId === context.suiteId)?.suiteName} / {context.tester ? 'Selected tester' : 'All testers'}</span></div>
            <div className="global-filters"><label>Test plan<input aria-label="Search test plans" placeholder="Search plans" value={planSearch} onChange={e => setPlanSearch(e.target.value)} />
                <select aria-label="Test plan" value={context.planId ?? ''} onChange={e => { invalidate(); setCatalog(null); setContext(changePlan(context, Number(e.target.value))); }}>
                    {!plans.length && <option value="">No plans available</option>}{plans.filter(p => p.name.toLowerCase().includes(planSearch.toLowerCase()) || p.id === context.planId).map(p => <option key={p.id} value={p.id}>{p.name}</option>)}</select></label>
                <label>Suite subtree<input aria-label="Search suites" placeholder="Search hierarchy" value={suiteSearch} onChange={e => setSuiteSearch(e.target.value)} />
                    <select aria-label="Suite subtree" value={context.suiteId ?? ''} onChange={e => { invalidate(); setContext({ ...context, suiteId: e.target.value ? Number(e.target.value) : null }); }}>
                        <option value="">All suites</option>{catalog?.suites.filter(s => s.suiteName.toLowerCase().includes(suiteSearch.toLowerCase()) || s.suiteId === context.suiteId).map(s => <option key={s.suiteId} value={s.suiteId}>{s.suiteName} · {s.suiteId}</option>)}</select></label>
                <label>Tester<input aria-label="Search testers" placeholder="Search identities" value={testerSearch} onChange={e => setTesterSearch(e.target.value)} />
                    <select aria-label="Tester" value={context.tester} onChange={e => { invalidate(); setContext({ ...context, tester: e.target.value }); }}>
                        <option value="">All testers / unattributed</option><option value="unattributed">Unassigned / unattributed</option>{[...testers.values()].filter(t => t.displayName.toLowerCase().includes(testerSearch.toLowerCase()) || t.key === context.tester).map(t => <option key={t.key} value={t.key}>{t.displayName}</option>)}</select></label>
                <label>Reporting period<select value={context.preset} onChange={e => setContext({ ...context, preset: e.target.value,
                    ...(e.target.value === 'Release-to-Date' ? { from: context.baselineFrom, to: new Date().toISOString() } : {}) })}>
                    {['Today', 'Yesterday', 'Last 7 Days', 'Custom Range', 'Release-to-Date'].map(p => <option key={p}>{p}</option>)}</select>
                    <small>{cutoff ?? 'Configured'} UK cutoff · Europe/London</small></label>
                <button className="primary-button" onClick={() => { void api.refresh(new AbortController().signal).then(() => { invalidate(); setEvidence(null); setRefresh(r => r + 1); }).catch(e => setError(errorText(e))); }}>↻ Refresh</button></div>
            <div className="period-fields"><label>From (inclusive, ISO offset)<input value={context.from} onChange={e => setContext({ ...context, preset: 'Custom Range', from: e.target.value })} /></label>
                <label>To (exclusive, ISO offset)<input value={context.to} onChange={e => setContext({ ...context, preset: 'Custom Range', to: e.target.value })} /></label>
                <label>Repeat baseline start<input value={context.baselineFrom} placeholder="Explicit baseline, ISO offset" onChange={e => setContext({ ...context, baselineFrom: e.target.value })} /></label></div>
        </header>
        <main className="cc-content"><div className="trust-banner" role="status">Awaiting corporate data reconciliation. Current metrics are provisional; period activity is separate from latest point state.
            {snapshot && <small>Collected {new Date(snapshot.retrievedAt).toLocaleString('en-GB', { timeZone: 'Europe/London' })} UK · {snapshot.rulesVersion} · {snapshot.suiteCount} suites · cache age at response {Math.round(snapshot.cacheAgeSeconds ?? 0)}s</small>}</div>
            {error && <p role="alert" className="error">{error} <button onClick={() => setRefresh(r => r + 1)}>Retry</button></p>}
            {!snapshot && !error && <div className="empty-state" role="status">Loading authorized Azure DevOps scope…</div>}
            {snapshot && m && module === 'Overview' && <>
                <h2>Current execution snapshot</h2><div className="compact-kpis">{[
                    ['Total test points', m.total, ''], ['Passed', m.passed, 'passed'], ['Failed', m.failed, 'failed'], ['Blocked', m.blocked, 'blocked'], ['Pending', m.pending, 'pending'], ['Not applicable', m.notApplicable, 'notapplicable'],
                    ['Executed', m.executed, 'executed'], ['Execution', percent(m.executionRate), 'applicable'], ['Primary pass', percent(m.primaryPassRate), 'executed'], ['Secondary pass', percent(m.secondaryPassRate), 'secondary'],
                    ['Re-executed points', scopedEvidence?.activity.reExecutedPoints ?? 'Load history', ''], ['Open defects', scopedEvidence?.quality.openLinkedDefects ?? 'Awaiting evidence', ''], ['Release readiness', scopedEvidence?.quality.releaseReadiness ?? 'Not assessed', ''],
                ].map(([label, value, status]) => <button className={`compact-kpi status-${status}`} key={label} onClick={() => label === 'Open defects' || label === 'Release readiness' ? changeModule('Quality Intelligence') : label === 'Re-executed points' ? changeModule('Execution Intelligence') : drill(String(status))}>
                    <small>{label}</small><b>{value}</b><span>View evidence →</span></button>)}</div>
                <div className="panel-grid"><section className="cc-panel"><h3>Outcome distribution · current state</h3>{Object.entries(m.statuses).filter(([, count]) => count > 0).map(([status, count]) => <button className="distribution-row" key={status} onClick={() => drill(status)}>
                    <span>{status}</span><span className="bar-track"><span style={{ width: `${m.total ? count / m.total * 100 : 0}%` }} /></span><b>{count}</b></button>)}</section>
                    <section className="cc-panel"><h3>Suite progress · own and subtree</h3><DataTable name="suite progress" rows={suiteRows(snapshot)} columns={[
                        { key: 'name', label: 'Suite' }, { key: 'own', label: 'Own points' }, { key: 'subtree', label: 'Subtree points' }, { key: 'blocked', label: 'Blocked' }, { key: 'progress', label: 'Progress %' },
                    ]} onSelect={row => { invalidate(); setContext({ ...context, suiteId: Number(row.key) }); }} /></section></div>
                <p className="definition">Primary = Passed / Executed (includes blocked and N/A). Secondary = Passed / (Passed + Failed + Blocked). Progress excludes N/A from applicable scope. Unknown outcomes are not silently executed.</p>
                <QualityPanel key={`overview:${JSON.stringify(context)}`} api={api} context={context} compact onLoaded={value => setEvidence({ scope: JSON.stringify(context), value })} />
                {scopedEvidence && <><h3>Period activity · separate from current outcomes</h3>
                    <DataTable name="overview daily trends" rows={scopedEvidence.activity.daily.map(d => ({ ...d }))}
                        columns={['day', 'executionEvents', 'uniquePoints', 'repeatEvents', 'passed', 'failed', 'blocked'].map(key => ({ key, label: key }))} onSelect={() => changeModule('Execution Intelligence')} />
                    <h3>Tester activity summary</h3><DataTable name="overview tester activity" rows={scopedEvidence.activity.productivity.map(p => ({ ...p }))}
                        columns={['tester', 'day', 'executionEvents', 'uniquePoints', 'repeatEvents'].map(key => ({ key, label: key }))} onSelect={() => changeModule('Productivity Tracker')} />
                    <h3>Defect distribution · result-linked scope</h3>{[...new Set(scopedEvidence.quality.defects.map(b => b.state ?? 'Unknown'))].map(state => <button className="distribution-row" key={state} onClick={() => changeModule('Quality Intelligence')}>
                        <span>{state}</span><b>{scopedEvidence.quality.defects.filter(b => (b.state ?? 'Unknown') === state).length}</b></button>)}
                    <h3>Risk summary</h3><ul>{scopedEvidence.quality.riskReasons.map(reason => <li key={reason}>{reason}</li>)}</ul></>}
            </>}
            {snapshot && module === 'Test Explorer' && <div className="panel-grid"><section className="cc-panel"><h2>Suite hierarchy</h2><TestSuiteTree suites={buildSuiteTree(snapshot.suites)} /></section>
                <section className="cc-panel"><h2>Own-suite and subtree counts</h2><DataTable name="explorer suites" rows={suiteRows(snapshot)} columns={[
                    { key: 'id', label: 'Suite ID' }, { key: 'name', label: 'Suite' }, { key: 'own', label: 'Own points' }, { key: 'subtree', label: 'Subtree points' }, { key: 'progress', label: 'Progress %' },
                ]} onSelect={row => { invalidate(); setContext({ ...context, suiteId: Number(row.id) }); }} /></section><section className="cc-panel full-width"><h2>Test cases in selected scope</h2>
                    <DataTable name="explorer cases" rows={rows} columns={linkedColumns} onSelect={setDetail} /></section></div>}
            {snapshot && module === 'Live TCM' && <section className="cc-panel"><div className="section-title"><h2>Live Test Case Matrix</h2><span>Read-only ADO source records</span></div>
                <QualityPanel key={`enrichment:${JSON.stringify(context)}`} api={api} context={context} compact onLoaded={value => setEvidence({ scope: JSON.stringify(context), value })} />
                {statusFilter && <p>Outcome filter: {statusFilter} <button onClick={() => setStatusFilter('')}>Clear</button></p>}
                <p className="definition">Point identity includes configuration and suite membership. Latest outcomes do not represent period execution counts. Unavailable enrichment is never shown as zero.</p>
                <DataTable name="Live TCM" rows={displayed} columns={linkedColumns} onSelect={setDetail} /></section>}
            {snapshot && module === 'Data Reconciliation' && <section className="cc-panel"><h2>Source reconciliation</h2><p>{snapshot.reconciliation?.validationState}</p>
                <DataTable name="reconciliation" rows={rows} columns={[{ key: 'pointId', label: 'Point' }, { key: 'rawOutcome', label: 'Raw outcome' }, { key: 'outcomeType', label: 'JSON type' }, { key: 'outcome', label: 'Normalized outcome' }]} />
                <OperationsPanel key={JSON.stringify(context)} api={api} mode={module} snapshot={snapshot} context={context} /></section>}
            {['Settings', 'Audit History'].includes(module) && <OperationsPanel key={module} api={api} mode={module} snapshot={snapshot} context={context} />}
            {snapshot && ['Productivity Tracker', 'Execution Intelligence'].includes(module) && <ActivityPanel key={JSON.stringify(context)} api={api} context={context} snapshot={catalog ?? snapshot} assignmentSnapshot={snapshot} mode={module} />}
            {snapshot && ['DSR Center', 'TCR Center'].includes(module) && <ReportPanel key={`${module}:${JSON.stringify(context)}`} api={api} context={context} kind={module === 'DSR Center' ? 'DSR' : 'TCR'} />}
            {snapshot && module === 'Quality Intelligence' && <><QualityPanel key={`quality:${JSON.stringify(context)}`} api={api} context={context} onLoaded={value => setEvidence({ scope: JSON.stringify(context), value })} />
                <section className="cc-panel"><h2>Blocked suites / current risk hotspots</h2><DataTable name="blocked suites" rows={suiteRows(snapshot).filter(s => (s.blocked ?? 0) > 0)}
                    columns={['name', 'own', 'subtree', 'blocked', 'progress'].map(key => ({ key, label: key }))} /></section>
                <ComparisonPanel api={api} plans={plans} planId={context.planId!} /></>}
            {detail && <aside className="detail-panel" aria-label="Record details"><button onClick={() => setDetail(null)}>Close details ×</button><h2>Source record</h2>
                {Object.entries(detail).map(([key, value]) => <div className="stat-row" key={key}><span>{key}</span><strong>{value == null ? 'Unavailable' : String(value)}</strong></div>)}
                {snapshot?.sourceBaseUrl && detail.caseId != null && <p><a href={`${snapshot.sourceBaseUrl}/_workitems/edit/${Number(detail.caseId)}`} target="_blank" rel="noreferrer">Open test case in ADO ↗</a></p>}
                {scopedEvidence ? <><h3>Recorded result history in period</h3><DataTable name="point execution details"
                    rows={scopedEvidence.activity.events.filter(e => e.pointId === detail.pointId).map(e => ({ ...e, performer: e.performer?.displayName ?? 'Unattributed' }))}
                    columns={['key', 'outcome', 'completedAt', 'performer', 'transition'].map(key => ({ key, label: key }))} />
                    <h3>Result-linked bugs</h3>{scopedEvidence.quality.defects.filter(b => b.relatedResults.some(e => e.pointId === detail.pointId)).map(b => <p key={b.bugId}>
                        <a href={`${snapshot?.sourceBaseUrl}/_workitems/edit/${b.bugId}`} target="_blank" rel="noreferrer">{b.bugId}: {b.title}</a> · {b.state} · {b.verificationStatus}</p>)}
                    <h3>Attachments</h3>{scopedEvidence.quality.cases.find(c => c.caseId === detail.caseId)?.evidenceLinks.map((url, i) => <p key={i}><a href={url} target="_blank" rel="noreferrer">ADO evidence {i + 1} ↗</a></p>)}</>
                    : <p>Load TCM source enrichment with a baseline to inspect execution history, bugs and evidence.</p>}</aside>}
        </main></div></div></TableExportContext.Provider>;
}
