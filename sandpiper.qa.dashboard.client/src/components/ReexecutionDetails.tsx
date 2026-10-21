import { useState } from 'react';
import type { ExecutionIntelligence } from '../services/adoApi';
import DataTable from './DataTable';

export default function ReexecutionDetails({ report, baseline, periodRepeats, complete }: {
    report: ExecutionIntelligence['reexecution']; baseline?: ExecutionIntelligence['baselineReexecution']; periodRepeats?: number; complete?: boolean;
}) {
    const [selected, setSelected] = useState<number | null>(null);
    const point = report?.points.find(p => p.pointId === selected);
    const rows = (report?.points ?? []).map(p => ({ pointId: p.pointId, caseId: p.testCaseId, title: p.title,
        suite: p.suites.map(s => s.suitePath).join('; '), executionCount: p.executionCount, reexecutionCount: p.reexecutionCount,
        firstExecution: p.attempts[0]?.completedAt, lastExecution: p.attempts.at(-1)?.completedAt,
        outcomes: p.attempts.map(a => a.outcome).join(' → '),
        testers: [...new Set(p.attempts.map(a => a.performer?.displayName ?? 'Unattributed'))].join('; ') }));
    return <section aria-label="Re-execution drill-down">
        <p>ADO Progress Report terminology: “Re-executed Test Cases” counts distinct test points with at least two qualifying attempts, not distinct case IDs. Additional attempts = execution count − 1 per point.</p>
        {report ? <p>Selected range [{report.window.from}, {report.window.to}). Evidence: {report.state}.</p> : <p role="status">Re-execution evidence is unavailable. Load the selected reporting scope.</p>}
        {report && <p>Re-executed Points · selected range: <b>{report.state === 'Verified' ? report.reexecutedTestPoints : 'Incomplete evidence'}</b>.
            {' '}Repeat Execution Events · additional attempts within selected range: <b>{report.state === 'Verified' ? report.additionalAttempts : 'Incomplete evidence'}</b>.</p>}
        {baseline && <p>Baseline [{baseline.window.from}, {baseline.window.to}): {baseline.state === 'Verified' ? `${baseline.reexecutedTestPoints} re-executed points; ${baseline.additionalAttempts} additional attempts` : 'Incomplete evidence'}.
            {' '}Period repeat activity including earlier baseline attempts: {complete ? periodRepeats : 'Incomplete evidence'}. These scopes can differ.</p>}
        {report?.reasons.map(reason => <p className="evidence-warning" key={reason}>{reason}</p>)}
        {report?.state === 'Verified' && !rows.length && <p role="status">No test points were re-executed in the selected reporting range.</p>}
        {report && report.state !== 'Verified' && <p>Known rows only; complete totals are withheld.</p>}
        {report?.diagnostics && <details><summary>Execution diagnostics · counts and exclusions</summary>
            <p>Counts describe retained accessible result evidence, not a complete audit of overwritten or deleted history.</p>
            <dl>{Object.entries(report.diagnostics).filter(([, value]) => typeof value === 'number').map(([key, value]) => <div key={key}><dt>{key}</dt><dd>{String(value)}</dd></div>)}</dl>
            <h4>Excluded records by reason</h4>{Object.keys(report.diagnostics.excluded).length ? <ul>{Object.entries(report.diagnostics.excluded).map(([reason, count]) => <li key={reason}>{reason}: {count}</li>)}</ul> : <p>No records excluded.</p>}
            <h4>Point mapping</h4><ul>{Object.entries(report.diagnostics.mapping).map(([reason, count]) => <li key={reason}>{reason}: {count}</li>)}</ul>
        </details>}
        <DataTable name="reexecuted test points" rows={rows}
            columns={['pointId', 'caseId', 'title', 'suite', 'executionCount', 'reexecutionCount', 'firstExecution', 'lastExecution', 'outcomes', 'testers'].map(key => ({ key, label: key }))}
            onSelect={row => setSelected(Number(row.pointId))} />
        <p>Select a test point to inspect every qualifying result. Use Columns / saved view for outcomes and tester summary.</p>
        {point && <><h3>Test point {point.pointId} · Test case {point.testCaseId ?? 'Unknown'} · execution attempts</h3>
            <DataTable name="reexecution attempts" rows={point.attempts.map(a => ({ key: a.key, runId: a.runId, resultId: a.resultId,
                completedAt: a.completedAt, outcome: a.outcome, tester: a.performer?.displayName ?? 'Unattributed', isRepeat: a.isRepeat }))}
                columns={['runId', 'resultId', 'completedAt', 'outcome', 'tester', 'isRepeat'].map(key => ({ key, label: key }))} /></>}
    </section>;
}
