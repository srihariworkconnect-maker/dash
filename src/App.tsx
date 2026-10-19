import { useEffect, useState } from 'react';
import type { AdoTestPlan, AdoTestSuite, AdoTestPoint } from './types/ado';
import { getTestPlans, getTestSuites } from './services/adoApi';
import {
    loadPlanMetrics, loadReexecution, type QaMetrics, type HistoryMetrics,
} from './services/qaAnalytics';
import TestSuiteTree from './components/TestSuiteTree';

interface Snapshot {
    planId: number;
    suites: AdoTestSuite[];
    metrics: QaMetrics;
    points: AdoTestPoint[];
    retrievedAt: string;
}

function message(error: unknown): string {
    return error instanceof Error ? error.message : 'Unable to load Azure DevOps data.';
}

function PlanDashboard({ plan, onRefresh }: { plan: AdoTestPlan; onRefresh: () => void }) {
    const [snapshot, setSnapshot] = useState<Snapshot | null>(null);
    const [status, setStatus] = useState('Loading suites...');
    const [error, setError] = useState<string | null>(null);
    const [historyRequested, setHistoryRequested] = useState(false);
    const [history, setHistory] = useState<HistoryMetrics | null>(null);
    const [historyStatus, setHistoryStatus] = useState('');
    const [historyError, setHistoryError] = useState<string | null>(null);

    useEffect(() => {
        const controller = new AbortController();
        const { signal } = controller;
        async function load() {
            try {
                const suites = await getTestSuites(plan.id, signal);
                const result = await loadPlanMetrics(plan.id, suites, signal, text => {
                    if (!signal.aborted) setStatus(text);
                });
                if (!signal.aborted) {
                    setSnapshot({ planId: plan.id, suites, ...result,
                        retrievedAt: new Date().toLocaleString() });
                }
            } catch (cause) {
                if (!signal.aborted) {
                    controller.abort();
                    setError(message(cause));
                }
            }
        }
        void load();
        return () => controller.abort();
    }, [plan.id]);

    useEffect(() => {
        if (!historyRequested || !snapshot) return;
        const controller = new AbortController();
        const { signal } = controller;
        void loadReexecution(plan.id, snapshot.points, signal, text => {
            if (!signal.aborted) setHistoryStatus(text);
        }).then(result => {
            if (!signal.aborted) setHistory(result);
        }).catch(cause => {
            if (!signal.aborted) {
                controller.abort();
                setHistoryError(message(cause));
            }
        });
        return () => controller.abort();
    }, [historyRequested, snapshot, plan.id]);

    const metrics = snapshot?.metrics;
    const cards = metrics ? [
        ['Total test points', metrics.total, 'blue'],
        ['Passed', metrics.passed, 'green'],
        ['Failed', metrics.failed, 'red'],
        ['Blocked', metrics.blocked, 'orange'],
        ['Pending', metrics.pending, 'gray'],
        ['Not applicable', metrics.notApplicable, 'gray'],
        ['In progress / paused', metrics.inProgress, 'blue'],
        ['Other completed', metrics.otherCompleted, 'orange'],
        ['Unknown outcome', metrics.unknown, 'gray'],
    ] : [];

    return (
        <>
            <div className="section-title">
                <h2>{plan.name} · Plan {plan.id}</h2>
                <button onClick={onRefresh}>Refresh plan</button>
            </div>
            {error ? <p role="alert" className="error">{error}</p> : !snapshot ? (
                <p role="status">{status}</p>
            ) : metrics && (
                <>
                    <p className="panel-note">Live ADO snapshot · Retrieved {snapshot.retrievedAt}</p>
                    <section className="kpi-grid section">
                        {cards.map(([label, value, tone]) => (
                            <article className={`kpi-card ${tone}`} key={label}>
                                <div className="kpi-label">{label}</div>
                                <div className="kpi-value">{value}</div>
                            </article>
                        ))}
                    </section>
                    <section className="insights-grid">
                        <article className="panel">
                            <h2>Execution progress</h2>
                            <div className="progress-number">{metrics.executionRate.toFixed(1)}%</div>
                            <div className="progress-track"><div className="progress-fill"
                                style={{ width: `${metrics.executionRate}%` }} /></div>
                            <p>{metrics.executed} completed attempts / {metrics.applicable} applicable points</p>
                            <p className="panel-note">N/A is excluded from the denominator. Blocked, pending,
                                in-progress and unknown outcomes do not count as executed.</p>
                        </article>
                        <article className="panel">
                            <h2>Pass rate</h2>
                            <div className="quality-score">{metrics.passRate.toFixed(1)}%</div>
                            <p>{metrics.passed} passed / {metrics.executed} completed attempts</p>
                            <p className="panel-note">Completed attempts include passed, failed, inconclusive,
                                timeout, aborted and error outcomes. These are current point outcomes,
                                not cumulative execution counts.</p>
                        </article>
                    </section>
                    <section className="panel section">
                        <h2>Re-execution history</h2>
                        <p>Scope: all retained, accessible execution history for this plan,
                            restricted to its current test points. No release date filter is applied.</p>
                        {!historyRequested && <button onClick={() => {
                            setHistoryStatus('Loading test runs...');
                            setHistoryRequested(true);
                        }}>Load live execution history</button>}
                        {historyRequested && !history && !historyError &&
                            <p role="status">{historyStatus}</p>}
                        {historyError && <p role="alert" className="error">{historyError}</p>}
                        {history && <>
                            <div className="stat-row"><span>Re-executed points</span>
                                <strong>{history.unlinkedResults ? 'Incomplete — see note' : history.reExecuted}</strong></div>
                            <div className="stat-row"><span>Additional executions of linked points</span>
                                <strong>{history.additionalExecutions}</strong></div>
                            <div className="stat-row"><span>Completed executions of linked points</span>
                                <strong>{history.executions}</strong></div>
                            <p className="panel-note">Scanned {history.runs} test runs. A re-executed point has
                                at least two distinct completed result records with qualifying outcomes.
                                Test steps and iterations are not counted separately.</p>
                            {history.unlinkedResults > 0 && <p role="alert" className="error">
                                {history.unlinkedResults} qualifying results lack a test-point link.
                                The linked-point re-execution count is {history.reExecuted};
                                it is not a complete plan total.</p>}
                        </>}
                    </section>
                    <section className="panel suite-panel">
                        <h2>Test Suite Explorer</h2>
                        {snapshot.suites.length ? <TestSuiteTree suites={snapshot.suites} />
                            : <p>No suites returned for this plan.</p>}
                    </section>
                </>
            )}
        </>
    );
}

