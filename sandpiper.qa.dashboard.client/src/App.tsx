import { useEffect, useState } from 'react';
import type { AdoTestPlan } from './types/ado';
import { buildSuiteTree, type BackendClient, type PlanAnalytics, type HistoryAnalytics,
    type ReportingWindow } from './services/adoApi';
import TestSuiteTree from './components/TestSuiteTree';

const message = (error: unknown) => error instanceof Error ? error.message : 'Analytics unavailable.';
const rate = (value: number | null) => value == null ? 'N/A' : `${value.toFixed(1)}%`;
const ukTime = (value: string) => new Intl.DateTimeFormat('en-GB', {
    timeZone: 'Europe/London', dateStyle: 'medium', timeStyle: 'short',
}).format(new Date(value));

function History({ api, planId, suiteId }: { api: BackendClient; planId: number; suiteId: number | null }) {
    const [window, setWindow] = useState<ReportingWindow>({ from: '', to: '' });
    const [request, setRequest] = useState<ReportingWindow | null>(null);
    const [result, setResult] = useState<HistoryAnalytics | null>(null);
    const [error, setError] = useState<string | null>(null);
    useEffect(() => {
        const controller = new AbortController();
        void api.dsrPeriod(controller.signal).then(period => {
            if (!controller.signal.aborted) setWindow(period);
        }).catch(cause => { if (!controller.signal.aborted) setError(message(cause)); });
        return () => controller.abort();
    }, [api]);
    useEffect(() => {
        if (!request) return;
        const controller = new AbortController();
        void api.history(planId, suiteId, request, controller.signal).then(data => {
            if (!controller.signal.aborted) setResult(data);
        }).catch(cause => { if (!controller.signal.aborted) setError(message(cause)); });
        return () => controller.abort();
    }, [api, planId, suiteId, request]);
    return <section className="panel section"><h2>Scoped re-execution history</h2>
        <p>Default: last completed DSR period, 6 PM Europe/London cutoff (server configurable).
            For TCR, enter the agreed release start and closure. Dates require Z or an explicit UTC offset.</p>
        <form className="toolbar" onSubmit={event => {
            event.preventDefault();
            setError(null); setResult(null); setRequest({ ...window });
        }}>
            <label>From (inclusive)<input required value={window.from} placeholder="2026-10-18T17:00:00Z"
                onChange={event => setWindow({ ...window, from: event.target.value })} /></label>
            <label>To (exclusive)<input required value={window.to} placeholder="2026-10-19T17:00:00Z"
                onChange={event => setWindow({ ...window, to: event.target.value })} /></label>
            <button type="submit">Load execution history</button>
        </form>
        {request && !result && !error && <p role="status">Reading retained ADO result records...</p>}
        {error && <p role="alert" className="error">{error}</p>}
        {result && <>
            <p>{ukTime(result.metrics.window.from)} to {ukTime(result.metrics.window.to)} UK time</p>
            <div className="stat-row"><span>Re-executed points</span>
                <strong>{result.metrics.reExecuted ?? 'Incomplete'}</strong></div>
            <div className="stat-row"><span>Additional recorded attempts (linked points)</span>
                <strong>{result.metrics.additionalExecutions}</strong></div>
            <div className="stat-row"><span>Qualifying attempts (linked points)</span>
                <strong>{result.metrics.executions}</strong></div>
            {!result.metrics.complete && <p role="alert" className="error">
                History is incomplete: {result.metrics.unlinkedResults} unlinked and {result.metrics.undatedResults} undated
                qualifying records. Linked re-executed points: {result.metrics.linkedReExecuted}.</p>}
            <p className="panel-note">{result.scope} Distinct completed blocked result records qualify;
                repeating the same run/result identity does not create another attempt. No N/A attempts counted.
                Scanned {result.runs} retained runs.</p>
        </>}
    </section>;
}

