import { useEffect, useState } from 'react';
import { buildSuiteTree, type BackendClient, type PlanAnalytics } from '../services/adoApi';
import type { AdoTestPlan } from '../types/ado';
import { initialContext, changePlan, pointRows, suiteRows } from './ReportingContext';
import DataTable, { type Column, type DataRow } from '../components/DataTable';
import TestSuiteTree from '../components/TestSuiteTree';
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
            if (context.suiteId == null && !context.tester) setCatalog(data);
        }).catch(cause => { if (!controller.signal.aborted) setError(errorText(cause)); });
        return () => controller.abort();
    }, [api, context.planId, context.suiteId, context.tester, refresh]);
    useEffect(() => {
        if (context.preset === 'Custom Range' || context.preset === 'Release-to-Date') return;
        const controller = new AbortController();
        void api.period(context.preset, controller.signal).then(window => {
            if (!controller.signal.aborted) setContext(c => ({ ...c, ...window }));
        }).catch(cause => { if (!controller.signal.aborted) setError(errorText(cause)); });
        return () => controller.abort();
    }, [api, context.preset]);
    useEffect(() => {
        const handler = () => { setSnapshot(null); setRefresh(r => r + 1); };
        window.addEventListener('ado-widget-refresh', handler);
        return () => window.removeEventListener('ado-widget-refresh', handler);
    }, []);
    const selectedPlan = plans.find(p => p.id === context.planId);
    const testers = new Map((catalog?.points ?? []).filter(p => p.assignedTester).map(p => [p.assignedTester!.key, p.assignedTester!]));
    const m = snapshot?.metrics;
    const rows = pointRows(snapshot?.points ?? [], selectedPlan?.name ?? '');
    const displayed = rows.filter(row => !statusFilter || row.outcome.toLowerCase() === statusFilter);
    const linkedColumns = tcmColumns.map(c => c.key === 'caseId' ? { ...c, render: (row: DataRow) =>
        row.caseId && snapshot?.sourceBaseUrl ? <a href={`${snapshot.sourceBaseUrl}/_workitems/edit/${row.caseId}`} target="_blank" rel="noreferrer">{String(row.caseId)} ↗</a> : 'Unavailable' } : c);
    const invalidate = () => { setSnapshot(null); setDetail(null); };
    const drill = (status: string) => { setStatusFilter(status); setModule('Live TCM'); };
    const changeModule = (name: string) => { setModule(name); setDetail(null); setStatusFilter(''); };
    return <div className={`command-center ${collapsed ? 'nav-collapsed' : ''}`}>
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
                        <option value="">All testers / unattributed</option>{[...testers.values()].filter(t => t.displayName.toLowerCase().includes(testerSearch.toLowerCase()) || t.key === context.tester).map(t => <option key={t.key} value={t.key}>{t.displayName}</option>)}</select></label>
                <label>Reporting period<select value={context.preset} onChange={e => setContext({ ...context, preset: e.target.value })}>
                    {['Today', 'Yesterday', 'Last 7 Days', 'Custom Range', 'Release-to-Date'].map(p => <option key={p}>{p}</option>)}</select>
                    <small>18:00 UK cutoff · Europe/London</small></label>
                <button className="primary-button" onClick={() => { invalidate(); setRefresh(r => r + 1); }}>↻ Refresh</button></div>
            <div className="period-fields"><label>From (inclusive, ISO offset)<input value={context.from} onChange={e => setContext({ ...context, preset: 'Custom Range', from: e.target.value })} /></label>
                <label>To (exclusive, ISO offset)<input value={context.to} onChange={e => setContext({ ...context, preset: 'Custom Range', to: e.target.value })} /></label>
                <label>Repeat baseline start<input value={context.baselineFrom} placeholder="Explicit baseline, ISO offset" onChange={e => setContext({ ...context, baselineFrom: e.target.value })} /></label></div>
        </header>
        <main className="cc-content"><div className="trust-banner" role="status">Awaiting corporate data reconciliation. Current metrics are provisional; period activity is separate from latest point state.
            {snapshot && <small>Collected {new Date(snapshot.retrievedAt).toLocaleString('en-GB', { timeZone: 'Europe/London' })} UK · {snapshot.rulesVersion} · {snapshot.suiteCount} suites</small>}</div>
            {error && <p role="alert" className="error">{error} <button onClick={() => setRefresh(r => r + 1)}>Retry</button></p>}
            {!snapshot && !error && <div className="empty-state" role="status">Loading authorized Azure DevOps scope…</div>}
            {snapshot && m && module === 'Overview' && <>
                <h2>Current execution snapshot</h2><div className="compact-kpis">{[
                    ['Total test points', m.total, ''], ['Passed', m.passed, 'passed'], ['Failed', m.failed, 'failed'], ['Blocked', m.blocked, 'blocked'], ['Pending', m.pending, 'notexecuted'], ['Not applicable', m.notApplicable, 'notapplicable'],
                    ['Executed', m.executed, ''], ['Execution', percent(m.executionRate), ''], ['Primary pass', percent(m.primaryPassRate), ''], ['Secondary pass', percent(m.secondaryPassRate), ''],
                    ['Re-executed points', 'Load history', ''], ['Open defects', 'Awaiting evidence', ''], ['Release readiness', 'Not assessed', ''],
                ].map(([label, value, status]) => <button className={`compact-kpi status-${status}`} key={label} onClick={() => label === 'Open defects' || label === 'Release readiness' ? changeModule('Quality Intelligence') : label === 'Re-executed points' ? changeModule('Execution Intelligence') : drill(String(status))}>
                    <small>{label}</small><b>{value}</b><span>View evidence →</span></button>)}</div>
                <div className="panel-grid"><section className="cc-panel"><h3>Outcome distribution · current state</h3>{Object.entries(m.statuses).filter(([, count]) => count > 0).map(([status, count]) => <button className="distribution-row" key={status} onClick={() => drill(status)}>
                    <span>{status}</span><span className="bar-track"><span style={{ width: `${m.total ? count / m.total * 100 : 0}%` }} /></span><b>{count}</b></button>)}</section>
                    <section className="cc-panel"><h3>Suite progress · own and subtree</h3><DataTable name="suite progress" rows={suiteRows(snapshot)} columns={[
                        { key: 'name', label: 'Suite' }, { key: 'own', label: 'Own points' }, { key: 'subtree', label: 'Subtree points' }, { key: 'blocked', label: 'Blocked' }, { key: 'progress', label: 'Progress %' },
                    ]} onSelect={row => { invalidate(); setContext({ ...context, suiteId: Number(row.key) }); }} /></section></div>
                <p className="definition">Primary = Passed / Executed (includes blocked and N/A). Secondary = Passed / (Passed + Failed + Blocked). Progress excludes N/A from applicable scope. Unknown outcomes are not silently executed.</p>
            </>}
            {snapshot && module === 'Test Explorer' && <div className="panel-grid"><section className="cc-panel"><h2>Suite hierarchy</h2><TestSuiteTree suites={buildSuiteTree(snapshot.suites)} /></section>
                <section className="cc-panel"><h2>Own-suite and subtree counts</h2><DataTable name="explorer suites" rows={suiteRows(snapshot)} columns={[
                    { key: 'id', label: 'Suite ID' }, { key: 'name', label: 'Suite' }, { key: 'own', label: 'Own points' }, { key: 'subtree', label: 'Subtree points' }, { key: 'progress', label: 'Progress %' },
                ]} onSelect={row => { invalidate(); setContext({ ...context, suiteId: Number(row.id) }); }} /></section><section className="cc-panel full-width"><h2>Test cases in selected scope</h2>
                    <DataTable name="explorer cases" rows={rows} columns={linkedColumns} onSelect={setDetail} /></section></div>}
            {snapshot && module === 'Live TCM' && <section className="cc-panel"><div className="section-title"><h2>Live Test Case Matrix</h2><span>Read-only ADO source records</span></div>
                {statusFilter && <p>Outcome filter: {statusFilter} <button onClick={() => setStatusFilter('')}>Clear</button></p>}
                <p className="definition">Point identity includes configuration and suite membership. Latest outcomes do not represent period execution counts. Unavailable enrichment is never shown as zero.</p>
                <DataTable name="Live TCM" rows={displayed} columns={linkedColumns} onSelect={setDetail} /></section>}
            {snapshot && module === 'Data Reconciliation' && <section className="cc-panel"><h2>Source reconciliation</h2><p>{snapshot.reconciliation?.validationState}</p>
                <DataTable name="reconciliation" rows={rows} columns={[{ key: 'pointId', label: 'Point' }, { key: 'rawOutcome', label: 'Raw outcome' }, { key: 'outcomeType', label: 'JSON type' }, { key: 'outcome', label: 'Normalized outcome' }]} />
                <pre>{JSON.stringify(snapshot.reconciliation, null, 2)}</pre></section>}
            {module === 'Settings' && <section className="cc-panel"><h2>Reporting settings</h2><p>Europe/London · default cutoff 18:00. Configure server AzureDevOps:DsrCutoff to change it.</p>
                <p>No mandatory release mapping. Select a plan/subtree and explicit baseline dates. Saved table views store columns and sort metadata only.</p>
                <p>Enterprise endpoint, database and authentication require AXA approval. The API remains local-development only.</p></section>}
            {!['Overview', 'Test Explorer', 'Live TCM', 'Data Reconciliation', 'Settings'].includes(module) && <section className="cc-panel"><h2>{module}</h2><p>Dependent evidence and services are being added in subsequent gate commits. Unavailable metrics are not zero.</p></section>}
            {detail && <aside className="detail-panel" aria-label="Record details"><button onClick={() => setDetail(null)}>Close details ×</button><h2>Source record</h2>
                {Object.entries(detail).map(([key, value]) => <div className="stat-row" key={key}><span>{key}</span><strong>{value == null ? 'Unavailable' : String(value)}</strong></div>)}</aside>}
        </main></div></div>;
}
