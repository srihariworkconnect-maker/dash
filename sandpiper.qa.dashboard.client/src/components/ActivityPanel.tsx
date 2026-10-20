import { useEffect, useState } from 'react';
import type { BackendClient, ExecutionIntelligence } from '../services/adoApi';
import type { ReportingContext } from '../app/ReportingContext';
import DataTable, { type DataRow } from './DataTable';

export default function ActivityPanel({ api, context, mode }: { api: BackendClient; context: ReportingContext; mode: string }) {
    const [request, setRequest] = useState(0);
    const [data, setData] = useState<ExecutionIntelligence | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [detail, setDetail] = useState<DataRow | null>(null);
    useEffect(() => {
        if (!request || !context.planId) return;
        const controller = new AbortController();
        void api.activity(context.planId, context.suiteId, context.from, context.to, context.baselineFrom, context.tester, controller.signal)
            .then(result => { if (!controller.signal.aborted) setData(result); })
            .catch(cause => { if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : 'History unavailable'); });
        return () => controller.abort();
    }, [api, context, request]);
    const load = () => { setData(null); setError(null); setRequest(r => r + 1); };
    return <section className="cc-panel"><div className="section-title"><h2>{mode} · period activity</h2>
        <button className="primary-button" disabled={!context.from || !context.to || !context.baselineFrom} onClick={load}>Load retained history</button></div>
        <p className="definition">Set an explicit repeat baseline at or before the reporting start. Daily repeats include earlier baseline attempts.
            Assignment counts are current state; execution counts use the actual performer. Bugs/verification require separate work-item evidence.</p>
        {!context.baselineFrom && <p>Choose the baseline start in the global toolbar to enable activity calculations.</p>}
        {request > 0 && !data && !error && <p role="status">Reading paginated result records…</p>}
        {error && <p className="error" role="alert">{error}</p>}
        {data && <><div className="compact-kpis">{[['Re-executed points in baseline', data.reExecutedPoints ?? 'Incomplete'],
            ['Repeat events in period', data.repeatEvents], ['Unique points in period', data.uniquePoints],
            ['Missing point links', data.missingPointLinks], ['Missing dates', data.missingDates]].map(([label, value]) =>
                <div className="compact-kpi" key={label}><small>{label}</small><b>{value}</b></div>)}</div>
            {!data.complete && <p className="error">History is incomplete. Counts below are linked, dated evidence only and are not authoritative totals.</p>}
            <p className="definition">{data.attribution} Baseline: {data.baseline.from} to {data.baseline.to}.
                Activity: {data.window.from} to {data.window.to}.</p>
            <h3>Daily execution and repeat trend</h3><div className="trend-chart" role="img" aria-label="Daily execution and repeat-event counts">
                {data.daily.map(day => <button key={day.day} className="trend-column" title={`${day.day}: ${day.executionEvents} executions, ${day.repeatEvents} repeats`}
                    onClick={() => setDetail({ day: day.day, executions: day.executionEvents, repeats: day.repeatEvents })}>
                    <span style={{ height: `${Math.max(2, day.executionEvents / Math.max(1, ...data.daily.map(d => d.executionEvents)) * 100)}px` }} />
                    <b>{day.executionEvents}</b><small>{day.day}</small></button>)}</div>
            <h3>Tester productivity · no rankings</h3><DataTable name="tester productivity" rows={data.productivity.map(r => ({ ...r, key: `${r.testerKey}:${r.day}` }))}
                columns={[
                    ['tester', 'Actual performer'], ['day', 'UK day'], ['assignedPoints', 'Assigned now'], ['pendingAssigned', 'Pending assigned now'],
                    ['executionEvents', 'Execution events'], ['uniquePoints', 'Unique points'], ['passed', 'Passed events'], ['failed', 'Failed events'],
                    ['blocked', 'Blocked events'], ['repeatEvents', 'Repeat events'], ['reExecutedPoints', 'Repeated points active today'],
                ].map(([key, label]) => ({ key, label }))} onSelect={setDetail} />
            <h3>Execution history and transitions</h3><DataTable name="execution history" rows={data.events.map(e => ({ ...e,
                performerName: e.performer?.displayName ?? 'Unattributed', bugs: e.bugIds.join(', ') || 'No result links returned',
                repeat: e.isRepeat ? 'Repeat' : 'First in baseline' }))} columns={[
                    ['key', 'Run:Result'], ['pointId', 'Point ID'], ['outcome', 'Outcome'], ['completedAt', 'Completed UTC'], ['performerName', 'Actual performer'],
                    ['repeat', 'Baseline classification'], ['transition', 'Transition'], ['bugs', 'Result-level bug links'],
                ].map(([key, label]) => ({ key, label }))} onSelect={setDetail} />
            <details><summary>Calculation limitations</summary><ul>{data.limitations.map(text => <li key={text}>{text}</li>)}</ul></details>
        </>}
        {detail && <aside className="detail-panel"><button onClick={() => setDetail(null)}>Close ×</button><h2>Activity evidence</h2><pre>{JSON.stringify(detail, null, 2)}</pre></aside>}
    </section>;
}
