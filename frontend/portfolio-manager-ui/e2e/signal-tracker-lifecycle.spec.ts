import type { Locator, Page, TestInfo } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { seedLifecycleHistory, signalState } from './fixtures/signals-audit';
import { expectNoHorizontalPageOverflow, settle } from './support/overflow';
import { expect, test } from './support/test';

const isPhone = (testInfo: TestInfo) => testInfo.project.name.startsWith('mobile');
const rowsLoc = (page: Page) => page.locator('tr.signal-row:visible');
const symbols = (page: Page) => page.locator('tr.signal-row:visible .symbol').allTextContents();

async function open(page: Page, url = '/signal-tracker') {
  await page.goto(url);
  await expect(page.getByRole('heading', { level: 1, name: 'Signal Tracker' })).toBeVisible();
  await expect(rowsLoc(page).first()).toBeVisible();
}

async function openFilters(page: Page, testInfo: TestInfo) {
  // Filters start collapsed on handsets only, and the header toggles, so open only when the control is hidden.
  if (isPhone(testInfo) && !(await page.getByRole('combobox', { name: 'Status' }).isVisible())) {
    await page.getByRole('button', { name: /Filters/ }).click();
  }
}

async function chooseStatus(page: Page, testInfo: TestInfo, option: string) {
  await openFilters(page, testInfo);
  await page.getByRole('combobox', { name: 'Status' }).click();
  await page.getByRole('option', { name: option, exact: true }).click();
  // Let the select overlay close before the next interaction re-opens it.
  await expect(page.getByRole('listbox')).toHaveCount(0);
}

async function sortBy(page: Page, testInfo: TestInfo, label: string) {
  if (isPhone(testInfo)) {
    await page.locator('app-mobile-table-controls mat-select').click();
    await page.getByRole('option', { name: label, exact: true }).click();
  } else {
    await page.getByRole('columnheader', { name: label }).click();
  }
}

const signalRequests = (page: Page) => {
  const urls: string[] = [];
  page.on('request', (r) => {
    if (/\/api\/signals-audit(\?|$)/.test(r.url())) urls.push(r.url());
  });
  return urls;
};

/** Column order in a rendered row (DOM order is the displayed order, on tables and on phone cards). */
async function cellOrder(row: Locator): Promise<string[]> {
  return row.locator('td').evaluateAll((tds) =>
    tds.map((td) => Array.from(td.classList).find((c) => c.startsWith('mat-column-'))?.replace('mat-column-', '') ?? ''),
  );
}

