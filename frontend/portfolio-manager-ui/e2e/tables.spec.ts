import { test, expect } from './support/test';
import { APP_ROUTES } from './support/routes';

const TOLERANCE_PX = 1;

/**
 * Data-table smoke: fixtures must actually render rows (otherwise overflow checks are
 * meaningless), sorting must not crash, and the actions/filters a phone user relies on must sit
 * inside the viewport without sideways panning.
 */
for (const route of APP_ROUTES.filter((r) => r.hasMatTable)) {
  test.describe(`tables: ${route.path}`, () => {
    test('renders body rows and tolerates sorting', async ({ page, errors }) => {
      await page.goto(route.path);
      await expect(route.landmark(page)).toBeVisible();

      const rows = page.locator('table.mat-mdc-table tr.mat-mdc-row');
      await expect(rows.first()).toBeAttached();
      expect(await rows.count()).toBeGreaterThanOrEqual(2);

      const sortHeader = page.locator('table.mat-mdc-table th.mat-sort-header:visible').first();
      if (await sortHeader.count()) {
        await sortHeader.click();
        await sortHeader.click();
        await expect(route.landmark(page)).toBeVisible();
        await expect(rows.first()).toBeAttached();
      }
      expect(errors, 'runtime errors while sorting').toEqual([]);
    });

    const controls = route.primaryControls;
    if (controls) {
      test('primary controls are reachable without horizontal panning', async ({ page }) => {
        await page.goto(route.path);
        await expect(route.landmark(page)).toBeVisible();
        const viewportWidth = page.viewportSize()!.width;

        for (const control of controls(page)) {
          const target = control.first();
          await expect(target).toBeVisible();
          const box = await target.boundingBox();
          expect(box, 'control has a layout box').not.toBeNull();
          expect(box!.x, `${await describe(target)} left edge`).toBeGreaterThanOrEqual(
            -TOLERANCE_PX,
          );
          expect(box!.x + box!.width, `${await describe(target)} right edge`).toBeLessThanOrEqual(
            viewportWidth + TOLERANCE_PX,
          );
        }
      });
    }
  });
}

async function describe(locator: import('@playwright/test').Locator): Promise<string> {
  return locator.evaluate(
    (el) =>
      el.getAttribute('aria-label') ??
      el.getAttribute('placeholder') ??
      el.textContent?.trim() ??
      el.tagName,
  );
}