export default function App() {
    const [plans, setPlans] = useState<AdoTestPlan[]>([]);
    const [planId, setPlanId] = useState<number | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [loading, setLoading] = useState(true);
    const [refresh, setRefresh] = useState(0);

    useEffect(() => {
        const handler = () => setRefresh(value => value + 1);
        window.addEventListener('ado-widget-refresh', handler);
        return () => window.removeEventListener('ado-widget-refresh', handler);
    }, []);

    useEffect(() => {
        const controller = new AbortController();
        void getTestPlans(controller.signal).then(result => {
            if (controller.signal.aborted) return;
            setPlans(result);
            setPlanId(current => result.some(plan => plan.id === current)
                ? current : result[0]?.id ?? null);
            setError(null);
        }).catch(cause => {
            if (!controller.signal.aborted) setError(message(cause));
        }).finally(() => {
            if (!controller.signal.aborted) setLoading(false);
        });
        return () => controller.abort();
    }, [refresh]);

    const selected = plans.find(plan => plan.id === planId);
    return (
        <div className="dashboard">
            <aside className="sidebar"><div className="brand"><div className="brand-icon">S</div>
                <div><strong>Sandpiper</strong><span>QA Intelligence</span></div></div>
                <div className="nav-item active">Live execution overview</div>
                <div className="sidebar-footer">Source: Azure DevOps<span>Signed-in user access</span></div>
            </aside>
            <main className="main">
                <header className="topbar"><div><h1>QA Command Center</h1>
                    <p>Live test plans, suites, outcomes and execution history.</p></div>
                    <div className="toolbar"><label htmlFor="plan">Test plan</label>
                        <select id="plan" value={planId ?? ''} disabled={loading || !!error}
                            onChange={event => setPlanId(Number(event.target.value))}>
                            {!plans.length && <option value="">No plan selected</option>}
                            {plans.map(plan => <option key={plan.id} value={plan.id}>{plan.name}</option>)}
                        </select>
                        <button onClick={() => {
                            setLoading(true);
                            setPlans([]);
                            setError(null);
                            setRefresh(value => value + 1);
                        }}>Reload plans</button>
                    </div>
                </header>
                {loading ? <p role="status">Loading Azure DevOps test plans...</p>
                    : error ? <p role="alert" className="error">{error}</p>
                    : selected ? <PlanDashboard key={`${selected.id}:${refresh}`} plan={selected}
                        onRefresh={() => setRefresh(value => value + 1)} />
                    : <p>No test plans are available to your account in this project.</p>}
            </main>
        </div>
    );
}
