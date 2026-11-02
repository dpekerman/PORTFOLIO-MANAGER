import { test, expect } from './support/test';
import { ALL_ROUTES } from './support/routes';
import { settle } from './support/overflow';

/**
 * Every route boots with mocked data, shows its own landmark and raises no runtime errors.
 * This is the cheapest, widest net for a template/store regression during the refactor.
 */
for (const route of ALL_ROUTES) {
  test(`smoke: ${route.path} renders without errors`, async ({ page, errors }) => {
    await page.goto(route.path);
    await expect(route.landmark(page)).toBeVisible();
    if (route.inLayout) {
      await expect(page.getByRole('main')).toBeVisible();
      await expect(page).toHaveURL(new RegExp(`${route.path}$`));
    }
    await settle(page);
    expect(errors, 'uncaught exceptions / console.error').toEqual([]);
  });
}
