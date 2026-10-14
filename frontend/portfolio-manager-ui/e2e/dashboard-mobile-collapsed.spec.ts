import type { Page } from '@playwright/test';
import { expect, test } from './support/test';
import { expectNoHorizontalPageOverflow, settle } from './support/overflow';

async function openDashboard(page: Page): Promise<void> {
  await page.goto('/dashboard');
  await expect(page.getByRole('heading', { level: 1, name: 'Dashboard' })).toBeVisible();
  await expect(page.getByText('Portfolio value', { exact: true })).toBeVisible();
  await settle(page);
}

const toggles = (page: Page) => page.locator('.db-page button[aria-label^="Toggle"]');

test.describe('dashboard collapsed-first on phones', () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(!testInfo.project.name.startsWith('mobile'), 'phones only');
  });

  test('opens with every section collapsed and the summary visible', async ({ page }) => {
    await openDashboard(page);
    const count = await toggles(page).count();
    expect(count).toBeGreaterThanOrEqual(10);
    for (let i = 0; i < count; i++) {
      const t = toggles(page).nth(i);
      if (await t.isVisible()) await expect(t).toHaveAttribute('aria-expanded', 'false');
    }
    const viewportH = page.viewportSize()!.height;
    const thisMonth = page.locator('.db-metric', { hasText: 'This month' });
    const box = (await thisMonth.boundingBox())!;
    expect(box.y + box.height).toBeLessThanOrEqual(viewportH);
    await expect(page.getByRole('button', { name: 'Refresh dashboard' })).toBeVisible();
    await expectNoHorizontalPageOverflow(page);
  });

  test('has no "Click ... to view" hint text', async ({ page }) => {
    await openDashboard(page);
    await expect(page.locator('.db-collapsed-hint')).toHaveCount(0);
    await expect(page.getByText(/Click .* to view/)).toHaveCount(0);
  });

  test('every chevron is pinned to the right edge of its card', async ({ page }) => {
    await openDashboard(page);
    const count = await toggles(page).count();
    for (let i = 0; i < count; i++) {
      const t = toggles(page).nth(i);
      if (!(await t.isVisible())) continue;
      const card = t.locator('xpath=ancestor::mat-card[1]');
      const cardBox = (await card.boundingBox())!;
      const box = (await t.boundingBox())!;
      const label = await t.getAttribute('aria-label');
      expect(cardBox.x + cardBox.width - (box.x + box.width), label ?? '').toBeLessThanOrEqual(20);
      // Collapsed cards are a single header row: the chevron never drops onto a second line.
      expect(cardBox.height, label ?? '').toBeLessThanOrEqual(box.height + 40);
    }
  });

  test('tapping a chevron expands, collapses and remembers the choice', async ({ page }) => {
    await openDashboard(page);
    const t = page.getByRole('button', { name: 'Toggle indices' });
    await t.tap();
    await expect(t).toHaveAttribute('aria-expanded', 'true');
    await expect(page.locator('.db-index-row').first()).toBeVisible();
    await page.reload();
    await expect(page.getByRole('button', { name: 'Toggle indices' })).toHaveAttribute(
      'aria-expanded',
      'true',
    );
    await page.getByRole('button', { name: 'Toggle indices' }).tap();
    await expect(page.locator('.db-index-row')).toHaveCount(0);
  });

  test('keeps the range and mover controls out of the collapsed headers', async ({ page }) => {
    await openDashboard(page);
    await expect(page.locator('.db-signal-summary, .db-signal-row')).toBeHidden();
    await expect(page.locator('.eod-ownership-legend')).toBeHidden();
    await page.getByRole('button', { name: 'Toggle signal details' }).tap();
    await expect(page.locator('.db-signal-summary')).toBeVisible();
    await expect(page.locator('.db-range-group')).toBeHidden();
    await expect(page.locator('.movers-count-sel')).toBeHidden();
  });
});

test.describe('dashboard defaults off-phone', () => {
  test('tablet and desktop keep their previous defaults', async ({ page }, testInfo) => {
    test.skip(testInfo.project.name.startsWith('mobile'), 'not phones');
    await openDashboard(page);
    await expect(page.getByRole('button', { name: 'Toggle indices' })).toHaveAttribute(
      'aria-expanded',
      'true',
    );
    await expect(page.getByRole('button', { name: 'Toggle market leadership' })).toHaveAttribute(
      'aria-expanded',
      'false',
    );
  });
});
