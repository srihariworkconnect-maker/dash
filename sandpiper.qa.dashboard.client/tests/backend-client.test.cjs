const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const ts = require('typescript');

function load(fetch) {
    const source = fs.readFileSync(path.join(__dirname, '../src/services/adoApi.ts'), 'utf8');
    const compiled = ts.transpileModule(source, {
        compilerOptions: { target: ts.ScriptTarget.ES2023, module: ts.ModuleKind.CommonJS },
    }).outputText;
    const module = { exports: {} };
    vm.runInNewContext(compiled, { module, exports: module.exports, fetch, URL, URLSearchParams });
    return module.exports;
}

test('evidence permission failures retain an explicit status instead of becoming empty data', async () => {
    const exports = load(async () => Response.json({}, { status: 403 }));
    await assert.rejects(exports.createBackendClient('/api').evidence(1, null, '2026-10-19T00:00:00Z', '2026-10-20T00:00:00Z', '2026-10-18T00:00:00Z', '', new AbortController().signal),
        error => error instanceof exports.BackendRequestError && error.status === 403 && /Permission denied/.test(error.message));
});

test('evidence preserves unavailable null metrics and rejects mismatched boundaries', async () => {
    const from = '2026-10-19T00:00:00Z', to = '2026-10-20T00:00:00Z', baselineFrom = '2026-10-18T00:00:00Z';
    const body = { snapshot: { planId: 1, suiteId: null, complete: true, connected: true }, activity: { window: { from, to }, baseline: { from: baselineFrom }, caseRetesting: { reExecutedTestCases: null } }, quality: { totalLinkedDefects: null, openLinkedDefects: null } };
    const api = load(async () => Response.json(body)).createBackendClient('/api');
    const result = await api.evidence(1, null, from, to, baselineFrom, '', new AbortController().signal);
    assert.equal(result.activity.caseRetesting.reExecutedTestCases, null); assert.equal(result.quality.totalLinkedDefects, null);
    body.activity.window.to = '2026-10-21T00:00:00Z';
    await assert.rejects(api.evidence(1, null, from, to, baselineFrom, '', new AbortController().signal), /mismatched evidence/);
});

test('standalone local uses same-origin proxy without embedding localhost in production', () => {
    const { resolveApiBase } = load();
    assert.equal(resolveApiBase(true, false), '/api');
    assert.equal(resolveApiBase(false, false), '/api');
    assert.throws(() => resolveApiBase(false, true), /approved HTTPS backend/);
    assert.throws(() => resolveApiBase(false, true, 'http://localhost:5243/api'), /localhost/);
    assert.throws(() => resolveApiBase(false, true, '//untrusted.example/api'));
});

test('client forwards cancellation and local protection header', async () => {
    const controller = new AbortController();
    const api = load(async (url, options) => {
        assert.equal(url, '/api/health');
        assert.equal(options.signal, controller.signal);
        assert.equal(options.headers['X-Sandpiper-Client'], 'dashboard');
        return Response.json({ status: 'Healthy', adoConnected: null });
    }).createBackendClient('/api');
    const result = await api.health(controller.signal);
    assert.equal(result.adoConnected, null);
});

test('analytics rejects stale plan responses instead of displaying them', async () => {
    const api = load(async () => Response.json({ planId: 839718, suiteId: null,
        connected: true, complete: true })).createBackendClient('/api');
    await assert.rejects(api.analytics(819259, null, new AbortController().signal), /mismatched/);
});

test('HTML fallback is not treated as analytics', async () => {
    const api = load(async () => new Response('<html>SPA</html>', {
        headers: { 'content-type': 'text/html' },
    })).createBackendClient('/api');
    await assert.rejects(api.health(new AbortController().signal), /Expected backend JSON/);
});

test('history transmits reporting boundaries and suite scope', async () => {
    const window = { from: '2026-10-18T18:00:00+01:00', to: '2026-10-19T18:00:00+01:00' };
    const api = load(async url => {
        const parsed = new URL(url, 'http://localhost');
        assert.equal(parsed.searchParams.get('from'), window.from);
        assert.equal(parsed.searchParams.get('to'), window.to);
        assert.equal(parsed.searchParams.get('suiteId'), '11');
        return Response.json({});
    }).createBackendClient('/api');
    await api.history(819259, 11, window, new AbortController().signal);
});

test('hierarchy attaches children without losing parent roots', () => {
    const { buildSuiteTree } = load();
    const tree = buildSuiteTree([
        { suiteId: 11, suiteName: 'child', parentSuiteId: 10 },
        { suiteId: 10, suiteName: 'root', parentSuiteId: null },
    ]);
    assert.equal(tree.length, 1);
    assert.equal(tree[0].children[0].id, 11);
});
