import { expect, test as setup } from '@playwright/test';

/**
 * Signs in once and stores the session for the device projects. With PM_LIVE_EMAIL / PM_LIVE_PASSWORD it fills the
 * form; otherwise it waits (up to 5 minutes) for YOU to sign in in the opened browser window.
 */
setup('sign in', async ({ page }) => {
  setup.setTimeout(330_000);
  await page.goto('/login');
  const email = process.env['PM_LIVE_EMAIL'];
  const password = process.env['PM_LIVE_PASSWORD'];
  if (email && password) {
    await page.getByLabel('Email').fill(email);
    await page.getByLabel('Password').fill(password);
    await page.getByRole('button', { name: /sign in/i }).click();
  } else {
    console.log('>>> Sign in manually in the opened browser window (waiting up to 5 minutes)…');
  }
  await expect(page).not.toHaveURL(/\/login/, { timeout: 300_000 });
  await expect(page.getByRole('navigation', { name: 'Main navigation' })).toBeVisible({ timeout: 60_000 });
  await page.context().storageState({ path: '.auth/live.json' });
});