test.describe('PM-015 Signal Tracker lifecycle view', () => {
  test.beforeEach(() => seedLifecycleHistory());

  test('opens on Active, newest first, with no date window, and lists an Active signal weeks old', async ({ page, errors }) => {
    const urls = signalRequests(page);
    await open(page);

    // FUT (+4d), BMO (2d), LMT (3d), WMT (25d), MSFT (40d): every Active row, newest first.
    expect(await symbols(page)).toEqual(['FUT', 'BMO.TO', 'LMT.TO', 'WMT.TO', 'MSFT']);
    for (const closed of ['ACT.TO', 'DIS.TO', 'INV.TO']) await expect(page.locator('tr.signal-row:visible', { hasText: closed })).toHaveCount(0);

    const first = urls.find((u) => u.includes('pageSize=25'))!;
    expect(first).toContain('status=Active');
    expect(first).toContain('sortBy=signalDate');
    expect(first).toContain('sortDir=desc');
    expect(first).not.toMatch(/[?&]from=\d/);
    expect(first).not.toMatch(/[?&]to=\d/);
    expect(urls.filter((u) => /[?&](from|to)=\d/.test(u))).toEqual([]);
    expect(errors).toEqual([]);
  });

  test('All reveals the historical Acted, Dismissed and Invalidated records; Acted shows only its own', async ({ page }, testInfo) => {
    await open(page);
    await chooseStatus(page, testInfo, 'All');
    await expect(rowsLoc(page)).toHaveCount(8);
    for (const sym of ['ACT.TO', 'DIS.TO', 'INV.TO']) await expect(page.locator('tr.signal-row:visible', { hasText: sym })).toHaveCount(1);

    await chooseStatus(page, testInfo, 'Acted');
    await expect(rowsLoc(page)).toHaveCount(1);
    expect(await symbols(page)).toEqual(['ACT.TO']);
    await expect(rowsLoc(page).first().getByTestId('lifecycle-badge')).toHaveText('Acted');
  });

  test('Signal Type shows one primary label only; lifecycle lives in its own column', async ({ page }) => {
    await open(page);
    const typeCells = page.locator('td.mat-column-signalType:visible');
    const count = await typeCells.count();
    expect(count).toBeGreaterThan(0);
    for (let i = 0; i < count; i++) {
      const cell = typeCells.nth(i);
      await expect(cell.locator('.sig-type')).toHaveCount(1);
      await expect(cell.locator('.tag')).toHaveCount(0);
      await expect(cell).not.toContainText(/Lifecycle:|Current:|Unaudited|Initial baseline|Backfill/);
    }
    await expect(page.getByTestId('lifecycle-tag')).toHaveCount(0);
    await expect(page.getByTestId('effective-tag')).toHaveCount(0);
    await expect(rowsLoc(page).first().getByTestId('lifecycle-badge')).toHaveText('Active');
    await settle(page);
    await expectNoHorizontalPageOverflow(page);
  });

  test('Lifecycle Status sits immediately after Signal Type and before My Action', async ({ page }) => {
    await open(page);
    const order = await cellOrder(rowsLoc(page).first());
    expect(order.indexOf('lifecycleStatus')).toBe(order.indexOf('signalType') + 1);
    expect(order.indexOf('lifecycleStatus')).toBeLessThan(order.indexOf('myAction'));
  });

  test('an existing saved column layout gets Lifecycle right after Signal Type without losing its other choices', async ({ page }) => {
    await page.addInitScript(() => {
      const old = ['symbol', 'signalDate', 'signalType', 'today', 'myAction', 'source'].map((key) => ({ key, visible: true }));
      localStorage.setItem('pm_grid_columns_v1', JSON.stringify({ 'signals-audit': old }));
    });
    await open(page);
    const order = (await cellOrder(rowsLoc(page).first())).filter((c) => c && c !== 'rowActions' && c !== 'colConfig');
    const idx = (c: string) => order.indexOf(c);
    expect(idx('lifecycleStatus')).toBe(idx('signalType') + 1);
    expect(idx('lifecycleStatus')).toBeLessThan(idx('myAction'));
    // the user's own order is kept: today before myAction before source (when those columns are shown).
    if (idx('today') >= 0 && idx('source') >= 0) {
      expect(idx('today')).toBeLessThan(idx('myAction'));
      expect(idx('myAction')).toBeLessThan(idx('source'));
    }
  });

  test('the warning icon shows only for stale or non-actionable Active evidence, never for Unknown + actionable', async ({ page }) => {
    await open(page);
    const warn = (sym: string) => page.locator('tr.signal-row:visible', { hasText: sym }).getByTestId('evidence-warning');

    await expect(warn('BMO.TO')).toHaveCount(1);
    await expect(warn('BMO.TO')).toHaveAttribute('aria-label', /Not currently actionable/);
    await expect(warn('WMT.TO')).toHaveCount(0); // Active + Unknown health + actionable: valid from a prior session
    await expect(warn('LMT.TO')).toHaveCount(0);
    await expect(page.getByTestId('evidence-warning')).toHaveCount(1);
  });

  test('details keep the full lifecycle and evidence story', async ({ page }) => {
    await open(page);
    await page.locator('tr.signal-row:visible', { hasText: 'BMO.TO' }).click();
    const dialog = page.getByRole('dialog');
    const section = dialog.getByTestId('lifecycle-evidence');
    await expect(section).toContainText('Lifecycle & current evidence');
    await expect(section).toContainText('Active');
    await expect(section).toContainText('Awaiting Data');
    await expect(section).toContainText('Stale');
    await expect(section).toContainText('Not currently actionable');
    await expect(section).toContainText('Reason codes');
    await dialog.getByRole('button', { name: 'Close' }).click();

    await page.locator('tr.signal-row:visible', { hasText: 'WMT.TO' }).click();
    const wmt = page.getByRole('dialog').getByTestId('lifecycle-evidence');
    await expect(wmt).toContainText('Unknown');
    await expect(wmt).toContainText('remains valid from its last completed session');
    await expect(page.getByRole('dialog').getByTestId('evidence-warning-text')).toHaveCount(0);
  });

  test('sorting by Lifecycle Status is requested from the server and orders the whole filtered set', async ({ page }, testInfo) => {
    await open(page);
    await chooseStatus(page, testInfo, 'All');
    await expect(rowsLoc(page)).toHaveCount(8);
    const urls = signalRequests(page);
    await sortBy(page, testInfo, 'Lifecycle Status');
    await expect.poll(() => urls.some((u) => u.includes('sortBy=lifecycleStatus'))).toBe(true);

    const last = urls.filter((u) => u.includes('sortBy=lifecycleStatus')).pop()!;
    const dir = new URL(last).searchParams.get('sortDir');
    const badges = await page.locator('tr.signal-row:visible [data-testid="lifecycle-badge"]').allTextContents();
    const expected = [...badges].sort();
    if (dir === 'desc') expected.reverse();
    expect(badges.map((b) => b.trim())).toEqual(expected.map((b) => b.trim()));
  });

  test('the default Signal Date sort is newest first and flips on request', async ({ page }, testInfo) => {
    await open(page);
    const before = await symbols(page);
    if (isPhone(testInfo)) await page.locator('.mtc-dir-btn').click();
    else await page.getByRole('columnheader', { name: 'Signal Date' }).click();
    await expect.poll(async () => (await symbols(page)).join()).not.toBe(before.join());
    expect(await symbols(page)).toEqual([...before].reverse());
  });

  test('Export respects the filters and carries Lifecycle Status', async ({ page }, testInfo) => {
    await open(page);
    await chooseStatus(page, testInfo, 'Acted');
    await expect(rowsLoc(page)).toHaveCount(1);
    await page.getByRole('button', { name: 'Export signals' }).click();
    const download = page.waitForEvent('download');
    await page.getByRole('menuitem', { name: /Export to CSV/ }).click();
    const csv = readFileSync(await (await download).path(), 'utf8').replace(/^\uFEFF/, '').trim().split('\r\n');
    expect(csv[0].split(',')).toContain('Lifecycle Status');
    expect(csv).toHaveLength(2);
    expect(csv[1].startsWith('ACT.TO')).toBe(true);
    expect(csv[1].split(',')[csv[0].split(',').indexOf('Lifecycle Status')]).toBe('Acted');
  });
});

