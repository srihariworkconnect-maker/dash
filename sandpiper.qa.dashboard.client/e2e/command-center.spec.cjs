const { test, expect } = require('@playwright/test');

// Synthetic HTTP fixtures are confined to this browser test. Application code has no demo fallback.
const metrics = { total: 5000, passed: 2500, failed: 0, blocked: 0, pending: 2500, notApplicable: 0,
    inProgress: 0, otherCompleted: 0, unknown: 0, executed: 2500, applicable: 5000, executedApplicable: 2500,
    executionRate: 50, primaryPassRate: 100, secondaryPassRate: 100, statuses: { passed: 2500, notexecuted: 2500 } };
function snapshot(planId, suiteId, tester) {
    const points = Array.from({ length: 5000 }, (_, i) => ({ id: i + 1, planId, testCaseId: 10000 + i,
        title: `Synthetic case ${i + 1}`, outcome: i % 2 ? 'notexecuted' : 'passed', configurationName: 'Fixture browser',
        assignedTester: { key: 'actor-1', displayName: 'Fixture actor' }, latestExecutedBy: null, latestExecutionDate: null,
        rawOutcome: i % 2 ? '"notexecuted"' : '"passed"', outcomeJsonType: 'String', memberships: [{ suiteId: 2, suitePath: 'Root / Child' }] }));
    return { planId, suiteId, metrics, complete: true, connected: true, points: tester === 'unattributed' ? [] : points,
        suites: [{ suiteId: 1, suiteName: 'Root', parentSuiteId: null, metrics, subtreeMetrics: metrics }, { suiteId: 2, suiteName: 'Child', parentSuiteId: 1, metrics, subtreeMetrics: metrics }],
        reconciliation: { validationState: 'Synthetic test evidence; NOT corporate validation', rawOutcomeTypes: { String: 5000 }, warnings: [] },
        sourceBaseUrl: 'https://dev.azure.com/fixture/project', suiteCount: 2, rulesVersion: 'fixture', retrievedAt: '2026-10-20T12:00:00Z', cacheAgeSeconds: 0 };
}
test.beforeEach(async ({ page }) => {
    await page.route('**/api/**', async route => {
        const url = new URL(route.request().url()); const path = url.pathname;
        let data;
        if (path.endsWith('/testplans')) data = { value: [{ id: 819259, name: 'Fixture plan A' }, { id: 839718, name: 'Fixture plan B' }] };
        else if (path.includes('/analytics/')) data = snapshot(Number(path.split('/').pop()), url.searchParams.has('suiteId') ? Number(url.searchParams.get('suiteId')) : null, url.searchParams.get('tester'));
        else if (path.endsWith('/period')) data = { from: '2026-10-19T17:00:00Z', to: '2026-10-20T17:00:00Z' };
        else if (path.endsWith('/reporting/settings')) data = { timezone: 'Europe/London', dsrCutoff: '18:00', cacheSeconds: 30 };
        else if (path.endsWith('/refresh')) data = { status: 'Cache cleared' };
        else if (path.endsWith('/audit')) data = { scope: 'Fixture request metadata', entries: [] };
        else if (path.endsWith('/reports')) data = [];
        else if (path.endsWith('/exports/table')) {
            await route.fulfill({ status: 200, contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', body: Buffer.from('fixture workbook transport only') }); return;
        } else { await route.fulfill({ status: 503, contentType: 'application/json', body: '{}' }); return; }
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(data) });
    });
});
const nav = (page, name) => page.getByRole('navigation').getByRole('button', { name: new RegExp(name) });
const choose = async (page, label, text) => {
    await page.getByRole('combobox', { name: label, exact: true }).click();
    await page.getByRole('option', { name: new RegExp(text) }).click();
};

