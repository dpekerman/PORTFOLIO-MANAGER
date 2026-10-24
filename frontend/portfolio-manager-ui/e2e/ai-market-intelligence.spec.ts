import { test, expect } from './support/test';

const row = (page: import('@playwright/test').Page, text: string) =>
  page.locator('tr.mat-mdc-row:visible', { hasText: text }).first();

/** Filters start collapsed on phones. */
async function openFilters(page: import('@playwright/test').Page): Promise<void> {
  const header = page.getByRole('button', { name: /^Filters/ });
  if ((await header.getAttribute('aria-expanded')) === 'false') await header.click();
}

test.describe('AI market intelligence', () => {
  test('report: priority filter, sector overview, detail dialog with evidence links', async ({ page, errors }) => {
    await page.goto('/ai-market-intelligence');
    await expect(page.getByRole('heading', { level: 1, name: 'AI Market Intelligence' })).toBeVisible();

    // Notes are collapsed by default and hold the disclaimer.
    await page.getByText(/Notes and data quality/).click();
    await expect(page.getByText('Decision support only')).toBeVisible();

    // Default filter = needs attention: XOM (HIGH) and NVDA (MEDIUM) show, SHOP.TO (LOW) does not.
    await expect(row(page, 'XOM')).toBeVisible();
    await expect(row(page, 'NVDA')).toBeVisible();
    await expect(page.locator('tr.mat-mdc-row:visible', { hasText: 'SHOP.TO' })).toHaveCount(0);

    // Sector accordion: open a sector to see every impacted security with decision, AI view and source links.
    await openFilters(page);
    await page.getByRole('button', { name: /^Energy/ }).click();
    const block = page.locator('.stock-block', { hasText: 'XOM' });
    await expect(block).toContainText('Decision:');
    await expect(block.getByRole('link', { name: 'XOM announces earnings' })).toHaveAttribute('href', 'https://example.com/A-XOM');
    await expect(block).toContainText('AI view');
    await expect(page.locator('.ev-box').getByRole('link', { name: /Crude oil surges/ })).toBeVisible();

    // Filters apply to events, sectors and securities at once.
    await page.getByLabel('Priority').selectOption('ALL');
    await expect(page.getByRole('button', { name: /^Technology/ })).toBeVisible();
    await page.getByLabel('Search ticker, company, sector').fill('SHOP');
    await expect(page.getByRole('button', { name: /^Energy/ })).toHaveCount(0);
    await expect(row(page, 'SHOP.TO')).toBeVisible();

    // Detail dialog: linked source, AI note flagged as inference.
    await page.getByRole('button', { name: 'Reset filters' }).click();
    await row(page, 'XOM').click();
    const dialog = page.locator('#selected-security');
    const link = dialog.getByRole('link', { name: 'XOM announces earnings' });
    await expect(link).toHaveAttribute('href', 'https://example.com/A-XOM');
    await expect(link).toHaveAttribute('rel', /noopener/);
    await expect(dialog.getByText('AI view').first()).toBeVisible();
    await dialog.getByRole('button', { name: 'Useful', exact: true }).click();
    await expect(page.getByText('Feedback saved')).toBeVisible();
    await dialog.getByRole('button', { name: 'Close' }).click();
    await expect(page.locator('#selected-security')).toHaveCount(0);
    expect(errors, 'runtime errors on the AI page').toEqual([]);
  });

  test('report: global event opens with mechanism and counterargument', async ({ page }) => {
    await page.goto('/ai-market-intelligence');
    await row(page, 'ENERGY SUPPLY').click();
    const dialog = page.locator('#selected-event');
    await expect(dialog.getByText('Counterargument')).toBeVisible();
    await expect(dialog.getByText('Possibly exposed: XOM')).toBeVisible();
  });

  test('sources: pending source must be approved; connectivity can be tested', async ({ page }) => {
    await page.goto('/ai-market-intelligence');
    await page.getByRole('tab', { name: 'Sources' }).click();
    await expect(row(page, 'Proposed feed')).toContainText('Pending approval');
    await expect(page.getByRole('button', { name: 'Approve Proposed feed' })).toBeVisible();
    await expect(row(page, 'Proposed feed').getByRole('switch')).toBeDisabled();
    await page.getByRole('button', { name: 'Test Yahoo Finance company headlines' }).click();
    await expect(page.getByText(/Reachable: 12 articles/)).toBeVisible();
  });

  test('agent: material date and time pickers, run now and test email', async ({ page }) => {
    await page.goto('/ai-market-intelligence');
    await page.getByRole('tab', { name: 'Agent' }).click();
    await expect(page.getByText(/Local AI: Ollama ready/)).toBeVisible();
    for (const label of ['First run date', 'Test as-of date', 'As-of date (blank = today)']) {
      await expect(page.getByLabel(label)).toBeVisible();
    }
    // Time fields use the Material timepicker, as on the Configuration screens.
    await expect(page.getByLabel('Start time (Toronto)')).toHaveValue(/7:00\s?AM/i);
    await page.getByRole('button', { name: 'Open calendar' }).first().click();
    await expect(page.locator('mat-calendar')).toBeVisible();
    await page.locator('mat-calendar .mat-calendar-body-cell:not(.mat-calendar-body-disabled)').first().click();
    await expect(page.locator('mat-calendar')).toHaveCount(0);
    await page.locator('button', { hasText: 'Run now' }).click();
    await expect(page.getByText('Run complete')).toBeVisible();
    await page.getByRole('button', { name: /Send test email/ }).click();
    await expect(page.getByText('Test email sent')).toBeVisible();
  });

  test('agent: discovery modes stay locked until the discovery source is approved', async ({ page }) => {
    await page.goto('/ai-market-intelligence');
    await page.getByRole('tab', { name: 'Agent' }).click();
    await expect(page.getByText('Approve the discovery source in Sources to enable')).toBeVisible();
    await page.getByRole('combobox', { name: 'Discovery mode' }).click();
    await expect(page.getByRole('option', { name: 'SMART_DISCOVERY' })).toHaveAttribute('aria-disabled', 'true');
  });

  test('history: lists reports and evaluates outcomes', async ({ page }) => {
    await page.goto('/ai-market-intelligence');
    await page.getByRole('tab', { name: 'History' }).click();
    await expect(row(page, '2026-10-20')).toBeVisible();
    await page.getByRole('button', { name: 'Outcomes' }).first().click();
    await expect(page.getByText(/Outcomes for 2026-10-20/)).toBeVisible();
    await expect(row(page, 'XOM')).toContainText('3.2%');
  });

  test('no horizontal overflow on any tab', async ({ page }) => {
    await page.goto('/ai-market-intelligence');
    for (const tab of ['Report', 'Sources', 'History', 'Agent']) {
      await page.getByRole('tab', { name: tab }).click();
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
      expect(overflow, `overflow on ${tab}`).toBeLessThanOrEqual(1);
    }
  });
});
