import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

test.describe('Login page', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/auth/setup-required', async (route) => {
      await route.fulfill({ json: { setupRequired: false } });
    });
    await page.route('**/api/auth/refresh', async (route) => {
      await route.fulfill({ status: 401, json: { message: 'Not authenticated' } });
    });
    await page.goto('/login');
    await expect(page.getByRole('main')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Portfolio Manager' })).toBeVisible();
  });

  test('renders the sign-in form without horizontal overflow', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Portfolio Manager' })).toBeVisible();
    await expect(page.getByLabel('Email')).toBeVisible();
    await expect(page.getByRole('textbox', { name: 'Password' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Sign In' })).toBeVisible();

    const hasHorizontalOverflow = await page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
    );
    expect(hasHorizontalOverflow).toBe(false);
  });

  test('has no automatically detectable accessibility violations', async ({ page }) => {
    const results = await new AxeBuilder({ page }).analyze();
    expect(results.violations).toEqual([]);
  });
});
