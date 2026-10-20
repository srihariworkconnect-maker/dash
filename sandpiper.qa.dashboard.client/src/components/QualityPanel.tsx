import { useEffect, useState } from 'react';
import type { BackendClient, WorkspaceEvidence } from '../services/adoApi';
import type { ReportingContext } from '../app/ReportingContext';
import DataTable, { type DataRow } from './DataTable';

export default function QualityPanel({ api, context, onLoaded, onError, autoLoad = false, compact = false }: {
    api: BackendClient; context: ReportingContext; onLoaded?: (value: WorkspaceEvidence) => void;
    onError?: (message: string) => void; autoLoad?: boolean; compact?: boolean;
}) {
    const [refresh, setRefresh] = useState(0);
    const [data, setData] = useState<WorkspaceEvidence | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [detail, setDetail] = useState<DataRow | null>(null);
    const ready = Boolean(context.planId && context.from && context.to && context.baselineFrom);
    // Load automatically once scope and an explicit baseline are available, so the Overview KPIs
    // populate without a manual click. The caller remounts this panel on context change (via key),
    // so a given selection fetches exactly once; the manual button bumps `refresh` to re-fetch.
    const shouldLoad = (autoLoad || refresh > 0) && ready;
    useEffect(() => {
        if (!shouldLoad || !context.planId) return;
        const controller = new AbortController();
        void api.evidence(context.planId, context.suiteId, context.from, context.to, context.baselineFrom, context.tester, controller.signal)
            .then(result => { if (!controller.signal.aborted) { setData(result); onLoaded?.(result); } })
            .catch(cause => { if (!controller.signal.aborted) { const message = cause instanceof Error ? cause.message : 'Evidence unavailable'; setError(message); onError?.(message); } });
        return () => controller.abort();
        // onLoaded/onError are result notifications, not query keys.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [api, context, shouldLoad, refresh]);
    const q = data?.quality;
    return <section className="cc-panel"><div className="section-title"><h2>{compact ? 'TCM source enrichment' : 'Quality Intelligence'}</h2>
        <button className="primary-button" disabled={!ready}
            onClick={() => { setData(null); setError(null); setRefresh(t => t + 1); }}>{autoLoad ? 'Refresh evidence' : 'Inspect schema and load evidence'}</button></div>
        <p className="definition">Reads actual WIT fields, Bug states/categories and requirement categories before interpretation.
            Defect scope uses inspected case relationships and result-level Bug links. Verification still requires result-level evidence. Set an explicit baseline to enable.</p>
        {!ready && <p role="status">Set a reporting window and an explicit baseline to load historical evidence.</p>}
        {shouldLoad && !data && !error && <p role="status">Inspecting process metadata and reading result/work-item evidence…</p>}
        {error && <p className="error" role="alert">{error}</p>}
        {q && <><p className={q.available ? 'trust-banner' : 'error'}>{q.schemaStatus}</p>
            {!q.relationshipsComplete && <p className="error">History or case/result Bug relationships are incomplete. Scoped defect totals are withheld.</p>}
            {!compact && <><h3>Blocker aging · current blocked points</h3><DataTable name="blocker aging" rows={(q.blockers ?? []).map(b => ({ ...b }))}
                columns={['pointId', 'observedSince', 'observedAgeDays', 'evidenceStatus'].map(key => ({ key, label: key }))} onSelect={setDetail} />
                <h3>Observed regression transitions in reporting period</h3><DataTable name="quality regression evidence" rows={(data?.activity.events ?? []).filter(e => e.transition).map(e => ({ key: e.key, pointId: e.pointId, completedAt: e.completedAt, transition: e.transition }))}
                    columns={['key', 'pointId', 'completedAt', 'transition'].map(key => ({ key, label: key }))} onSelect={setDetail} /></>}
            {q.available && !compact && <><div className="compact-kpis">{[['Total scoped defects', q.totalLinkedDefects ?? 'Incomplete / unavailable'], ['Open linked defects', q.openLinkedDefects ?? 'Unavailable'], ['Completed linked defects', q.completedLinkedDefects ?? 'Unavailable'], ['Requirement-link gaps', q.requirementGaps ?? 'Unavailable'],
                ['Release readiness', q.releaseReadiness]].map(([label, value]) => <div className="compact-kpi" key={label}><small>{label}</small><b>{value}</b></div>)}</div>
                <h3>Explainable risk summary</h3><ul>{q.riskReasons.map(reason => <li key={reason}>{reason}</li>)}</ul>
                <h3>Defect state distribution</h3>{[...new Set(q.defects.map(b => b.state ?? 'Unknown'))].map(state => <button key={state} className="distribution-row"
                    onClick={() => setDetail({ state, bugIds: q.defects.filter(b => (b.state ?? 'Unknown') === state).map(b => b.bugId).join(', ') })}>
                    <span>{state}</span><b>{q.defects.filter(b => (b.state ?? 'Unknown') === state).length}</b></button>)}
                <h3>Defect verification queue</h3><DataTable name="defect verification" rows={q.defects.map(b => ({ ...b, key: b.bugId,
                    createdByName: b.createdBy?.displayName ?? 'Unattributed', assignedName: b.assignedTo?.displayName,
                    retestedByName: b.retestedBy?.displayName, evidence: b.verificationEvidence.join('; ') }))}
                    columns={['bugId', 'title', 'state', 'severity', 'priority', 'ageDays', 'verificationStatus', 'createdByName', 'createdAt', 'assignedName', 'retestedByName', 'retestedAt', 'verificationOutcome', 'evidence'].map(key => ({ key, label: key }))} onSelect={setDetail} />
                <h3>Daily bug activity · Created By and revision actors</h3><DataTable name="bug activity" rows={q.bugActivity.map(b => ({ ...b, key: `${b.testerKey}:${b.day}` }))}
                    columns={['tester', 'day', 'raised', 'resolved', 'reopened', 'verifiedWithEvidence', 'verificationAttempts'].map(key => ({ key, label: key }))} onSelect={setDetail} />
                <h3>Completion scenarios</h3>{q.forecast ? <pre>{JSON.stringify(q.forecast, null, 2)}</pre> : <p>Unavailable: insufficient validated baseline/throughput evidence. No AI forecast.</p>}
                <details><summary>Inspected schema and transparent state categories</summary><pre>{JSON.stringify({ fields: q.inspectedFields, states: q.stateCategories }, null, 2)}</pre></details>
            </>}
            <details><summary>Evidence limitations</summary><ul>{q.limitations.map(l => <li key={l}>{l}</li>)}</ul></details>
        </>}
        {detail && <aside className="detail-panel"><button onClick={() => setDetail(null)}>Close ×</button><h2>Defect / activity evidence</h2><pre>{JSON.stringify(detail, null, 2)}</pre></aside>}
    </section>;
}