function Analytics({ api, plan, suiteId, onRefresh }: {
    api: BackendClient; plan: AdoTestPlan; suiteId: number | null; onRefresh: () => void;
}) {
    const [snapshot, setSnapshot] = useState<PlanAnalytics | null>(null);
    const [error, setError] = useState<string | null>(null);
    useEffect(() => {
        const controller = new AbortController();
        void api.analytics(plan.id, suiteId, controller.signal).then(data => {
            if (!controller.signal.aborted) setSnapshot(data);
        }).catch(cause => { if (!controller.signal.aborted) setError(message(cause)); });
        return () => controller.abort();
    }, [api, plan.id, suiteId]);
    if (error) return <><p role="alert" className="error">{error}</p><button onClick={onRefresh}>Retry</button></>;
    if (!snapshot) return <p role="status">Loading complete live plan analytics...</p>;
    const m = snapshot.metrics;
    const cards = [
        ['Total test points', m.total, 'blue'], ['Passed', m.passed, 'green'],
        ['Failed', m.failed, 'red'], ['Blocked', m.blocked, 'orange'],
        ['Pending', m.pending, 'gray'], ['Not applicable', m.notApplicable, 'gray'],
        ['In progress / paused', m.inProgress, 'blue'], ['Other completed', m.otherCompleted, 'orange'],
        ['Unknown outcome', m.unknown, 'gray'],
    ];
    return <>
        <div className="section-title"><h2>{plan.name} · Plan {plan.id}</h2>
            <button onClick={onRefresh}>Refresh plan</button></div>
        <p className="panel-note">Live ADO snapshot · Retrieved {ukTime(snapshot.retrievedAt)} UK time · Rules {snapshot.rulesVersion}</p>
        {m.unknown > 0 && <p role="alert" className="error">{m.unknown} points have unclassified outcomes.
            Review their individual categories before using these rates for a release decision.</p>}
        <section className="kpi-grid section">{cards.map(([label, value, tone]) =>
            <article className={`kpi-card ${tone}`} key={label}><div className="kpi-label">{label}</div>
                <div className="kpi-value">{value}</div></article>)}</section>
        <section className="insights-grid">
            <article className="panel"><h2>Execution progress</h2>
                <div className="progress-number">{rate(m.executionRate)}</div>
                <p>{m.executedApplicable} executed applicable points / {m.applicable} applicable points</p>
                <p className="panel-note">Blocked counts as executed. N/A is excluded from both progress counts.</p></article>
            <article className="panel"><h2>Primary pass rate</h2>
                <div className="quality-score">{rate(m.primaryPassRate)}</div>
                <p>{m.passed} passed / {m.executed} executed points (includes blocked and N/A)</p>
                <div className="stat-row"><span>Secondary: Passed / (Passed + Failed + Blocked)</span>
                    <strong>{rate(m.secondaryPassRate)}</strong></div>
                <p className="panel-note">Terminal outcomes also include inconclusive, timeout, aborted and error.
                    Zero denominators are shown as N/A. These are latest point statuses, not attempt counts.</p></article>
        </section>
        <details className="panel section"><summary>All individual status categories</summary>
            {Object.entries(m.statuses).map(([name, count]) => <div className="stat-row" key={name}>
                <span>{name}</span><strong>{count}</strong></div>)}</details>
        <History api={api} planId={plan.id} suiteId={suiteId} />
        <section className="panel suite-panel"><h2>Test Suite Explorer</h2>
            <p className="panel-note">Hierarchy for the selected scope. Suite metrics in the API are own-suite counts;
                the overall total deduplicates points across the scope.</p>
            <TestSuiteTree suites={buildSuiteTree(snapshot.suites)} /></section>
    </>;
}

