import { test, expect } from './support/test';

const isPhone = (name: string) => name.startsWith('mobile');

/** Reads a column's cells as numbers; the em dash for unavailable becomes null. */
async function column(page: import('@playwright/test').Page, col: string): Promise<(number | null)[]> {
  const texts = await page.locator(`table.mat-mdc-table tr.mat-mdc-row td.mat-column-${col}`).allInnerTexts();
  return texts.map((t) => {
    const m = t.replace(/\s+/g, ' ').match(/-?\d[\d,]*\.?\d*/);
    return m ? Number(m[0].replace(/,/g, '')) : null;
  });
}

/** Direction of the numeric run, requiring every unavailable value to come last; 'invalid' otherwise. */
function direction(values: (number | null)[]): 'asc' | 'desc' | 'invalid' {
  const firstNull = values.findIndex((v) => v === null);
  const numeric = (firstNull === -1 ? values : values.slice(0, firstNull)) as number[];
  if (!values.slice(firstNull === -1 ? values.length : firstNull).every((v) => v === null)) return 'invalid';
  const asc = [...numeric].sort((a, b) => a - b);
  if (JSON.stringify(numeric) === JSON.stringify(asc)) return 'asc';
  return JSON.stringify(numeric) === JSON.stringify([...asc].reverse()) ? 'desc' : 'invalid';
}

test.describe('PM-034 forward dividend columns', () => {
  for (const route of [
    { path: '/watchlist', sym: 'ABX.TO', zeroSym: 'COST', missingSym: 'SHOP', perShare: '0.40', yieldPct: '1.52%' },
    { path: '/portfolio', sym: 'ENB.TO', zeroSym: 'NVDA', missingSym: 'XEQT.TO', perShare: '3.88', yieldPct: '5.65%' },
  ]) {
    test(`${route.path}: shows estimates, confirmed zero and unavailable distinctly, and sorts with unavailable last`, async ({ page, errors }, testInfo) => {
      test.skip(isPhone(testInfo.project.name), 'phones render cards instead of the grid');
      await page.goto(route.path);
      const rows = page.locator('table.mat-mdc-table tr.mat-mdc-row');
      await expect(rows.first()).toBeAttached();

      await expect(page.locator('th.mat-column-fwdDivPerShare')).toContainText('FWD DIV/SHARE');
      await expect(page.locator('th.mat-column-fwdYieldPct')).toContainText('FWD YIELD %');

      const row = (sym: string) => rows.filter({ hasText: sym }).first();
      await expect(row(route.sym).locator('td.mat-column-fwdDivPerShare')).toContainText(route.perShare);
      await expect(row(route.sym).locator('td.mat-column-fwdYieldPct')).toContainText(route.yieldPct);
      await expect(row(route.zeroSym).locator('td.mat-column-fwdYieldPct')).toContainText('0.00%');
      await expect(row(route.missingSym).locator('td.mat-column-fwdYieldPct')).toHaveText('—');

      for (const col of ['fwdYieldPct', 'fwdDivPerShare']) {
        const header = page.locator(`th.mat-column-${col}`);
        await header.click();
        await expect.poll(async () => direction(await column(page, col)), `${col} first click`).not.toBe('invalid');
        const firstDir = direction(await column(page, col));
        await header.click();
        await expect
          .poll(async () => direction(await column(page, col)), `${col} second click reverses`)
          .toBe(firstDir === 'asc' ? 'desc' : 'asc');
      }
      expect(errors, 'runtime errors').toEqual([]);
    });

    test(`${route.path}: unavailable estimates explain themselves for keyboard users`, async ({ page }, testInfo) => {
      test.skip(isPhone(testInfo.project.name), 'phones render cards instead of the grid');
      await page.goto(route.path);
      const cell = page.locator('table.mat-mdc-table tr.mat-mdc-row').filter({ hasText: route.missingSym }).first().locator('td.mat-column-fwdYieldPct .fwd');
      await cell.focus();
      await expect(cell).toBeFocused();
      await expect(cell).toHaveAttribute('aria-label', 'Forward dividend unavailable');
    });
  }
});