test('all eight modules keep shared dates; changing plan resets incompatible scope', async ({ page }) => {
    const errors = []; page.on('pageerror', e => errors.push(e.message));
    await page.goto('/'); await expect(page.getByRole('heading', { name: 'Current execution snapshot' })).toBeVisible();
    await page.getByLabel('Repeat baseline start').fill('2026-10-01T00:00:00Z');
    await choose(page, 'Test Suite', 'Child');
    await choose(page, 'Tester', 'Fixture actor');
    for (const name of ['Test Explorer', 'Live TCM', 'Productivity Tracker', 'DSR Center', 'TCR Center', 'Execution Intelligence', 'Quality Intelligence', 'Overview']) {
        await nav(page, name).click(); await expect(page.locator('.cc-nav button.selected')).toContainText(name);
        await expect(page.getByLabel('Repeat baseline start')).toHaveValue('2026-10-01T00:00:00Z');
    }
    await choose(page, 'Test Plan', 'Fixture plan B');
    await expect(page.getByRole('combobox', { name: 'Test Suite', exact: true })).toContainText('All Suites');
    await expect(page.getByRole('combobox', { name: 'Tester', exact: true })).toContainText('All testers');
    expect(errors).toEqual([]);
});

test('TCM virtual scrolling bounds DOM rows and filtered workbook exports full selected records', async ({ page }) => {
    await page.goto('/'); await nav(page, 'Live TCM').click();
    const table = page.locator('.data-table').filter({ has: page.getByLabel('Search Live TCM', { exact: true }) });
    await expect(table).toContainText('5000 records');
    await table.getByLabel('Virtual scrolling').check();
    expect(await table.locator('tbody tr').count()).toBeLessThan(40);
    await table.getByLabel('Search Live TCM', { exact: true }).fill('Synthetic case 4999');
    await expect(table).toContainText('1 records');
    const requestPromise = page.waitForRequest('**/api/v2/exports/table');
    const downloadPromise = page.waitForEvent('download');
    await table.getByRole('button', { name: 'Export filtered Excel' }).click();
    const request = await requestPromise;
    expect(request.postDataJSON().rows).toHaveLength(1);
    expect((await downloadPromise).suggestedFilename()).toMatch(/\.xlsx$/);
    await table.getByRole('button', { name: '4999', exact: true }).click();
    await expect(page.getByRole('complementary', { name: 'Record details' })).toContainText('Synthetic case 4999');
});

test('refresh invalidates source caches and failures never display fabricated live totals', async ({ page }) => {
    await page.goto('/'); await expect(page.getByRole('heading', { name: 'Current execution snapshot' })).toBeVisible();
    const clear = page.waitForRequest('**/api/v2/cache/refresh'); await page.getByRole('button', { name: /↻ Refresh/ }).click(); await clear;
    await page.route('**/api/ado/frontier/analytics/**', route => route.fulfill({ status: 503, contentType: 'application/json', body: '{}' }));
    await page.getByRole('button', { name: /↻ Refresh/ }).click();
    await expect(page.getByRole('alert')).toContainText('503');
    await expect(page.getByRole('heading', { name: 'Current execution snapshot' })).toHaveCount(0);
});

test('combobox search is internal, keyboard selection restores focus, Escape preserves selection', async ({ page }) => {
    await page.goto('/'); await expect(page.getByRole('heading', { name: 'Current execution snapshot' })).toBeVisible();
    await expect(page.getByRole('searchbox')).toHaveCount(0);
    const plan = page.getByRole('combobox', { name: 'Test Plan', exact: true });
    await plan.focus(); await plan.press('ArrowDown');
    const search = page.getByRole('searchbox', { name: 'Search Test Plan' }); await expect(search).toBeFocused();
    await search.fill('plan B'); await expect(page.getByRole('listbox').getByRole('option')).toHaveCount(1);
    await search.press('Enter'); await expect(plan).toContainText('Fixture plan B'); await expect(plan).toBeFocused();
    await plan.click(); await page.getByRole('searchbox').fill('no such plan'); await expect(page.getByText('No matching options', { exact: true })).toBeVisible();
    await page.getByRole('searchbox').press('Escape'); await expect(plan).toBeFocused(); await expect(plan).toContainText('Fixture plan B');
    await page.getByRole('combobox', { name: 'Test Suite', exact: true }).click();
    await expect(page.getByRole('option', { name: /Root \/ Child/ })).toBeVisible();
    await page.getByRole('searchbox').press('Escape');
});

