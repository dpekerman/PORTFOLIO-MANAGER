import type { Page } from '@playwright/test';
import { swingState } from './fixtures/swing';
import { expectNoHorizontalPageOverflow, settle } from './support/overflow';
import { expect, test } from './support/test';

const isPhone = (name: string) => name.startsWith('mobile');

/** PM-025 to PM-028. API is mocked; no write is ever sent to a real backend. */
test.describe('PM-025 Swing To Target % and Target Reached', () => {
  const rows = (page: Page) => page.locator('[data-testid="open-trades-table"] tr.mat-mdc-row');
  const rowFor = (page: Page, symbol: string) => rows(page).filter({ hasText: symbol }).first();

  async function open(page: Page) {
    await page.goto('/swing-trading');
    await expect(rows(page).first()).toBeAttached();
    await settle(page);
  }

  test('shows a signed To Target % and no reached flag below target', async ({ page }, info) => {
    test.skip(isPhone(info.project.name), 'To Target % is a secondary column, shown on phone cards via Show all fields');
    await open(page);
    const novo = rowFor(page, 'NOVO.TO');
    await expect(novo.getByTestId('to-target-value')).toHaveText('+16.67%');
    await expect(novo.getByTestId('target-flag')).toHaveCount(0);
    await expect(rowFor(page, 'DIS.TO').getByTestId('to-target-value')).toHaveText('—');
  });

  test('flags Target reached (observation only) with a negative percentage when the price is above target', async ({ page }, info) => {
    test.skip(isPhone(info.project.name), 'Secondary column; covered on desktop/tablet and by unit tests');
    const novo = swingState.rows.find((r) => r.symbol === 'NOVO.TO')!;
    novo.currentPrice = 25;
    await open(page);
    const row = rowFor(page, 'NOVO.TO');
    await expect(row.getByTestId('to-target-value')).toHaveText('-16.00%');
    const flag = row.getByTestId('target-flag');
    await expect(flag).toContainText('Target reached');
    await expect(flag).toHaveAttribute('aria-label', /does not show that a sell order was placed or filled/);
  });

  test('shows Unverified, with no percentage, when the price is stale', async ({ page }, info) => {
    test.skip(isPhone(info.project.name), 'Secondary column; covered on desktop/tablet and by unit tests');
    const novo = swingState.rows.find((r) => r.symbol === 'NOVO.TO')!;
    novo.currentPrice = 25;
    novo.priceBasis = 'Stale';
    await open(page);
    const row = rowFor(page, 'NOVO.TO');
    await expect(row.getByTestId('to-target-value')).toHaveText('—');
    await expect(row.getByTestId('target-flag')).toContainText('Unverified');
  });

  test('sorts To Target % numerically and keeps the Manage Risk action compact and labelled', async ({ page }, info) => {
    await open(page);
    if (!isPhone(info.project.name)) {
      const header = page.getByRole('columnheader', { name: 'To Target %' });
      await header.click();
      await expect(header).toHaveAttribute('aria-sort', 'ascending');
      await expect(rows(page).first()).toContainText('NOVO.TO');
      const btn = rowFor(page, 'DIS.TO').getByTestId('manage-risk-btn');
      await expect(btn).toHaveAccessibleName(/Manage risk for DIS\.TO/);
      expect((await btn.boundingBox())!.height).toBeLessThanOrEqual(32);
    }
    await expectNoHorizontalPageOverflow(page);
  });
});

test.describe('PM-026 Signal Tracker Close Position', () => {
  async function open(page: Page) {
    await page.goto('/signal-tracker');
    await expect(page.locator('tr.signal-row:visible').first()).toBeVisible();
    await settle(page);
  }
  const rowFor = (page: Page, symbol: string) => page.locator('tr.signal-row:visible', { hasText: symbol }).first();

  test('is offered only for a security with an open holding', async ({ page }) => {
    await open(page);
    await expect(rowFor(page, 'MSFT').getByTestId('signal-close-position-btn')).toBeVisible();
    await expect(rowFor(page, 'LMT.TO').getByTestId('signal-close-position-btn')).toHaveCount(0);
    await expect(rowFor(page, 'LMT.TO').getByRole('button', { name: /Add position for LMT\.TO/ })).toBeVisible();
  });

  test('opens the existing close dialog and writes nothing when cancelled', async ({ page }) => {
    const writes: string[] = [];
    page.on('request', (r) => {
      if (['PUT', 'POST', 'DELETE', 'PATCH'].includes(r.method()) && r.url().includes('/api/portfolio')) writes.push(`${r.method()} ${r.url()}`);
    });
    await open(page);
    await rowFor(page, 'MSFT').getByTestId('signal-close-position-btn').click();
    const dialog = page.locator('mat-dialog-container');
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText('MSFT');
    await page.keyboard.press('Escape');
    await expect(dialog).toHaveCount(0);
    expect(writes).toEqual([]);
  });

  test('Record, Dismiss, + Position and Close Position share one compact height', async ({ page }, info) => {
    test.skip(isPhone(info.project.name), 'Phones use touch-sized buttons');
    await open(page);
    const row = rowFor(page, 'MSFT');
    const heights = await row.locator('.action-btn').evaluateAll((els) => els.map((e) => Math.round(e.getBoundingClientRect().height)));
    expect(heights.length).toBeGreaterThanOrEqual(3);
    expect(new Set(heights).size).toBe(1);
    expect(heights[0]).toBeLessThanOrEqual(32);
    await expectNoHorizontalPageOverflow(page);
  });
});

test.describe('PM-027 Transactions default sort', () => {
  test('defaults to Trans Date, newest first, and keeps it across filter changes', async ({ page }, info) => {
    test.skip(isPhone(info.project.name), 'Phones sort through the card controls');
    await page.goto('/transactions');
    const table = page.locator('table.mat-mdc-table').first();
    await expect(table.locator('tr.mat-mdc-row').first()).toBeAttached();
    const header = table.getByRole('columnheader', { name: 'TRANS DATE' });
    await expect(header).toHaveAttribute('aria-sort', 'descending');

    const dates = async () => table.locator('td.mat-column-tx_trans_date').allInnerTexts();
    for (const tab of ['All', 'Open', 'Closed']) {
      await page.getByRole('radio', { name: tab, exact: true }).click();
      await expect(header).toHaveAttribute('aria-sort', 'descending');
      const values = (await dates()).map((d) => d.trim()).filter((d) => /^\d{4}-\d{2}-\d{2}$/.test(d));
      expect(values.length).toBeGreaterThan(0);
      expect(values).toEqual([...values].sort().reverse());
    }
  });
});

test.describe('PM-028 Watchlist default tab', () => {
  test('opens on All and keeps Active selectable', async ({ page }) => {
    await page.goto('/watchlist');
    const all = page.getByRole('radio', { name: /^All/ });
    await expect(all).toBeChecked();
    await page.getByRole('radio', { name: /^Active/ }).click();
    await expect(page.getByRole('radio', { name: /^Active/ })).toBeChecked();
    await expect(all).not.toBeChecked();
  });
});
