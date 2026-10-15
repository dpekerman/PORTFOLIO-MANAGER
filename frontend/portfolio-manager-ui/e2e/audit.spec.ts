import { test, expect } from './support/test';

test.describe('audit page', () => {
  test('lists events and expands a row to show old → new values', async ({ page, errors }) => {
    await page.goto('/audit');
    await expect(page.getByRole('heading', { level: 1, name: 'Audit Log' })).toBeVisible();

    // Desktop shows the table, handsets show cards; exactly one is visible.
    const row = page.locator('tr.audit-row:visible, article.audit-card:visible').first();
    await expect(row).toBeVisible();
    const summary = page
      .locator('.summary-text:visible, .card-summary:visible', { hasText: 'Shares 100' })
      .first();
    await expect(summary).toBeVisible();

    await summary.click();
    await expect(page.locator('.diff-old:visible', { hasText: '100' }).first()).toBeVisible();
    await expect(page.locator('.diff-new:visible', { hasText: '120' }).first()).toBeVisible();
    expect(errors, 'runtime errors on the audit page').toEqual([]);
  });

  test('filters are reachable and Clear filters appears after a change', async ({ page }) => {
    await page.goto('/audit');
    await page.locator('mat-button-toggle', { hasText: '30 days' }).locator('button').click();
    await expect(page.getByRole('button', { name: /Clear filters/ })).toBeVisible();
    const viewportWidth = page.viewportSize()!.width;
    const box = await page.getByLabel(/Search symbol/).boundingBox();
    expect(box!.x + box!.width).toBeLessThanOrEqual(viewportWidth + 1);
  });
});