function businessFixture() {
    const source = snapshot(819259, null, ''); source.points = source.points.slice(0, 3).map((p, i) => ({ ...p, testCaseId: i < 2 ? 1001 : 1002, outcome: i === 0 ? 'passed' : 'notexecuted' }));
    source.metrics = { ...metrics, total: 3, passed: 1, pending: 2, executed: 1, applicable: 3, executedApplicable: 1, executionRate: 33.3333, statuses: { passed: 1, notexecuted: 2 } };
    source.suites = source.suites.map(s => ({ ...s, metrics: s.suiteId === 2 ? source.metrics : { ...source.metrics, total: 0, passed: 0, pending: 0, executed: 0, applicable: 0, executedApplicable: 0, statuses: {} }, subtreeMetrics: source.metrics }));
    source.caseMetrics = { totalTestCases: 2, executedTestCases: 1, executionCoverage: 50, knownTestCases: 2, knownExecutedTestCases: 1, missingCaseMappings: 0, state: 'Verified', testCaseIds: [1001, 1002], executedCaseIds: [1001] };
    const attempts = [1, 2, 3, 4].map((id, i) => ({ key: `1:${id}`, runId: 1, resultId: id, pointId: i < 2 ? 1 : 2, outcome: 'passed', completedAt: '2026-10-19T18:00:00Z', performer: { key: 'actor-1', displayName: 'Fixture actor' }, qualifies: true, isRepeat: i % 2 === 1, bugIds: [], transition: null }));
    const activity = { window: { from: '2026-10-19T17:00:00Z', to: '2026-10-20T17:00:00Z' }, baseline: { from: '2026-10-01T00:00:00Z', to: '2026-10-20T17:00:00Z' },
        events: attempts, productivity: [], daily: [], points: [{ pointId: 1, executionCount: 2, repeatCount: 1 }, { pointId: 2, executionCount: 2, repeatCount: 1 }], reExecutedPoints: 2, repeatEvents: 2, uniquePoints: 2,
        missingPointLinks: 0, missingDates: 0, complete: true, limitations: [], evidence: { state: 'Verified', reasons: [] },
        caseRetesting: { reExecutedTestCases: 1, knownReExecutedTestCases: 1, missingCaseMappings: 0, state: 'Verified', cases: [{ caseId: 1001, pointIds: [1, 2], attempts }] } };
    activity.reexecution = { window: activity.window, reexecutedTestPoints: 2, additionalAttempts: 2,
        knownReexecutedTestPoints: 2, knownAdditionalAttempts: 2, state: 'Verified', reasons: [],
        diagnostics: { sourceResults: 5, uniqueResults: 5, duplicatesRemoved: 0, qualifyingExecutions: 4,
            uniqueTestPoints: 2, repeatedTestPoints: 2, additionalAttempts: 2, excluded: { BeforeStart: 1 }, mapping: { Direct: 5 } },
        points: [1, 2].map(pointId => ({ pointId, testCaseId: 1001, title: 'Synthetic shared case', suites: [{ suiteId: 2, suitePath: 'Root / Child' }],
            executionCount: 2, reexecutionCount: 1, attempts: attempts.filter(a => a.pointId === pointId) })) };
    const quality = { available: true, relationshipsComplete: true, evidence: { state: 'Verified', reasons: [] }, schemaStatus: 'Synthetic test schema',
        totalLinkedDefects: 2, openLinkedDefects: 1, completedLinkedDefects: 1, releaseReadiness: 'Not Assessed', riskReasons: [], cases: [], bugActivity: [], limitations: [],
        defects: [9001, 9002].map((id, i) => ({ bugId: id, title: `Synthetic bug ${id}`, state: i ? 'Closed' : 'Active', category: i ? 'Completed' : 'InProgress', linkedCaseIds: [1001], relatedResults: attempts, verificationEvidence: [] })) };
    return { snapshot: source, activity, quality, retrievedAt: source.retrievedAt };
}
const kpi = (page, label) => page.locator('button.business-kpi').filter({ has: page.locator('small').getByText(label, { exact: true }) });
async function installBusiness(page, evidence, status = 200, delay = 0) {
    await page.route('**/api/ado/frontier/analytics/**', route => route.fulfill({ json: evidence.snapshot }));
    await page.route('**/api/v2/evidence/**', async route => {
        if (delay) await new Promise(resolve => setTimeout(resolve, delay));
        await route.fulfill({ status, json: status === 200 ? evidence : {} });
    });
    await page.goto('/'); await expect(page.getByRole('heading', { name: 'Current execution snapshot' })).toBeVisible();
    await page.getByLabel('Repeat baseline start').fill('2026-10-01T00:00:00Z');
}

