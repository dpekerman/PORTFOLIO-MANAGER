import { test, expect } from './support/test';
import { expectNoHorizontalPageOverflow, settle } from './support/overflow';
import type { AwaitingDataStatusDto } from '../src/app/core/models/portfolio.models';

/** Each config tab opens, shows its own section heading and raises no runtime errors. */
const TABS: { label: RegExp; heading: RegExp }[] = [
  { label: /Demo Mode/, heading: /^Demo Mode$/ },
  { label: /Scanner/, heading: /Scanner Settings/ },
  { label: /EOD Window/, heading: /EOD Confirmation Window/ },
  { label: /Value Screener/, heading: /Value Screener Schedule/ },
  { label: /Alerts/, heading: /RSI Confirmed Signal Alerts/ },
  { label: /Sectors/, heading: /Sectors & Industries/ },
  { label: /Account Types/, heading: /^Account Types$/ },
  { label: /Allocation/, heading: /Allocation & Risk Management/ },
  { label: /Decision Sources/, heading: /Decision Source Picklist/ },
  { label: /Signal Tracker/, heading: /Signal Tracker Settings/ },
  { label: /Portfolio History/, heading: /Portfolio History Reconstruction/ },
  { label: /Automation/, heading: /EOD Automation/ },
  { label: /Users/, heading: /User Management/ },
];

for (const tab of TABS) {
  test(`config tab: ${tab.label}`, async ({ page, errors }) => {
    await page.goto('/config');
    await page.getByRole('tab', { name: tab.label }).click();
    await expect(page.getByRole('heading', { level: 2, name: tab.heading })).toBeVisible();
    await settle(page);
    await expectNoHorizontalPageOverflow(page);
    expect(errors, 'uncaught exceptions / console.error').toEqual([]);
  });
}

test('config: unsaved edits survive switching tabs and Save All reports dirty sections', async ({
  page,
}) => {
  await page.goto('/config');
  const overdue = page.getByLabel('Oversold threshold (≤)');
  await overdue.fill('28');
  await page.getByRole('tab', { name: /EOD Window/ }).click();
  await page.getByRole('tab', { name: /Scanner/ }).click();
  await expect(overdue).toHaveValue('28');
  await expect(page.getByText(/section\(s\) with unsaved changes/)).toBeAttached();
});

test('automation refresh counts identify portfolio rows including closed positions', async ({
  page,
  mocks,
  errors,
}) => {
  const awaitingResponse = page.waitForResponse('**/api/automation/awaiting-data');
  await page.goto('/config');
  await page.getByRole('tab', { name: /Automation/ }).click();
  await expect(page.getByText(/9 portfolio rows \(including closed\)/)).toBeVisible();
  await expect(page.getByText(/10 watchlist rows/)).toBeVisible();
  await (await awaitingResponse).finished();
  await settle(page);
  expect(mocks.unmocked).not.toContain('GET /api/automation/awaiting-data');
  await expect(page.getByRole('button', { name: 'Retry awaiting data' })).toHaveCount(0);
  await expectNoHorizontalPageOverflow(page);
  expect(errors, 'uncaught exceptions / console.error').toEqual([]);
});

test('automation shows Trading Opportunities results and can start a scan manually', async ({
  page,
  errors,
}) => {
  const historyResponse = page.waitForResponse('**/api/opportunities/history');
  await page.goto('/config');
  await page.getByRole('tab', { name: /Automation/ }).click();
  await (await historyResponse).finished();

  const historyTable = page.locator('.automation-history-table');
  await expect(historyTable).toBeVisible();
  const opportunityResult = historyTable.locator('td.mat-column-tradingOpportunitiesStatus').first();
  await expect(opportunityResult).toContainText('Succeeded');
  await expect(opportunityResult).toContainText(/\d+ symbols/);

  const refreshResponse = page.waitForResponse(
    (response) =>
      response.url().endsWith('/api/opportunities/refresh') &&
      response.request().method() === 'POST',
  );
  await page
    .locator('.section-footer')
    .getByRole('button', { name: 'Run Trading Opportunities Scan' })
    .click();
  expect((await refreshResponse).ok()).toBeTruthy();
  await expectNoHorizontalPageOverflow(page);
  expect(errors, 'uncaught exceptions / console.error').toEqual([]);
});

test('automation shows the affected symbol and disables retry while data settles', async ({
  page,
  errors,
}) => {
  await page.route('**/api/automation/awaiting-data', (route) =>
    route.fulfill({
      json: {
        symbols: ['MHO'],
        tradingDate: '2026-11-18',
        canRetry: false,
        availableAtUtc: '2026-11-18T23:05:00Z',
        reason: 'Market data is still settling.',
      } satisfies AwaitingDataStatusDto,
    }),
  );
  await page.goto('/config');
  await page.getByRole('tab', { name: /Automation/ }).click();
  await expect(page.getByText('MHO', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Retry awaiting data' })).toBeDisabled();
  await expectNoHorizontalPageOverflow(page);
  expect(errors, 'uncaught exceptions / console.error').toEqual([]);
});
