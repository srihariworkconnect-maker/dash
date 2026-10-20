const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const ts = require('typescript');
const source = fs.readFileSync(require('node:path').join(__dirname, '../src/app/ReportingContext.ts'), 'utf8');
const moduleState = { exports: {} };
vm.runInNewContext(ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText,
    { module: moduleState, exports: moduleState.exports });
const { changePlan, pointRows, matchesOutcome, suiteOptions } = moduleState.exports;
test('plan changes reset suite and tester while preserving reporting dates', () => {
    const next = changePlan({ planId: 1, suiteId: 2, tester: 'identity', from: 'start', to: 'end' }, 3);
    assert.equal(next.planId, 3); assert.equal(next.suiteId, null); assert.equal(next.tester, ''); assert.equal(next.from, 'start');
});
test('TCM keeps point and case identities and does not invent history', () => {
    const rows = pointRows([{ id: 1, testCaseId: 2, memberships: [{ suitePath: 'Root / Child' }], outcome: 'unspecified' }], 'Plan');
    assert.equal(rows[0].pointId, 1); assert.equal(rows[0].caseId, 2);
    assert.equal(rows[0].executionCount, null); assert.equal(rows[0].outcome, 'unspecified');
});
test('KPI drill-through includes each approved denominator category without reclassifying unknowns', () => {
    assert.equal(matchesOutcome('active', 'pending'), true);
    assert.equal(matchesOutcome('unspecified', 'pending'), false);
    assert.equal(matchesOutcome('notApplicable', 'executed'), true);
    assert.equal(matchesOutcome('notApplicable', 'applicable'), false);
    assert.equal(matchesOutcome('blocked', 'secondary'), true);
    assert.equal(matchesOutcome('notApplicable', 'secondary'), false);
});
test('searchable suite choices retain complete parent paths and hierarchy order', () => {
    const choices = suiteOptions([{ suiteId: 3, suiteName: 'Leaf', parentSuiteId: 2 }, { suiteId: 1, suiteName: 'Root', parentSuiteId: null }, { suiteId: 2, suiteName: 'Child', parentSuiteId: 1 }]);
    assert.equal(choices[0].value, '1'); assert.equal(choices[2].label, 'Root / Child / Leaf · 3'); assert.equal(choices[2].depth, 2);
});