test('Overview case and defect KPIs match drill rows and retain technical point counts', async ({ page }) => {
    await installBusiness(page, businessFixture(), 200, 500);
    await expect(kpi(page, 'Total Defects').locator('b')).toHaveText('Loading');
    await expect(kpi(page, 'Total Defects').locator('b')).toHaveText('2');
    await expect(kpi(page, 'Total Test Cases').locator('b')).toHaveText('2');
    await expect(kpi(page, 'Total Test Points').locator('b')).toHaveText('3');
    await expect(kpi(page, 'Executed Test Cases').locator('b')).toHaveText('1');
    await expect(kpi(page, 'Execution Coverage').locator('b')).toHaveText('50.0%');
    await expect(kpi(page, 'Re-executed Test Cases').locator('b')).toHaveText('2');
    await expect(kpi(page, 'Total Re-execution Attempts').locator('b')).toHaveText('2');
    await expect(kpi(page, 'Release Readiness').locator('b')).toHaveText('Not Assessed');
    await page.screenshot({ path: 'test-results/overview-kpi-enhancement.png', fullPage: true });
    await kpi(page, 'Executed Test Cases').click(); await expect(page.getByRole('complementary', { name: 'KPI evidence' })).toContainText('1 records');
    await page.getByRole('button', { name: 'Close KPI evidence' }).click();
    await kpi(page, 'Open Defects').click(); const detail = page.getByRole('complementary', { name: 'KPI evidence' });
    await expect(detail).toContainText('1 records'); await expect(detail.getByRole('link', { name: /9001/ })).toBeVisible();
    await page.getByRole('button', { name: 'Close KPI evidence' }).click();
    await kpi(page, 'Re-executed Test Cases').click();
    await detail.getByText('Execution diagnostics · counts and exclusions').click();
    await expect(detail).toContainText('BeforeStart: 1');
    await expect(detail).toContainText('qualifyingExecutions');
    await expect(detail).toContainText('2 records');
    await detail.getByRole('button', { name: '1', exact: true }).click();
    await expect(detail.getByRole('heading', { name: 'Test point 1 · Test case 1001 · execution attempts' })).toBeVisible();
    await expect(detail.getByRole('columnheader', { name: 'runId' })).toBeVisible();
    await detail.getByLabel('Search reexecuted test points', { exact: true }).fill('no match');
    await expect(detail).toContainText('No matching records in this collected scope.');
    await detail.getByLabel('Search reexecuted test points', { exact: true }).fill('');
    await detail.getByRole('columnheader', { name: 'executionCount', exact: true }).getByRole('button').click();
    await expect(detail.getByRole('columnheader', { name: 'executionCount', exact: true })).toHaveAttribute('aria-sort', 'ascending');
});

