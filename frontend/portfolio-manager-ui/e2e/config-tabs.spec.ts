import { test, expect } from './support/test';
import { expectNoHorizontalPageOverflow, settle } from './support/overflow';

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
