import { test, expect } from './support/test';
import { APP_ROUTES } from './support/routes';

const MOBILE_PROJECTS = ['mobile-s21', 'mobile-s23'];

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

  for (const route of APP_ROUTES.filter((r) => r.navLabel)) {
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
        const nav = page.getByRole('navigation', { name: 'Main navigation' });
        if (route.navLabel === 'Configuration') {
          await page.getByRole('link', { name: 'Configuration' }).click();
        } else if ((await nav.locator('a', { hasText: route.navLabel }).count()) > 0) {
          const link = nav.locator('a', { hasText: route.navLabel });
          await expect(link).toBeVisible();
          await link.click();
        } else {
          // Lower-traffic pages live in the Markets / Insights menus.
          let opened = false;
          for (const group of ['Markets menu', 'Insights menu']) {
            await nav.getByRole('button', { name: group }).click();
            const item = page.getByRole('menuitem', { name: route.navLabel });
            if ((await item.count()) > 0) {
              await item.click();
              opened = true;
              break;
            }
            await page.keyboard.press('Escape');
          }
          expect(opened, `${route.navLabel} found in a navigation menu`).toBe(true);
        }
      }

      await expect(page).toHaveURL(new RegExp(`${route.path}$`));
      await expect(route.landmark(page)).toBeVisible();
      expect(errors, 'runtime errors during navigation').toEqual([]);
    });
  }
});
