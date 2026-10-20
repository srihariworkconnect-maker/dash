import { useEffect, useState } from 'react';
import type { BackendClient, ExecutionIntelligence, PlanAnalytics, QualityEvidence } from '../services/adoApi';
import type { ReportingContext } from '../app/ReportingContext';
import DataTable, { type DataRow } from './DataTable';

export default function ActivityPanel({ api, context, mode, snapshot, assignmentSnapshot }: { api: BackendClient; context: ReportingContext; mode: string; snapshot: PlanAnalytics; assignmentSnapshot: PlanAnalytics }) {
    const [request, setRequest] = useState(0);
    const [data, setData] = useState<ExecutionIntelligence | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [detail, setDetail] = useState<DataRow | null>(null);
    const [quality, setQuality] = useState<QualityEvidence | null>(null);
    useEffect(() => {
        if (!request || !context.planId) return;
        const controller = new AbortController();
        void (async () => {
            if (mode === 'Productivity Tracker') {
                const bundle = await api.evidence(context.planId!, context.suiteId, context.from, context.to, context.baselineFrom, context.tester, controller.signal);
                if (!controller.signal.aborted) setQuality(bundle.quality);
                return bundle.activity;
            }
            return api.activity(context.planId!, context.suiteId, context.from, context.to, context.baselineFrom, context.tester, controller.signal);
        })()
            .then(result => { if (!controller.signal.aborted) setData(result); })
            .catch(cause => { if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : 'History unavailable'); });
        return () => controller.abort();
    }, [api, context, request, mode]);
    const load = () => { setData(null); setQuality(null); setError(null); setRequest(r => r + 1); };
    const activityPoints = new Map(data?.points.map(p => [p.pointId, p]) ?? []);
    const suiteScope = new Set(context.suiteId == null ? snapshot.suites.map(s => s.suiteId) : [context.suiteId]);
    let expanded = true;
    while (expanded) { expanded = false; for (const s of snapshot.suites) if (s.parentSuiteId != null && suiteScope.has(s.parentSuiteId) && !suiteScope.has(s.suiteId)) { suiteScope.add(s.suiteId); expanded = true; } }
    const scopePoints = snapshot.points.filter(p => p.memberships.some(m => suiteScope.has(m.suiteId)));
    const assignments = assignmentSnapshot.assignments ?? [];
    const suiteHotspots = snapshot.suites.filter(s => suiteScope.has(s.suiteId)).map(suite => {
        const points = scopePoints.filter(p => p.memberships.some(m => m.suiteId === suite.suiteId));
        return { suiteId: suite.suiteId, suite: suite.suiteName, ownPoints: points.length,
            repeatedPoints: points.filter(p => (activityPoints.get(p.id)?.executionCount ?? 0) > 1).length,
            repeatEvents: points.reduce((n, p) => n + (activityPoints.get(p.id)?.repeatCount ?? 0), 0) };
    });
    return <section className="cc-panel"><div className="section-title"><h2>{mode} · period activity</h2>
        <button className="primary-button" disabled={!context.from || !context.to || !context.baselineFrom} onClick={load}>Load retained history</button></div>
        <p className="definition">Set an explicit repeat baseline at or before the reporting start. Daily repeats include earlier baseline attempts.
            Assignment counts are current state; execution counts use the actual performer. Bugs/verification require separate work-item evidence.</p>
        {!context.baselineFrom && <p>Choose the baseline start in the global toolbar to enable activity calculations.</p>}
        <h3>Current assignments · includes testers without period executions</h3><DataTable name="current assignment roster" rows={assignments}
            columns={[{ key: 'tester', label: 'Assigned tester' }, { key: 'assignedNow', label: 'Assigned now' }, { key: 'pendingNow', label: 'Pending assigned now' }]} />
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
            <h3>Team period activity</h3><p>{data.events.filter(e => e.qualifies).length} qualifying recorded events; {data.uniquePoints} linked unique points; {data.repeatEvents} repeat events. Partial evidence is explicitly flagged above.</p>
            {quality && <><h3>Daily bug activity · result-linked defect scope</h3><p>{quality.schemaStatus}</p>
                {quality.available ? <DataTable name="productivity bug activity" rows={quality.bugActivity.map(b => ({ ...b }))}
                    columns={['tester', 'day', 'raised', 'resolved', 'reopened', 'verifiedWithEvidence', 'verificationAttempts'].map(key => ({ key, label: key }))} onSelect={setDetail} />
                    : <p>Bug activity unavailable; execution activity remains visible separately.</p>}</>}
            <h3>Suite re-execution hotspots · own-suite evidence</h3><p>Overlapping suite memberships are not added together into team totals. These counts cover the selected baseline and known linked records.</p>
            <DataTable name="suite repeat hotspots" rows={suiteHotspots} columns={['suiteId', 'suite', 'ownPoints', 'repeatedPoints', 'repeatEvents'].map(key => ({ key, label: key }))} onSelect={setDetail} />
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
