import { test, expect } from './support/test';

test.describe('audit page', () => {
  test('lists events and expands a row to show old → new values', async ({ page, errors }) => {
    await page.goto('/audit');
    await expect(page.getByRole('heading', { level: 1, name: 'Audit', exact: true })).toBeVisible();

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

  test('filters start collapsed on phones, expanded elsewhere', async ({ page }, testInfo) => {
    await page.goto('/audit');
    await expect(page.getByRole('heading', { level: 1, name: 'Audit', exact: true })).toBeVisible();
    const category = page.getByLabel('Category');
    if (testInfo.project.name.startsWith('mobile')) {
      await expect(category).toBeHidden();
      await page.getByRole('button', { name: /Filters/ }).click();
      await expect(category).toBeVisible();
    } else {
      await expect(category).toBeVisible();
    }
  });

  test('admin can select entries and must confirm before deleting', async ({ page }) => {
    await page.goto('/audit');
    const checkbox = page.getByRole('checkbox', { name: 'Select audit entry' }).locator('visible=true').first();
    await checkbox.check();
    await expect(page.getByText('1 selected')).toBeVisible();
    await page.getByRole('button', { name: /Delete selected/ }).click();
    await expect(page.getByRole('dialog')).toContainText('Permanently delete 1 selected audit entry');
    await page.getByRole('dialog').getByRole('button', { name: 'Cancel' }).click();
    await expect(page.getByRole('dialog')).toBeHidden();
    await expect(page.getByText('1 selected')).toBeVisible();
  });

  test('filters are reachable and Clear filters appears after a change', async ({ page }) => {
    await page.goto('/audit');
    await page.locator('tr.audit-row:visible, article.audit-card:visible').first().waitFor();
    const toggles = page.locator('mat-button-toggle', { hasText: '30 days' });
    if (!(await toggles.isVisible())) await page.getByRole('button', { name: /Filters/ }).click();
    await toggles.locator('button').click();
    await expect(page.getByRole('button', { name: /Clear filters/ })).toBeVisible();
    const viewportWidth = page.viewportSize()!.width;
    const box = await page.getByLabel('Search', { exact: true }).boundingBox();
    expect(box!.x + box!.width).toBeLessThanOrEqual(viewportWidth + 1);
  });
});
