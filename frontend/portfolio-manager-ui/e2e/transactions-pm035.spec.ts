import { readFileSync } from 'node:fs';
import * as XLSX from 'xlsx';
import { expect, test } from './support/test';
import { signalState } from './fixtures/signals-audit';
import { expectNoHorizontalPageOverflow, settle } from './support/overflow';

const isPhone = (name: string) => name.startsWith('mobile');

/** First Material table on the page is the stock-transactions one. */
const stockRows = (page: import('@playwright/test').Page) =>
  page.locator('table.mat-mdc-table').first().locator('tr.mat-mdc-row');

async function pick(page: import('@playwright/test').Page, testId: string, option: string | RegExp) {
  await page.getByTestId(testId).click();
  await page.getByRole('option', { name: option }).click();
}

test.describe('PM-035 transactions filters, totals, SINCE CLOSE', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/transactions');
    await expect(stockRows(page).first()).toBeAttached();
  });

  test('filters have accessible labels and Reset restores the grid', async ({ page }) => {
    await page.getByRole('radio', { name: 'Closed', exact: true }).click();
    await expect(page.getByRole('combobox', { name: 'Role' })).toBeVisible();
    await expect(page.getByRole('combobox', { name: /Decision Source — Close/ })).toBeVisible();
    await expect(page.getByRole('textbox', { name: 'Decision Source' })).toBeVisible();

    const before = await stockRows(page).count();
    await pick(page, 'filter-role', 'Strategic');
    await expect.poll(() => stockRows(page).count()).toBe(2); // ATD.TO + BNS.TO
    await page.getByTestId('reset-filters').click();
    await expect.poll(() => stockRows(page).count()).toBe(before);
    await expect(page.getByTestId('reset-filters')).toHaveCount(0);
  });

  test('combined filters AND together and an empty result shows a reset action', async ({ page }) => {
    await page.getByRole('radio', { name: 'Closed', exact: true }).click();
    await pick(page, 'filter-role', 'Swing');
    await pick(page, 'filter-source-closed', 'Risk Control');
    await expect.poll(() => stockRows(page).count()).toBe(2); // SU.TO + META
    await page.getByTestId('filter-source').fill('no-such-source');
    await expect(page.getByText(/stock\s+transactions match the current filters/)).toBeVisible();
    await page.getByRole('button', { name: 'Reset filters' }).first().click();
    await expect.poll(() => stockRows(page).count()).toBeGreaterThan(2);
  });

  test('the close filter is disabled on OPEN and never hides open rows', async ({ page }) => {
    await page.getByRole('radio', { name: 'Closed', exact: true }).click();
    await pick(page, 'filter-source-closed', 'Risk Control');
    await page.getByRole('radio', { name: 'Open', exact: true }).click();
    await expect(page.getByTestId('filter-source-closed')).toHaveAttribute('aria-disabled', 'true');
    await expect.poll(() => stockRows(page).count()).toBeGreaterThan(0);
    await page.getByRole('radio', { name: 'Closed', exact: true }).click();
    await expect(page.getByTestId('filter-source-closed')).not.toHaveAttribute('aria-disabled', 'true');
  });

  test('totals are per currency and follow the filters', async ({ page }) => {
    await page.getByRole('radio', { name: 'Closed', exact: true }).click();
    const gl = page.getByTestId('stock-total-gl');
    await expect(gl).toHaveCount(2);
    await expect(gl.filter({ hasText: 'CAD realized' })).toHaveCount(1);
    await expect(gl.filter({ hasText: 'USD realized' })).toHaveCount(1);

    await pick(page, 'filter-role', 'Strategic'); // only CAD symbols
    await expect(gl).toHaveCount(1);
    await expect(gl).toContainText('CAD realized');
    await expect(page.locator('td.tx-totals-label').first()).toContainText('TOTALS · 2 records');
  });

  test('OPEN tab shows open DIFF $ totals, not realized Gain/Loss', async ({ page }) => {
    await page.getByRole('radio', { name: 'Open', exact: true }).click();
    await expect(page.getByTestId('stock-total-gl')).toHaveCount(0);
    await expect(page.getByTestId('stock-total-diff-open').first()).toBeVisible();
    await expect(page.getByTestId('stock-total-diff-closed')).toHaveCount(0);
  });

  test('SINCE CLOSE: after AGE, numeric on CLOSED rows, em dash on OPEN, numeric sort', async ({ page }, testInfo) => {
    test.skip(isPhone(testInfo.project.name), 'secondary columns are behind "All fields" on phones');
    const headers = page.locator('table.mat-mdc-table').first().locator('th.mat-mdc-header-cell');
    const texts = (await headers.allTextContents()).map((t) => t.trim());
    const age = texts.findIndex((t) => t.startsWith('AGE'));
    expect(age).toBeGreaterThan(-1);
    expect(texts[age + 1]).toContain('SINCE CLOSE');

    const cells = () =>
      page
        .locator('table.mat-mdc-table')
        .first()
        .getByTestId('since-close')
        .allTextContents()
        .then((a) => a.map((t) => t.trim()));
    const all = await cells();
    expect(all.some((t) => /^\d+d$/.test(t))).toBe(true);
    expect(all.some((t) => t === '—')).toBe(true);

    const header = page.getByRole('columnheader', { name: 'SINCE CLOSE' }).first();
    await header.click(); // asc
    await expect.poll(async () => {
      const n = (await cells()).filter((t) => /^\d+d$/.test(t)).map((t) => parseInt(t, 10));
      return n.every((v, i) => i === 0 || v >= n[i - 1]);
    }).toBe(true);
    const asc = await cells();
    expect(asc.at(-1)).toBe('—'); // not applicable always last
    await header.click(); // desc
    await expect.poll(async () => {
      const n = (await cells()).filter((t) => /^\d+d$/.test(t)).map((t) => parseInt(t, 10));
      return n.every((v, i) => i === 0 || v <= n[i - 1]);
    }).toBe(true);
    expect((await cells()).at(-1)).toBe('—');
  });

  test('CSV export matches the filtered grid and has numeric SINCE CLOSE', async ({ page }) => {
    await page.getByRole('radio', { name: 'Closed', exact: true }).click();
    await pick(page, 'filter-role', 'Strategic');
    await expect.poll(() => stockRows(page).count()).toBe(2);

    const download = page.waitForEvent('download');
    await page.getByRole('button', { name: /Export CSV/ }).click();
    const csv = readFileSync(await (await download).path(), 'utf8');
    const [header, ...rows] = csv.trim().split('\n');
    expect(header).toContain('Age (days),Since Close (days),Currency');
    expect(rows).toHaveLength(2);
    const since = header.split(',').indexOf('Since Close (days)');
    for (const row of rows) {
      expect(row).toContain('Strategic');
      expect(row.split(',')[since]).toMatch(/^\d+$/);
    }
  });

  test('Excel export respects tab and filters with numeric SINCE CLOSE', async ({ page }) => {
    await page.getByRole('radio', { name: 'Closed', exact: true }).click();
    await pick(page, 'filter-role', 'Strategic');
    await expect.poll(() => stockRows(page).count()).toBe(2);

    const download = page.waitForEvent('download');
    await page.getByRole('button', { name: /Export Excel/ }).click();
    const book = XLSX.read(readFileSync(await (await download).path()), { cellDates: true });
    expect(book.SheetNames).toEqual(expect.arrayContaining(['Stocks', 'Filters & Totals']));
    const rows = XLSX.utils.sheet_to_json<Record<string, unknown>>(book.Sheets['Stocks']);
    expect(rows).toHaveLength(2);
    for (const r of rows) {
      expect(r['Type']).toBe('CLOSE');
      expect(r['Role']).toBe('Strategic');
      expect(typeof r['Since Close (days)']).toBe('number');
      expect(typeof r['Gain/Loss']).toBe('number');
    }
    const summary = XLSX.utils.sheet_to_json<string[]>(book.Sheets['Filters & Totals'], { header: 1 });
    expect(summary).toContainEqual(['Role', 'Strategic']);
  });

  test('no horizontal page overflow with the new filter bar', async ({ page }) => {
    await settle(page);
    await expectNoHorizontalPageOverflow(page);
  });
});

