import { useEffect, useState } from 'react';
import type { BackendClient, PlanAnalytics, ReportSnapshot } from '../services/adoApi';
import type { ReportingContext } from '../app/ReportingContext';
import DataTable from './DataTable';

export default function OperationsPanel({ api, mode, snapshot, context }: {
    api: BackendClient; mode: string; snapshot: PlanAnalytics | null; context: ReportingContext;
}) {
    const [message, setMessage] = useState('');
    const [audit, setAudit] = useState<Record<string, unknown>[]>([]);
    const [reports, setReports] = useState<Awaited<ReturnType<BackendClient['reports']>>>([]);
    const [selected, setSelected] = useState<ReportSnapshot | null>(null);
    const [counts, setCounts] = useState<Record<string, string>>({ total: '', passed: '', failed: '', blocked: '', pending: '', notApplicable: '' });
    const [comparison, setComparison] = useState<Record<string, unknown>[]>([]);
    const [reload, setReload] = useState(0);
    const fail = (e: unknown) => setMessage(e instanceof Error ? e.message : 'Request unavailable');
    useEffect(() => {
        if (mode !== 'Audit History') return;
        const c = new AbortController();
        void Promise.all([api.audit(c.signal), api.reports(c.signal)]).then(([a, r]) => {
            if (!c.signal.aborted) { setAudit(a.entries); setReports(r); setMessage(a.scope); }
        }).catch(e => { if (!c.signal.aborted) fail(e); });
        return () => c.abort();
    }, [api, mode, reload]);
    const download = async (format: string) => {
        if (!selected) return;
        try {
            const blob = await api.exportReport(selected.id, format, new AbortController().signal);
            const url = URL.createObjectURL(blob), a = document.createElement('a');
            a.href = url; a.download = `Sandpiper-${selected.kind}.${format}`; a.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
        } catch (e) { fail(e); }
    };
    return <section className="cc-panel"><h2>{mode}</h2>{message && <p role="status">{message}</p>}
        {mode === 'Settings' && <><p>Europe/London · server-configurable cutoff (default 18:00). Dates require an explicit UTC offset.</p>
            <p>Current snapshot caches expire within 60 seconds; completed-run caches are revision keyed. Refresh clears local source caches. No confidential source data is stored in browser saved views.</p>
            <p>Local Visual Studio identity only. Enterprise authentication, hosting, database retention and installed-extension connectivity await approval and VDI testing.</p>
            <button onClick={() => { void api.auth(new AbortController().signal).then(a => setMessage(a.authenticated ? 'Local credential acquired. ADO authorization and installed-extension connectivity are not established by this check.' : a.reason ?? 'Credential unavailable')).catch(fail); }}>Check local sign-in</button></>}
        {mode === 'Data Reconciliation' && snapshot && <><p>{snapshot.reconciliation.validationState}</p>
            <p>Enter ADO Progress Report counts captured for the same plan, subtree, tester and retrieval time. This compares your reference; it does not independently certify corporate accuracy.</p>
            <div className="period-fields">{Object.entries(counts).map(([key, value]) => <label key={key}>{key}<input type="number" min="0" value={value} onChange={e => setCounts({ ...counts, [key]: e.target.value })} /></label>)}</div>
            <button disabled={Object.values(counts).some(v => !/^\d+$/.test(v))} onClick={() => {
                void api.reconcile(snapshot.planId, context.suiteId, context.tester, Object.fromEntries(Object.entries(counts).map(([k, v]) => [k, Number(v)])), new AbortController().signal)
                    .then(result => { setComparison(result.differences); setMessage(result.status); }).catch(fail);
            }}>Compare reference counts</button>
            <DataTable name="reference comparison" rows={comparison} columns={['category', 'source', 'reference', 'difference'].map(key => ({ key, label: key }))} />
            <details><summary>Normalization evidence and warnings</summary><pre>{JSON.stringify(snapshot.reconciliation, null, 2)}</pre></details></>}
        {mode === 'Audit History' && <><p>Frozen drafts are kept for one hour, up to 25 per local process. Export evidence JSON before restart. Imported checksums detect changes; they do not establish authorship or live ADO validation.</p>
            <button onClick={() => setReload(r => r + 1)}>Reload history</button>
            <label>Import frozen evidence JSON<input type="file" accept=".json,application/json" onChange={async e => {
                const file = e.target.files?.[0]; if (!file) return;
                if (file.size > 20_000_000) { setMessage('Import exceeds the 20 MB limit.'); return; }
                try { const report = await api.importReport(JSON.parse(await file.text()), new AbortController().signal); setSelected(report); setReload(r => r + 1); }
                catch (error) { fail(error); }
            }} /></label>
            <DataTable name="frozen report history" rows={reports} columns={['id', 'kind', 'createdAt', 'status', 'planId', 'checksum'].map(key => ({ key, label: key }))}
                onSelect={row => { void api.report(String(row.id), new AbortController().signal).then(setSelected).catch(fail); }} />
            {selected && <div><h3>{selected.kind} · {selected.status}</h3><p>{selected.origin} · {selected.createdAt}</p>
                {['xlsx', 'pdf', 'json'].map(format => <button key={format} onClick={() => { void download(format); }}>Download {format.toUpperCase()}</button>)}
                <details><summary>Frozen report limitations</summary>{selected.limitations.map((l, i) => <p key={i}>{l}</p>)}</details></div>}
            <h3>Local request metadata</h3><DataTable name="local request audit" rows={audit} columns={['at', 'method', 'route', 'status', 'traceId', 'durationMs'].map(key => ({ key, label: key }))} /></>}
    </section>;
}
