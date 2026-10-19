const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const ts = require('typescript');

function load(relative, dependencies = {}, globals = {}) {
    const source = fs.readFileSync(path.join(__dirname, '..', relative), 'utf8');
    const compiled = ts.transpileModule(source, {
        compilerOptions: { target: ts.ScriptTarget.ES2023, module: ts.ModuleKind.CommonJS },
    }).outputText;
    const module = { exports: {} };
    vm.runInNewContext(compiled, {
        module, exports: module.exports,
        require: name => {
            if (!(name in dependencies)) throw new Error(`Unexpected import ${name}`);
            return dependencies[name];
        },
        URL, AbortSignal, DOMException, setTimeout, clearTimeout, ...globals,
    });
    return module.exports;
}

function analytics(api = {}) {
    return load('src/services/qaAnalytics.ts', { './adoApi': api });
}

test('empty data has finite zero rates', () => {
    const result = analytics().calculateQaMetrics([]);
    assert.equal(result.executionRate, 0);
    assert.equal(result.passRate, 0);
});

test('none, paused and missing outcome never count as executed', () => {
    const result = analytics().calculateQaMetrics([
        { id: 1, results: { outcome: 'none' } },
        { id: 2, results: { outcome: 'paused' } },
        { id: 3 },
    ]);
    assert.equal(result.executed, 0);
    assert.equal(result.pending, 1);
    assert.equal(result.inProgress, 1);
    assert.equal(result.unknown, 1);
});

test('duplicate points, N/A and blocked use the documented denominators', () => {
    const point = { id: 1, results: { outcome: 'Passed' } };
    const result = analytics().calculateQaMetrics([
        point, point,
        { id: 2, results: { outcome: 'failed' } },
        { id: 3, results: { outcome: 'notApplicable' } },
        { id: 4, results: { outcome: 'blocked' } },
    ]);
    assert.equal(result.total, 4);
    assert.equal(result.executed, 2);
    assert.equal(result.applicable, 3);
    assert.equal(result.passRate, 50);
    assert.ok(Math.abs(result.executionRate - 200 / 3) < 1e-10);
});

test('history counts distinct results per point, excludes blocked and old points', async () => {
    const api = {
        getTestRuns: async () => [{ id: 10, plan: { id: '1' } }],
        getTestResults: async () => [
            { id: 1, state: 'Completed', outcome: 'Passed', testPoint: { id: '100' } },
            { id: 1, state: 'Completed', outcome: 'Passed', testPoint: { id: '100' } },
            { id: 2, state: 'Completed', outcome: 'Failed', testPoint: { id: '100' } },
            { id: 3, state: 'Completed', outcome: 'Blocked', testPoint: { id: '100' } },
            { id: 4, state: 'Completed', outcome: 'Passed', testPoint: { id: '999' } },
            { id: 5, state: 'Completed', outcome: 'Passed' },
        ],
    };
    const result = await analytics(api).loadReexecution(
        1, [{ id: 100 }], new AbortController().signal, () => {}
    );
    assert.equal(result.reExecuted, 1);
    assert.equal(result.additionalExecutions, 1);
    assert.equal(result.executions, 2);
    assert.equal(result.unlinkedResults, 1);
});

function apiWithFetch(fetch) {
    return load('src/services/adoApi.ts', {
        'azure-devops-extension-sdk': {
            getWebContext: () => ({ project: { id: 'project' } }),
            getHost: () => ({ name: 'organization', isHosted: true }),
            getAccessToken: async () => 'test-only-token',
        },
    }, { fetch });
}

test('point continuation token is sent in query string and encoded', async () => {
    const urls = [];
    const api = apiWithFetch(async url => {
        urls.push(new URL(url));
        return new Response(JSON.stringify({ value: [{ id: urls.length }] }), {
            headers: urls.length === 1 ? { 'x-ms-continuationtoken': 'a+b/=' } : {},
        });
    });
    const points = await api.getTestPoints(1, 2, new AbortController().signal);
    assert.equal(points.length, 2);
    assert.equal(urls[1].searchParams.get('continuationToken'), 'a+b/=');
});

test('repeated continuation token fails instead of showing partial totals', async () => {
    const api = apiWithFetch(async () => new Response(JSON.stringify({ value: [{ id: 1 }] }), {
        headers: { 'x-ms-continuationtoken': 'same' },
    }));
    await assert.rejects(
        api.getTestPoints(1, 2, new AbortController().signal), /Repeated/
    );
});

test('history offset pagination continues after short pages until empty', async () => {
    const skips = [];
    const api = apiWithFetch(async url => {
        const skip = Number(new URL(url).searchParams.get('$skip'));
        skips.push(skip);
        return new Response(JSON.stringify({ value: skip < 2 ? [{ id: skip + 1 }] : [] }));
    });
    const runs = await api.getTestRuns(1, new AbortController().signal);
    assert.equal(runs.length, 2);
    assert.deepEqual(skips, [0, 1, 2]);
});