function PlanView({ api, plan, refresh, onRefresh }: {
    api: BackendClient; plan: AdoTestPlan; refresh: number; onRefresh: () => void;
}) {
    const [suites, setSuites] = useState<PlanAnalytics['suites']>([]);
    const [suiteId, setSuiteId] = useState<number | null>(null);
    // The initial full-plan response also supplies the subtree selector, without an extra ADO scan.
    const wrappedApi: BackendClient = { ...api, analytics: async (id, selected, signal) => {
        const data = await api.analytics(id, selected, signal);
        if (!signal.aborted && selected == null) setSuites(data.suites);
        return data;
    } };
    // Keep the client stable across state updates so in-flight requests are not restarted.
    const [scopedApi] = useState(wrappedApi);
    return <>
        <div className="toolbar section"><label htmlFor="suite">Suite scope</label>
            <select id="suite" value={suiteId ?? ''} onChange={event =>
                setSuiteId(event.target.value ? Number(event.target.value) : null)}>
                <option value="">All suites</option>
                {suites.map(suite => <option key={suite.suiteId} value={suite.suiteId}>{suite.suiteName} · {suite.suiteId}</option>)}
            </select></div>
        <Analytics key={`${plan.id}:${suiteId}:${refresh}`} api={scopedApi} plan={plan}
            suiteId={suiteId} onRefresh={onRefresh} />
    </>;
}

export default function App({ api }: { api: BackendClient }) {
    const [plans, setPlans] = useState<AdoTestPlan[]>([]);
    const [planId, setPlanId] = useState<number | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [loading, setLoading] = useState(true);
    const [connected, setConnected] = useState(false);
    const [refresh, setRefresh] = useState(0);
    useEffect(() => {
        const handler = () => setRefresh(value => value + 1);
        window.addEventListener('ado-widget-refresh', handler);
        return () => window.removeEventListener('ado-widget-refresh', handler);
    }, []);
    useEffect(() => {
        const controller = new AbortController();
        async function load() {
            try {
                await api.health(controller.signal);
                if (controller.signal.aborted) return;
                setConnected(true);
                const data = await api.plans(controller.signal);
                if (controller.signal.aborted) return;
                setPlans(data.value);
                setPlanId(current => data.value.some(plan => plan.id === current) ? current : data.value[0]?.id ?? null);
                setError(null);
            } catch (cause) { if (!controller.signal.aborted) setError(message(cause)); }
            finally { if (!controller.signal.aborted) setLoading(false); }
        }
        void load();
        return () => controller.abort();
    }, [api, refresh]);
    const selected = plans.find(plan => plan.id === planId);
    return <div className="dashboard"><aside className="sidebar"><div className="brand">
        <div className="brand-icon">S</div><div><strong>Sandpiper</strong><span>QA Intelligence</span></div></div>
        <div className="nav-item active">Live execution overview</div>
        <div className="sidebar-footer">Source: ASP.NET Core → Azure DevOps<span>Europe/London reporting</span></div></aside>
        <main className="main"><header className="topbar"><div><h1>QA Command Center</h1>
            <p>{connected ? 'Local backend reachable. ADO access is checked separately.' : 'Connecting to reporting backend...'}</p></div>
            <div className="toolbar"><label htmlFor="plan">Test plan</label>
                <select id="plan" value={planId ?? ''} disabled={loading || !!error}
                    onChange={event => setPlanId(Number(event.target.value))}>
                    {!plans.length && <option value="">No plan selected</option>}
                    {plans.map(plan => <option key={plan.id} value={plan.id}>{plan.name}</option>)}</select>
                <button onClick={() => { setLoading(true); setPlans([]); setError(null); setRefresh(value => value + 1); }}>Reload plans</button>
            </div></header>
            {loading ? <p role="status">Loading Azure DevOps plans...</p>
                : error ? <p role="alert" className="error">{error}</p>
                : selected ? <PlanView key={`${selected.id}:${refresh}`} api={api} plan={selected}
                    refresh={refresh} onRefresh={() => setRefresh(value => value + 1)} />
                : <p>No test plans are available to your account.</p>}
        </main></div>;
}