test('incomplete null evidence stays incomplete while a genuine verified zero is displayed', async ({ page }) => {
    const data = businessFixture(); data.activity.complete = false; data.activity.caseRetesting.reExecutedTestCases = null; data.activity.caseRetesting.state = 'Incomplete';
    data.activity.reexecution.state = 'Incomplete'; data.activity.reexecution.reexecutedTestPoints = null; data.activity.reexecution.additionalAttempts = null;
    data.quality.relationshipsComplete = false; data.quality.totalLinkedDefects = null; data.quality.openLinkedDefects = null; data.quality.evidence.state = 'Incomplete';
    await installBusiness(page, data);
    await expect(kpi(page, 'Total Defects').locator('b')).toHaveText('Incomplete evidence');
    await expect(kpi(page, 'Open Defects').locator('b')).toHaveText('Incomplete evidence');
    await expect(kpi(page, 'Re-executed Test Cases').locator('b')).toHaveText('Incomplete evidence');
    data.activity.complete = true; data.activity.caseRetesting = { reExecutedTestCases: 0, knownReExecutedTestCases: 0, missingCaseMappings: 0, state: 'Verified', cases: [] };
    data.activity.reexecution = { ...data.activity.reexecution, state: 'Verified', reexecutedTestPoints: 0, additionalAttempts: 0, points: [] };
    data.quality.relationshipsComplete = true; data.quality.totalLinkedDefects = 0; data.quality.openLinkedDefects = 0; data.quality.evidence.state = 'Verified'; data.quality.defects = [];
    await page.getByRole('button', { name: 'Reload KPI evidence' }).click();
    await expect(kpi(page, 'Total Defects').locator('b')).toHaveText('0');
    await expect(kpi(page, 'Re-executed Test Cases').locator('b')).toHaveText('0');
    await kpi(page, 'Re-executed Test Cases').click();
    await expect(page.getByRole('complementary', { name: 'KPI evidence' })).toContainText('No test points were re-executed in the selected reporting range.');
});

test('Release-to-Date requires confirmed source and resets when plan changes', async ({ page }) => {
    await installBusiness(page, businessFixture());
    await page.getByRole('combobox', { name: /^Reporting period/ }).selectOption('Release-to-Date');
    await expect(page.getByLabel('From (inclusive, ISO offset)', { exact: true })).toHaveValue('');
    await page.getByLabel('Authoritative release start (ISO offset)').fill('2026-10-01T00:00:00Z');
    await page.getByRole('button', { name: 'Confirm release scope' }).click();
    await expect(page.getByRole('alert').first()).toContainText('authoritative release start');
    await page.getByLabel('Release source/reference').fill('Approved release record R2');
    await page.getByRole('button', { name: 'Confirm release scope' }).click();
    await expect(page.getByLabel('From (inclusive, ISO offset)', { exact: true })).toHaveValue('2026-10-01T00:00:00Z');
    await choose(page, 'Test Plan', 'plan B');
    await expect(page.getByLabel('From (inclusive, ISO offset)', { exact: true })).toHaveValue('');
    await expect(page.getByLabel('Release source/reference')).toHaveValue('');
});

test('changing Custom Range cancels obsolete evidence and prevents stale KPI results', async ({ page }) => {
    await installBusiness(page, businessFixture());
    await expect(kpi(page, 'Re-executed Test Cases').locator('b')).toHaveText('2');
    let started; const pending = new Promise(resolve => { started = resolve; });
    let oldRequest;
    await page.route('**/api/v2/evidence/**', async route => {
        const url = new URL(route.request().url());
        if (url.searchParams.get('from') === '2026-10-02T00:00:00Z') {
            oldRequest = route.request(); started();
            await new Promise(resolve => setTimeout(resolve, 1800));
            await route.fulfill({ json: businessFixture() }).catch(() => {}); return;
        }
        const data = businessFixture(); data.activity.window = { from: url.searchParams.get('from'), to: url.searchParams.get('to') };
        data.activity.baseline.from = url.searchParams.get('baselineFrom');
        data.activity.reexecution = { ...data.activity.reexecution, window: data.activity.window, reexecutedTestPoints: 0, additionalAttempts: 0, points: [] };
        await route.fulfill({ json: data });
    });
    const aborted = page.waitForEvent('requestfailed', request => request === oldRequest);
    await page.getByLabel('From (inclusive, ISO offset)', { exact: true }).fill('2026-10-02T00:00:00Z');
    await pending;
    await page.getByLabel('From (inclusive, ISO offset)', { exact: true }).fill('2026-10-03T00:00:00Z');
    await aborted;
    await expect(page.getByLabel('Repeat baseline start')).toHaveValue('2026-10-01T00:00:00Z');
    await expect(kpi(page, 'Re-executed Test Cases').locator('b')).toHaveText('0');
});

