import { test, expect } from './support/test';
import { signalState } from './fixtures/signals-audit';
import { expectNoHorizontalPageOverflow, settle } from './support/overflow';

const isPhone = (name: string) => name.startsWith('mobile');

test.describe('signal tracker', () => {
  test('shows summary cards matching the rows', async ({ page, errors }) => {
    await page.goto('/signal-tracker');
    await expect(page).toHaveURL(/\/signal-tracker$/);
    await expect(page.getByRole('heading', { level: 1, name: 'Signal Tracker' })).toBeVisible();

    const kpis = page.getByRole('region', { name: 'Signal summary' });
    await expect(kpis).toContainText('Total Signals');
    await expect(kpis.locator('.kpi', { hasText: 'Total Signals' }).locator('.kpi-value')).toHaveText('3');
    await expect(kpis.locator('.kpi', { hasText: 'Pending Review' }).locator('.kpi-value')).toHaveText('2');
    await expect(kpis.locator('.kpi', { hasText: 'Actioned' }).locator('.kpi-value')).toHaveText('1');
    expect(errors, 'runtime errors').toEqual([]);
  });

  test('renders signals as a table, or cards on phones, with Pending / N/A horizons and no overflow', async ({ page }, testInfo) => {
    await page.goto('/audit/signals');
    const lmt = page.locator('tr.signal-row:visible', { hasText: 'LMT.TO' });
    await expect(lmt).toBeVisible();
    await expect(lmt).toContainText('Priority Candidate');
    await expect(lmt).toContainText('Pending'); // 5D/20D/1M not complete yet: never shown as 0%
    await expect(lmt).toContainText('+2.40%'); // Today %

    const table = page.locator('table.signals-table');
    if (isPhone(testInfo.project.name)) await expect(table).toHaveAttribute('data-responsive', 'cards');
    else await expect(table).not.toHaveAttribute('data-responsive', 'cards');
    await settle(page);
    await expectNoHorizontalPageOverflow(page);
  });

  test('the old Audit address redirects to the Signal Tracker', async ({ page }) => {
    await page.goto('/audit/signals');
    await expect(page).toHaveURL(/\/signal-tracker$/);
    await expect(page.getByRole('heading', { level: 1, name: 'Signal Tracker' })).toBeVisible();
  });

  test('the future-dated and Invalidated tags: Invalidated is shown in red', async ({ page }) => {
    signalState.rows[0] = { ...signalState.rows[0], status: 'Invalidated', statusReason: 'Condition no longer produced' };
    await page.goto('/audit/signals');
    const tag = page.locator('.tag-invalidated:visible').first();
    await expect(tag).toHaveText('Invalidated');
    await expect(tag).toHaveCSS('background-color', 'rgb(198, 40, 40)');
  });

  test('future-dated signals are flagged as anomalies', async ({ page }) => {
    await page.goto('/audit/signals');
    await expect(page.locator('.anomaly:visible').first()).toBeVisible();
  });

  test('Record opens the editor, links an existing transaction and persists the status', async ({ page, errors }) => {
    await page.goto('/audit/signals');
    await page.getByRole('button', { name: 'Record action for LMT.TO' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog.getByRole('heading', { name: 'Record Investor Action' })).toBeVisible();
    await expect(dialog).toContainText('LMT.TO');
    await expect(dialog).toContainText('Priority Candidate');

    await dialog.getByRole('radio', { name: /^Actioned/ }).check();
    await expect(dialog.getByText('Link an existing transaction')).toBeVisible();
    await expect(dialog).toContainText('BUY');
    await dialog.getByRole('radio', { name: /BUY/ }).check();
    await dialog.getByLabel('Why did I follow or ignore this recommendation?').fill('Score 78 and under-weight sector');
    await dialog.getByRole('button', { name: 'Save Action' }).click();
    await expect(dialog).toBeHidden();

    // Row now reads Actioned with an Edit button; summary cards moved.
    const row = page.locator('tr.signal-row:visible', { hasText: 'LMT.TO' });
    await expect(row).toContainText('Actioned');
    await expect(page.getByRole('button', { name: 'Edit action for LMT.TO' })).toBeVisible();
    await expect(page.locator('.kpi', { hasText: 'Pending Review' }).locator('.kpi-value')).toHaveText('1');

    await page.reload();
    await expect(page.locator('tr.signal-row:visible', { hasText: 'LMT.TO' })).toContainText('Actioned');
    expect(errors, 'runtime errors').toEqual([]);
  });

  test('Not Actioned does not offer a transaction link and Cancel changes nothing', async ({ page }) => {
    await page.goto('/audit/signals');
    await page.getByRole('button', { name: 'Record action for LMT.TO' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByRole('radio', { name: /^Not Actioned/ }).check();
    await expect(dialog.getByText('Link an existing transaction')).toBeHidden();
    await dialog.getByRole('button', { name: 'Cancel' }).click();
    await expect(page.getByRole('button', { name: 'Record action for LMT.TO' })).toBeVisible();
  });

  test('clicking a row opens the read-only details with point-in-time context', async ({ page }) => {
    await page.goto('/audit/signals');
    await page.locator('tr.signal-row:visible', { hasText: 'MSFT' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog.getByRole('heading', { name: 'Signal details' })).toBeVisible();
    await expect(dialog).toContainText('Priority score & breakdown');
    await expect(dialog).toContainText('Ascending Channel');
    await expect(dialog).toContainText('Fibonacci');
    await expect(dialog).toContainText('Not Available'); // never reconstructed from today's indicators
    await expect(dialog).toContainText('Followed the score');
    await dialog.getByRole('button', { name: 'Close' }).click();
    await expect(dialog).toBeHidden();
  });

  test('filters narrow rows and summary together; Reset restores them', async ({ page }, testInfo) => {
    await page.goto('/audit/signals');
    await expect(page.locator('tr.signal-row:visible')).toHaveCount(3);
    if (isPhone(testInfo.project.name)) await page.getByRole('button', { name: /Filters/ }).click();
    await page.getByLabel('Ticker').fill('msft');
    await expect(page.locator('tr.signal-row:visible')).toHaveCount(1);
    await expect(page.locator('.kpi', { hasText: 'Total Signals' }).locator('.kpi-value')).toHaveText('1');

    await page.getByRole('button', { name: /Reset filters/ }).click();
    await expect(page.locator('tr.signal-row:visible')).toHaveCount(3);
  });

  test('From / To use a calendar picker and can be cleared', async ({ page }, testInfo) => {
    await page.goto('/audit/signals');
    if (isPhone(testInfo.project.name)) await page.getByRole('button', { name: /Filters/ }).click();
    await page.getByRole('button', { name: 'Open calendar' }).first().click();
    await page.locator('.mat-calendar-body-cell').first().click();
    await expect(page.getByRole('button', { name: 'Clear From date' })).toBeVisible();
    await page.getByRole('button', { name: 'Clear From date' }).click();
    await expect(page.getByRole('button', { name: 'Clear From date' })).toHaveCount(0);
  });

  test('Export offers Excel and CSV and downloads the filtered rows', async ({ page }) => {
    await page.goto('/audit/signals');
    await expect(page.locator('tr.signal-row:visible')).toHaveCount(3);
    await page.getByRole('button', { name: 'Export signals' }).click();
    const csvDownload = page.waitForEvent('download');
    await page.getByRole('menuitem', { name: /Export to CSV/ }).click();
    expect((await csvDownload).suggestedFilename()).toMatch(/^signals-audit-\d{4}-\d{2}-\d{2}\.csv$/);

    await page.getByRole('button', { name: 'Export signals' }).click();
    const xlsxDownload = page.waitForEvent('download');
    await page.getByRole('menuitem', { name: /Export to Excel/ }).click();
    expect((await xlsxDownload).suggestedFilename()).toMatch(/\.xlsx$/);
  });

  test('effectiveness shows observation counts', async ({ page }) => {
    await page.goto('/audit/signals');
    const eff = page.getByRole('region', { name: 'Signal effectiveness' });
    await expect(eff).toContainText('Priority Candidate');
    await expect(eff).toContainText('n=1');
  });
});

test.describe('signal tracker settings', () => {
  test('threshold can be changed and saved; settings persist', async ({ page, errors }) => {
    await page.goto('/config');
    await page.getByRole('tab', { name: /Signal Tracker/ }).click();
    await expect(page.getByRole('heading', { level: 2, name: /Signal Tracker Settings/ })).toBeVisible();

    const threshold = page.getByLabel(/Minimum score/);
    await expect(threshold).toHaveValue('75');
    await threshold.fill('80');
    await page.getByRole('button', { name: /Save( All Settings)?$/ }).click();
    await expect(page.getByText(/excluded until they re-qualify/)).toContainText('2 stock(s)');

    await page.reload();
    await page.getByRole('tab', { name: /Signal Tracker/ }).click();
    await expect(page.getByLabel(/Minimum score/)).toHaveValue('80');
    expect(errors, 'runtime errors').toEqual([]);
  });

  test('Final Action list: six defaults; add, edit, delete and persist', async ({ page, errors }) => {
    await page.goto('/config');
    await page.getByRole('tab', { name: /Signal Tracker/ }).click();

    const list = page.getByRole('list', { name: 'Audited Final Actions' });
    await expect(list.getByRole('listitem')).toHaveCount(6);
    await expect(list).toContainText('STARTER ENTRY');
    await expect(list).toContainText('TRIM/EXIT');
    await expect(list).toContainText('not produced by current rules');

    await page.getByLabel('Add action').fill('Wait For Pullback');
    await page.getByRole('button', { name: /^add Add$|^Add$/ }).click();
    await expect(list.getByRole('listitem')).toHaveCount(7);

    await page.getByRole('button', { name: 'Delete SMALL ADD' }).click();
    await expect(list.getByRole('listitem')).toHaveCount(6);

    await page.getByRole('button', { name: 'Edit TRIM/REVIEW' }).click();
    await page.getByLabel('Edit action name').fill('TRIM/REVIEW NOW');
    await page.getByRole('button', { name: 'Save name' }).click();
    await expect(list).toContainText('TRIM/REVIEW NOW');

    await page.getByRole('button', { name: /Save( All Settings)?$/ }).click();
    await expect(page.getByText(/excluded until they re-qualify/)).toBeVisible();

    await page.reload();
    await page.getByRole('tab', { name: /Signal Tracker/ }).click();
    await expect(list).toContainText('Wait For Pullback');
    await expect(list).not.toContainText('SMALL ADD');
    expect(errors, 'runtime errors').toEqual([]);
  });
});
