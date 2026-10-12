import { test, expect } from './support/test';
import { APP_ROUTES } from './support/routes';

const MOBILE_PROJECTS = ['mobile-pixel', 'mobile-iphone'];

/**
 * The shell shows desktop toolbar links on wide screens and a hamburger-driven sidenav on small
 * ones. Tablet sits on the breakpoint boundary, so it adapts: whichever navigation is visible
 * must work.
 */
test.describe('navigation', () => {
  test('exposes the navigation style expected for the viewport', async ({ page }, testInfo) => {
    await page.goto('/dashboard');
    const hamburger = page.getByRole('button', { name: 'Open navigation menu' });
    const toolbarNav = page.getByRole('navigation', { name: 'Main navigation' });

    if (testInfo.project.name === 'desktop-chrome') {
      await expect(toolbarNav).toBeVisible();
      await expect(hamburger).toBeHidden();
    } else if (MOBILE_PROJECTS.includes(testInfo.project.name)) {
      await expect(hamburger).toBeVisible();
    } else {
      await expect(hamburger.or(toolbarNav)).toBeVisible();
    }
  });

  for (const route of APP_ROUTES) {
    test(`navigates to ${route.navLabel}`, async ({ page, errors }) => {
      await page.goto('/dashboard');
      await expect(page.getByRole('heading', { level: 1, name: 'Dashboard' })).toBeVisible();
      const hamburger = page.getByRole('button', { name: 'Open navigation menu' });

      if (await hamburger.isVisible()) {
        await hamburger.click();
        const sidenav = page.getByRole('navigation', { name: 'App navigation' });
        const link = sidenav.locator('a', { hasText: route.navLabel });
        await expect(link).toBeVisible();
        await link.click();
        // Selecting a destination closes the drawer so it never covers the page.
        await expect(link).toBeHidden();
      } else {
        const link = page
          .getByRole('navigation', { name: 'Main navigation' })
          .locator('a', { hasText: route.navLabel });
        await expect(link).toBeVisible();
        await link.click();
      }

      await expect(page).toHaveURL(new RegExp(`${route.path}$`));
      await expect(route.landmark(page)).toBeVisible();
      expect(errors, 'runtime errors during navigation').toEqual([]);
    });
  }
});