for (const [status, label] of [[403, 'Permission denied'], [502, 'Unavailable']]) test(`evidence failure ${status} never becomes zero`, async ({ page }) => {
    await installBusiness(page, businessFixture(), status);
    await expect(kpi(page, 'Total Defects').locator('b')).toHaveText(label);
    await expect(kpi(page, 'Re-executed Test Cases').locator('b')).toHaveText(label);
    await expect(kpi(page, 'Release Readiness').locator('b')).toHaveText(label);
    await kpi(page, 'Release Readiness').click();
    await expect(page.getByRole('complementary', { name: 'KPI evidence' })).toContainText(label);
});

test('DSR preview and summary withhold zero repeat events when history is incomplete', async ({ page }) => {
    const data = businessFixture();
    data.activity.complete = false; data.activity.repeatEvents = 0; data.activity.reExecutedPoints = null;
    data.activity.caseRetesting.reExecutedTestCases = null;
    await page.route('**/api/v2/reports', async route => {
        if (route.request().method() !== 'POST') return route.fulfill({ json: [] });
        return route.fulfill({ json: { id: 'incomplete-fixture', kind: 'DSR', status: 'Draft — incomplete fixture',
            createdAt: '2026-10-21T12:00:00Z', calculationVersion: 'fixture', checksum: 'synthetic',
            currentSnapshot: data.snapshot, activity: data.activity, quality: data.quality,
            narrative: route.request().postDataJSON().narrative, limitations: ['Synthetic incomplete history'] } });
    });
    await installBusiness(page, data);
    await nav(page, 'DSR Center').click();
    await page.getByRole('button', { name: 'Generate evidence draft' }).click();
    await expect(page.getByText(/Incomplete period repeat events/)).toBeVisible();
    await expect(page.getByLabel('Email-ready report summary')).toHaveValue(/Incomplete repeat events/);
});