test.describe('signal tracker badges on phones (PM-035 mobile fix, compacted by PM-015)', () => {
  test('Signal Type is one label and the Lifecycle badge stays inside its card value column without overlap', async ({
    page,
  }, testInfo) => {
    test.skip(!isPhone(testInfo.project.name), 'phone card layout only');
    signalState.rows[0] = {
      ...signalState.rows[0],
      status: 'Active',
      effectiveState: 'PriorSessionValid',
      isActionable: true,
      origin: 'INITIAL_BASELINE',
    };
    await page.goto('/audit/signals');
    const cell = page.locator('td.mat-column-signalType:visible').first();
    await expect(cell.locator('.sig-type')).toHaveCount(1);
    await expect(cell.locator('.tag')).toHaveCount(0);
    await settle(page);

    const life = page.locator('td.mat-column-lifecycleStatus:visible').first();
    await expect(life.getByTestId('lifecycle-badge')).toHaveText('Active');
    const box = async (loc: import('@playwright/test').Locator) => (await loc.boundingBox())!;
    const cellBox = await box(cell);
    const label = await box(cell.locator('.sig-type'));
    const lifeBox = await box(life);
    const badge = await box(life.getByTestId('lifecycle-badge'));

    // inside the cells, and clear of the left-hand label column (min label width 5.5rem ≈ 88px)
    expect(label.x + label.width).toBeLessThanOrEqual(cellBox.x + cellBox.width + 0.5);
    expect(label.x).toBeGreaterThan(cellBox.x + 60);
    expect(badge.x + badge.width).toBeLessThanOrEqual(lifeBox.x + lifeBox.width + 0.5);
    expect(badge.x).toBeGreaterThan(lifeBox.x + 60);
    // the two cards rows do not intersect vertically
    expect(lifeBox.y).toBeGreaterThanOrEqual(cellBox.y + cellBox.height - 1);
    await expectNoHorizontalPageOverflow(page);
  });
});
