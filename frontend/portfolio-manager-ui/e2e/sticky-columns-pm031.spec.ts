import { test, expect } from './support/test';

/** PM-031: Signal Tracker keeps Ticker and Role frozen while the grid scrolls sideways (desktop; handsets use cards). */
test.describe('PM-031 sticky identity columns', () => {
  test('signal tracker ticker and role columns are sticky and opaque', async ({ page }, info) => {
    test.skip(info.project.name !== 'desktop-chrome', 'handsets render the grid as cards');
    await page.goto('/signal-tracker');
    const table = page.locator('table.signals-table');
    await expect(table.locator('tr.mat-mdc-row').first()).toBeAttached();

    for (const cls of ['mat-column-symbol', 'mat-column-role']) {
      for (const cell of [table.locator(`th.${cls}`).first(), table.locator(`td.${cls}`).first()]) {
        const style = await cell.evaluate((el) => {
          const s = getComputedStyle(el);
          return { position: s.position, background: s.backgroundColor };
        });
        expect(style.position).toBe('sticky');
        expect(style.background).not.toBe('rgba(0, 0, 0, 0)');
      }
    }
  });
});
