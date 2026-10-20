import { useEffect, useState } from 'react';
import type { BackendClient, PlanAnalytics } from '../services/adoApi';
import type { AdoTestPlan } from '../types/ado';
import DataTable from './DataTable';

export default function ComparisonPanel({ api, plans, planId }: { api: BackendClient; plans: AdoTestPlan[]; planId: number }) {
    const [other, setOther] = useState(''); const [trigger, setTrigger] = useState(0);
    const [rows, setRows] = useState<PlanAnalytics[]>([]); const [error, setError] = useState('');
    useEffect(() => {
        if (!trigger || !other) return;
        const controller = new AbortController();
        void api.compare([planId, Number(other)], controller.signal).then(data => { if (!controller.signal.aborted) setRows(data.snapshots); })
            .catch(cause => { if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : 'Comparison unavailable'); });
        return () => controller.abort();
    }, [api, planId, other, trigger]);
    return <section className="cc-panel"><h2>Cross-plan comparison</h2><p className="definition">Full-plan current state only. Different suite/configuration populations are not assumed equivalent. No combined point total.</p>
        <div className="table-tools"><select aria-label="Comparison plan" value={other} onChange={e => { setRows([]); setTrigger(0); setOther(e.target.value); }}>
            <option value="">Choose another plan</option>{plans.filter(p => p.id !== planId).map(p => <option value={p.id} key={p.id}>{p.name}</option>)}</select>
            <button disabled={!other} onClick={() => { setError(''); setRows([]); setTrigger(t => t + 1); }}>Compare current plans</button></div>
        {error && <p className="error">{error}</p>}
        {rows.length > 0 && <DataTable name="cross-plan" rows={rows.map(s => ({ key: s.planId, planId: s.planId, ...s.metrics, retrievedAt: s.retrievedAt }))}
            columns={['planId', 'total', 'passed', 'failed', 'blocked', 'notApplicable', 'executionRate', 'primaryPassRate', 'secondaryPassRate', 'retrievedAt'].map(key => ({ key, label: key }))} />}
    </section>;
}
