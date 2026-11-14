import { readFileSync } from 'node:fs';
import { test, expect } from './support/test';

const isPhone = (name: string) => name.startsWith('mobile');

test.describe('transactions: role, industry and CSV', () => {
  test('grid shows Role and Industry columns', async ({ page }, testInfo) => {
    test.skip(isPhone(testInfo.project.name), 'secondary columns are behind "All fields" on phones');
    await page.goto('/transactions');
    await expect(page.getByRole('columnheader', { name: 'ROLE' }).first()).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'INDUSTRY' }).first()).toBeVisible();
  });

  test('CSV export adds Decision Source - Closed, Role and Industry', async ({ page }) => {
    await page.goto('/transactions');
    const download = page.waitForEvent('download');
    await page.getByRole('button', { name: /Export CSV/ }).first().click();
    const path = await (await download).path();
    const csv = readFileSync(path, 'utf8');
    const header = csv.split('\n')[0];
    expect(header).toContain('Decision Source,Decision Source - Closed,Role,Industry,Age (days)');
    // every data row has the same number of columns as the header (values with commas are quoted)
    const cols = (line: string) => line.match(/("([^"]|"")*"|[^,]*)(,|$)/g)!.length - 1;
    const rows = csv.trim().split('\n').slice(1);
    expect(rows.length).toBeGreaterThan(0);
    for (const row of rows) expect(cols(row)).toBe(cols(header));
  });
});

test.describe('watchlist: duplicates and 5D / 1M change', () => {
  test('5D % and 1M % sit to the right of CHANGE %', async ({ page }, testInfo) => {
    test.skip(isPhone(testInfo.project.name), 'secondary columns are behind "All fields" on phones');
    await page.goto('/watchlist');
    const headers = page.locator('table.mat-mdc-table th.mat-mdc-header-cell:visible');
    await expect(headers.first()).toBeVisible();
    const texts = (await headers.allTextContents()).map((t) => t.trim());
    const i = texts.findIndex((t) => t.startsWith('CHANGE %'));
    expect(i).toBeGreaterThan(-1);
    expect(texts[i + 1]).toContain('5D %');
    expect(texts[i + 2]).toContain('1M %');
    await expect(page.locator('td.mat-column-change5d').first()).toContainText('%');
  });

  test('selecting a ticker already on the watchlist is tagged, warned and blocked', async ({ page }) => {
    await page.route(
      (url) => url.pathname === '/api/stocks/search',
      (route) =>
        route.fulfill({
          json: [{ description: 'SHOPIFY INC', displaySymbol: 'SHOP', symbol: 'SHOP', type: 'Common Stock', exchange: 'US' }],
        }),
    );
    await page.goto('/watchlist');
    await page.locator('button').filter({ hasText: /Add Symbol/ }).first().click();
    const dialog = page.getByRole('dialog');
    await dialog.getByRole('combobox', { name: /Symbol/ }).fill('SHOP');
    const option = page.getByRole('option', { name: /SHOP/ });
    await expect(option).toContainText('Already exists');
    await option.click();

    await expect(dialog).toContainText('Already exists');
    await expect(page.locator('.mat-mdc-snack-bar-container')).toContainText('SHOP already exists on your watchlist');
    await expect(dialog.getByRole('button', { name: 'Add', exact: true })).toBeDisabled();
  });
});

test.describe('portfolio stock filters', () => {
  test('Sector is replaced by Role, Final Action and Gain/Loss', async ({ page }) => {
    await page.goto('/portfolio');
    await page.getByRole('button', { name: /^Filters/ }).first().click();
    const bar = page.locator('.grid-filters-bar');
    await expect(bar.getByLabel('Role')).toBeVisible();
    await expect(bar.getByLabel('Final Action')).toBeVisible();
    await expect(bar.getByLabel('Gain/Loss')).toBeVisible();
    await expect(bar.getByLabel('Sector')).toHaveCount(0);
    await expect(bar.getByLabel('Industry')).toBeVisible();
  });

  test('Gain/Loss filter narrows the grid and Clear restores it', async ({ page }) => {
    await page.goto('/portfolio');
    const rows = page.locator('table.portfolio-grid-table tr.mat-mdc-row');
    await expect(rows.first()).toBeAttached();
    let before = 0;
    await expect
      .poll(
        async () => {
          const count = await rows.count();
          const stable = count > 0 && count === before;
          before = count;
          return stable;
        },
        { intervals: [250] },
      )
      .toBe(true);

    await page.getByRole('button', { name: /^Filters/ }).first().click();
    await page.locator('.grid-filters-bar').getByLabel('Gain/Loss').click();
    await page.getByRole('option', { name: 'Loss' }).click();
    await expect.poll(async () => rows.count()).toBeLessThan(before);

    await page.locator('.grid-filters-bar').getByRole('button', { name: /Clear/ }).click();
    await expect.poll(async () => rows.count()).toBe(before);
  });
});
