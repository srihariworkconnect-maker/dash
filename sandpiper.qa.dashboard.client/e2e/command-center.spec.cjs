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

test('all eight modules keep shared dates; changing plan resets incompatible scope', async ({ page }) => {
    const errors = []; page.on('pageerror', e => errors.push(e.message));
    await page.goto('/'); await expect(page.getByRole('heading', { name: 'Current execution snapshot' })).toBeVisible();
    await page.getByLabel('Repeat baseline start').fill('2026-10-01T00:00:00Z');
    await page.getByLabel('Suite subtree', { exact: true }).selectOption('2');
    await page.getByLabel('Tester', { exact: true }).selectOption('actor-1');
    for (const name of ['Test Explorer', 'Live TCM', 'Productivity Tracker', 'DSR Center', 'TCR Center', 'Execution Intelligence', 'Quality Intelligence', 'Overview']) {
        await nav(page, name).click(); await expect(page.locator('.cc-nav button.selected')).toContainText(name);
        await expect(page.getByLabel('Repeat baseline start')).toHaveValue('2026-10-01T00:00:00Z');
    }
    await page.getByLabel('Test plan', { exact: true }).selectOption('839718');
    await expect(page.getByLabel('Suite subtree', { exact: true })).toHaveValue('');
    await expect(page.getByLabel('Tester', { exact: true })).toHaveValue('');
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
