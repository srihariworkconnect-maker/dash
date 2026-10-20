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
    await expect(kpi(page, 'Re-executed Test Cases').locator('b')).toHaveText('1');
    await expect(kpi(page, 'Release Readiness').locator('b')).toHaveText('Not Assessed');
    await page.screenshot({ path: 'test-results/overview-kpi-enhancement.png', fullPage: true });
    await kpi(page, 'Executed Test Cases').click(); await expect(page.getByRole('complementary', { name: 'KPI evidence' })).toContainText('1 records');
    await page.getByRole('button', { name: 'Close KPI evidence' }).click();
    await kpi(page, 'Open Defects').click(); const detail = page.getByRole('complementary', { name: 'KPI evidence' });
    await expect(detail).toContainText('1 records'); await expect(detail.getByRole('link', { name: /9001/ })).toBeVisible();
    await page.getByRole('button', { name: 'Close KPI evidence' }).click();
    await kpi(page, 'Re-executed Test Cases').click(); await detail.getByRole('button', { name: '1001', exact: true }).click();
    await expect(detail.getByRole('heading', { name: 'Case 1001 · baseline attempts' })).toBeVisible(); await expect(detail).toContainText('4 records');
});

test('incomplete null evidence stays incomplete while a genuine verified zero is displayed', async ({ page }) => {
    const data = businessFixture(); data.activity.complete = false; data.activity.caseRetesting.reExecutedTestCases = null; data.activity.caseRetesting.state = 'Incomplete';
    data.quality.relationshipsComplete = false; data.quality.totalLinkedDefects = null; data.quality.openLinkedDefects = null; data.quality.evidence.state = 'Incomplete';
    await installBusiness(page, data);
    await expect(kpi(page, 'Total Defects').locator('b')).toHaveText('Incomplete evidence');
    await expect(kpi(page, 'Open Defects').locator('b')).toHaveText('Incomplete evidence');
    await expect(kpi(page, 'Re-executed Test Cases').locator('b')).toHaveText('Incomplete evidence');
    data.activity.complete = true; data.activity.caseRetesting = { reExecutedTestCases: 0, knownReExecutedTestCases: 0, missingCaseMappings: 0, state: 'Verified', cases: [] };
    data.quality.relationshipsComplete = true; data.quality.totalLinkedDefects = 0; data.quality.openLinkedDefects = 0; data.quality.evidence.state = 'Verified'; data.quality.defects = [];
    await page.getByRole('button', { name: 'Reload KPI evidence' }).click();
    await expect(kpi(page, 'Total Defects').locator('b')).toHaveText('0');
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