test('unified filters reach all eight modules and preserved DSR/TCR reopen without regeneration', async ({ page }) => {
    const generated = new Map(), calls = [], requests = [];
    function scoped(url) {
        const d = businessFixture(), s = d.snapshot;
        s.appliedCategory = url.searchParams.get('category'); s.appliedOutcome = url.searchParams.get('outcome');
        s.availableCategories = ['Targeted Regression', 'Unclassified'];
        s.points.forEach(p => { p.testingCategories = ['Targeted Regression']; p.memberships.forEach(m => { m.testingCategory = 'Targeted Regression'; }); });
        if (s.appliedOutcome) s.points = s.points.filter(p => p.outcome === s.appliedOutcome);
        s.metrics = { ...s.metrics, total: s.points.length, pending: s.points.filter(p => p.outcome === 'notexecuted').length, passed: s.points.filter(p => p.outcome === 'passed').length };
        if (url.searchParams.has('from')) {
            d.activity.window = { from: url.searchParams.get('from'), to: url.searchParams.get('to') };
            d.activity.baseline = { from: url.searchParams.get('baselineFrom'), to: d.activity.window.to };
            d.activity.events.forEach((e, i) => { e.completedAt = new Date(Date.parse(d.activity.window.from) + 3600000 + i * 1000).toISOString(); });
            d.activity.reexecution.window = d.activity.window;
            d.activity.baselineReexecution = { ...d.activity.reexecution, window: d.activity.baseline };
        }
        return d;
    }
    await page.route('**/api/**', async route => {
        const url = new URL(route.request().url()), path = url.pathname;
        calls.push(url);
        if (path.includes('/analytics/')) return route.fulfill({ json: scoped(url).snapshot });
        if (path.includes('/evidence/')) return route.fulfill({ json: scoped(url) });
        if (path.includes('/activity/')) return route.fulfill({ json: scoped(url).activity });
        if (path.endsWith('/period') && url.searchParams.get('preset') === 'Last Month')
            return route.fulfill({ json: { from: '2026-09-01T17:00:00Z', to: '2026-10-01T17:00:00Z' } });
        if (path.endsWith('/reports') && route.request().method() === 'POST') {
            const body = route.request().postDataJSON(); requests.push(body);
            const reportUrl = new URL('https://fixture.invalid/?' + new URLSearchParams({ from: body.from, to: body.to, baselineFrom: body.baselineFrom, category: body.category, outcome: body.outcome }));
            const d = scoped(reportUrl), id = `saved-${body.kind}`;
            const report = { id, kind: body.kind, status: 'Draft fixture', createdAt: '2026-10-21T12:00:00Z', origin: 'Synthetic', checksum: 'synthetic', calculationVersion: 'fixture',
                currentSnapshot: d.snapshot, activity: d.activity, quality: d.quality, narrative: body.narrative, limitations: ['Synthetic fixture only'],
                scope: { category: body.category, outcome: body.outcome, currentStateCollectedAt: d.snapshot.retrievedAt, historicalAsOfStatus: 'Unavailable', membershipBasis: 'Current membership' } };
            generated.set(id, report); return route.fulfill({ json: report });
        }
        if (path.endsWith('/reports')) return route.fulfill({ json: [...generated.values()].map(r => ({ ...r, planId: r.currentSnapshot.planId })) });
        if (path.includes('/reports/saved-')) return route.fulfill({ json: generated.get(path.split('/').pop()) });
        if (path.endsWith('/exports/table') && url.searchParams.get('format') === 'pdf')
            return route.fulfill({ contentType: 'application/pdf', body: Buffer.from('%PDF-1.4 Synthetic transport fixture') });
        return route.fallback();
    });
    await page.goto('/'); await expect(page.getByRole('heading', { name: 'Current execution snapshot' })).toBeVisible();
    await choose(page, 'Testing Category', 'Targeted Regression');
    await expect(page.getByRole('heading', { name: 'Current execution snapshot' })).toBeVisible();
    await choose(page, 'Outcome', '^passed$');
    await page.getByRole('combobox', { name: /^Reporting period/ }).selectOption('Last Month');
    await expect(page.getByLabel('From (inclusive, ISO offset)', { exact: true })).toHaveValue('2026-09-01T17:00:00Z');
    await expect(page.getByLabel('Repeat baseline start')).toHaveValue('2026-09-01T17:00:00Z');
    await page.getByLabel('Repeat baseline start').fill('2026-08-01T17:00:00Z');
    for (const name of ['Overview', 'Test Explorer', 'Live TCM', 'Productivity Tracker', 'DSR Center', 'TCR Center', 'Execution Intelligence', 'Quality Intelligence']) {
        await nav(page, name).click();
        await expect(page.getByRole('combobox', { name: 'Testing Category', exact: true })).toContainText('Targeted Regression');
        await expect(page.getByRole('combobox', { name: 'Outcome', exact: true })).toContainText('passed');
        if (name === 'Live TCM') {
            const download = page.waitForEvent('download');
            await page.getByRole('button', { name: 'Export filtered PDF', exact: true }).first().click();
            expect((await download).suggestedFilename()).toBe('Sandpiper-selected-records.pdf');
        }
        if (['DSR Center', 'TCR Center'].includes(name)) {
            await page.getByRole('button', { name: 'Generate evidence draft' }).click();
            await expect(page.getByRole('button', { name: 'Download Excel', exact: true })).toBeVisible();
            const kind = name.startsWith('DSR') ? 'DSR' : 'TCR';
            const count = requests.length;
            await page.getByRole('combobox', { name: new RegExp(`^Preserved ${kind} evidence`) }).selectOption(`saved-${kind}`);
            await expect(page.getByRole('button', { name: 'Download Excel', exact: true })).toBeVisible();
            expect(requests.length).toBe(count);
            await expect(page.getByText(/Category: Targeted Regression; current outcome: passed/)).toBeVisible();
        }
    }
    expect(requests.map(r => r.kind)).toEqual(['DSR', 'TCR']);
    for (const r of requests) { expect(r.category).toBe('Targeted Regression'); expect(r.outcome).toBe('passed'); expect(r.baselineFrom).toBe('2026-08-01T17:00:00Z'); }
    for (const segment of ['/analytics/', '/evidence/', '/activity/']) expect(calls.some(u => u.pathname.includes(segment) && u.searchParams.get('category') === 'Targeted Regression' && u.searchParams.get('outcome') === 'passed')).toBeTruthy();
    expect(calls.some(u => u.searchParams.get('format') === 'pdf')).toBeTruthy();
});