test.describe('PM-015 partial evaluation (more than 5,000 matching signals)', () => {
  test.beforeEach(() => {
    seedLifecycleHistory();
    signalState.largeTotal = 5100;
  });

  test('a complete SQL-backed view shows the exact total, partial KPIs, and no grid warning; a computed sort is labelled Partial and blocks CSV', async ({ page }, testInfo) => {
    await open(page);
    // Exact total stays exact, KPIs are labelled as evaluated over 5,000.
    await expect(page.getByTestId('kpi-partial')).toContainText('Partial — 5,000 of 5,100 evaluated');
    await expect(page.locator('.kpi', { hasText: 'Total Signals' }).locator('.kpi-value')).toHaveText('5100');
    await expect(page.getByTestId('grid-partial')).toHaveCount(0);

    await sortBy(page, testInfo, 'Today vs Signal %');
    await expect(page.getByTestId('grid-partial')).toContainText('ranked within the newest 5,000 of 5,100');

    // CSV is blocked outright: nothing downloads and the message explains how to get a complete result.
    let downloaded = false;
    page.on('download', () => (downloaded = true));
    await page.getByRole('button', { name: 'Export signals' }).click();
    await page.getByRole('menuitem', { name: /Export to CSV/ }).click();
    await expect(page.locator('.mat-mdc-snack-bar-container')).toContainText('CSV export blocked');
    await expect(page.locator('.mat-mdc-snack-bar-container')).toContainText('Narrow the filters');
    expect(downloaded).toBe(false);

    // Excel is allowed but explicitly labelled partial.
    await page.getByRole('button', { name: 'Export signals' }).click();
    const xlsx = page.waitForEvent('download');
    await page.getByRole('menuitem', { name: /Export to Excel/ }).click();
    expect((await xlsx).suggestedFilename()).toMatch(/-partial\.xlsx$/);

    // Back to an SQL-backed sort: complete paging again, the grid warning clears.
    await sortBy(page, testInfo, 'Signal Date');
    await expect(page.getByTestId('grid-partial')).toHaveCount(0);
    await expect(page.getByTestId('kpi-partial')).toBeVisible();
    await settle(page);
    await expectNoHorizontalPageOverflow(page);
  });
});
