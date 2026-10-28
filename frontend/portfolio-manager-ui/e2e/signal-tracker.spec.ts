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

});

test.describe('signal effectiveness', () => {
  const eff = (page: import('@playwright/test').Page) => page.getByRole('region', { name: 'Signal effectiveness' });

  test('defaults to the 90-day cohort with sample counts, window dates and an accessible selector', async ({ page }) => {
    await page.goto('/signal-tracker');
    const region = eff(page);
    await expect(region).toContainText('Priority Candidate');
    await expect(region).toContainText('n=6');
    await expect(region.getByTestId('eff-window-note')).toContainText('Signals dated');
    await expect(region.getByTestId('eff-coverage')).toContainText('11 signals in this window');
    await expect(region.getByTestId('eff-window-90')).toHaveClass(/mat-button-toggle-checked/);
    await expect(region.getByRole('radiogroup', { name: /Evaluation window/ })).toBeVisible();
    await expect(region.getByRole('table', { name: 'Signal effectiveness by signal type' })).toBeVisible();
  });

  test('is independent of the table filters: ticker, dates, status and My Action never reach it', async ({ page }, testInfo) => {
    const requests: string[] = [];
    page.on('request', (r) => {
      if (/\/api\/signals-audit\/effectiveness/.test(r.url())) requests.push(r.url());
    });
    await page.goto('/signal-tracker');
    await expect(eff(page)).toContainText('n=6');
    const initial = requests.length;
    expect(initial).toBeGreaterThan(0);

    if (isPhone(testInfo.project.name)) await page.getByRole('button', { name: /Filters/ }).click();
    await page.getByLabel('Ticker').fill('msft');
    await expect(page.locator('tr.signal-row:visible')).toHaveCount(1);
    await page.getByRole('button', { name: 'Open calendar' }).first().click();
    await page.locator('.mat-calendar-body-cell').first().click();
    await page.waitForTimeout(600);

    expect(requests.length).toBe(initial);
    for (const url of requests) expect(url).not.toMatch(/[?&](ticker|from|to|status|myAction)=/);
    await expect(eff(page)).toContainText('n=6'); // unchanged
  });

  test('window selector changes the cohort and shows insufficient history, incomplete coverage and all history', async ({ page }) => {
    await page.goto('/signal-tracker');
    const region = eff(page);
    await expect(region).toContainText('n=6');

    await region.getByTestId('eff-window-30').click();
    await expect(region.getByTestId('eff-coverage')).toContainText('2 signals in this window');
    await expect(region).toContainText('Insufficient history'); // no completed 20D / 1M outcomes in a 30-day cohort

    await region.getByTestId('eff-window-180').click();
    await expect(region.getByTestId('eff-incomplete')).toContainText('no usable price history');

    await region.getByTestId('eff-window-all').click();
    await expect(region.getByTestId('eff-window-note')).toContainText('All history through');
    await expect(region.getByTestId('eff-incomplete')).toHaveCount(0);
  });

  test('keeps raw and direction-adjusted returns separate and never forces a direction', async ({ page }) => {
    await page.goto('/signal-tracker');
    const region = eff(page);
    const buy = region.locator('tr.mat-mdc-row', { hasText: 'Priority Candidate' });
    await expect(buy).toContainText('-3.20%'); // entry followed by a decline: negative, unfavourable
    await expect(buy).toContainText('25%');

    const exit = region.locator('tr.mat-mdc-row', { hasText: 'Cut Loss / Exit' });
    await expect(exit).toContainText('+4.50%'); // exit followed by a decline: adjusted positive
    await expect(exit).toContainText('raw -4.50%'); // raw stock return retained
    await expect(exit).toContainText('100%');

    const watch = region.locator('tr.mat-mdc-row', { hasText: 'Buy Watch' });
    await expect(watch).toContainText('Direction not defined');
    await expect(watch).not.toContainText('Favourable');
  });

  test('context filters apply: an unmatched role shows an explicit insufficient-history state', async ({ page }, testInfo) => {
    await page.goto('/signal-tracker');
    await expect(eff(page)).toContainText('n=6');
    if (isPhone(testInfo.project.name)) await page.getByRole('button', { name: /Filters/ }).click();
    // Keyboard-open the multi-select: the empty outline label overlaps its trigger for pointer clicks.
    await page.getByRole('combobox', { name: 'Investment Role' }).focus();
    await page.keyboard.press('Enter');
    await page.getByRole('option', { name: 'Options', exact: true }).click();
    await page.keyboard.press('Escape');
    await expect(eff(page).getByTestId('eff-empty')).toContainText('Insufficient history');
  });

  test('explains the calculation, uses singular/plural labels and fits every viewport', async ({ page }, testInfo) => {
    await page.goto('/signal-tracker');
    const region = eff(page);
    await expect(region).toContainText('6 signals');
    const help = region.getByTestId('eff-help-toggle');
    await expect(help).toHaveAttribute('aria-expanded', 'false');
    await help.click();
    await expect(help).toHaveAttribute('aria-expanded', 'true');
    await expect(region).toContainText('Completed periods only');
    await expect(region).toContainText('never treated as zero');

    const table = region.locator('table.eff-table');
    if (isPhone(testInfo.project.name)) await expect(table).toHaveAttribute('data-responsive', 'cards');
    else await expect(table).not.toHaveAttribute('data-responsive', 'cards');
    await settle(page);
    await expectNoHorizontalPageOverflow(page);
  });

  test('plots the averages as a chart: declines below zero, exits direction-adjusted, missing periods not drawn', async ({ page }) => {
    await page.goto('/signal-tracker');
    const region = eff(page);
    const chart = region.getByTestId('eff-chart');
    await expect(chart).toBeVisible();
    await expect(chart.locator('.eff-group')).toHaveCount(3);

    const entry = chart.getByTestId('eff-group-PRIORITY_CANDIDATE');
    await expect(entry).toHaveAttribute('aria-label', /20D: -3\.20%/);
    const exit = chart.getByTestId('eff-group-FA:CUT LOSS / EXIT');
    await expect(exit).toHaveAttribute('aria-label', /20D: \+4\.50% \(raw -4\.50%\)/);
    await expect(exit).toHaveAttribute('aria-label', /a fall counts as positive/);
    const watch = chart.getByTestId('eff-group-FA:BUY WATCH');
    await expect(watch).toHaveAttribute('aria-label', /direction not defined/);
    await expect(watch).toHaveAttribute('aria-label', /1M: insufficient history/);
    await expect(watch.locator('.eff-bar-na')).toHaveCount(1); // the missing 1M period is marked, not drawn as zero
    await expect(watch.locator('.eff-bar')).toHaveCount(2);

    // Geometry: the entry that fell sits below the zero line; the exit that fell (adjusted +) sits above it.
    const zero = await chart.locator('.eff-zero-line').boundingBox();
    const down = await entry.locator('.eff-bar-neg').first().boundingBox();
    const up = await exit.locator('.eff-bar:not(.eff-bar-neg)').first().boundingBox();
    expect(zero && down && up).toBeTruthy();
    expect(down!.y + 1).toBeGreaterThanOrEqual(zero!.y);
    expect(up!.y + up!.height).toBeLessThanOrEqual(zero!.y + 2);
  });

  test('the chart follows the window selector and stays inside the viewport', async ({ page }) => {
    await page.goto('/signal-tracker');
    const region = eff(page);
    const chart = region.getByTestId('eff-chart');
    await expect(chart.locator('.eff-bar')).toHaveCount(8); // 5D+20D+1M for two types, 5D+20D for the watch type

    await region.getByTestId('eff-window-30').click();
    await expect(chart.locator('.eff-group')).toHaveCount(1);
    await expect(chart.locator('.eff-bar')).toHaveCount(1); // only the 5D period has completed in a 30-day cohort
    await expect(chart.locator('.eff-bar-na')).toHaveCount(2);

    const box = await chart.boundingBox();
    const viewport = page.viewportSize()!;
    expect(box!.x).toBeGreaterThanOrEqual(0);
    expect(box!.x + box!.width).toBeLessThanOrEqual(viewport.width + 1);
    await settle(page);
    await expectNoHorizontalPageOverflow(page);
  });

  test('keyboard users can focus each signal type in the chart', async ({ page }) => {
    await page.goto('/signal-tracker');
    const group = eff(page).getByTestId('eff-chart').locator('.eff-group').first();
    await group.focus();
    await expect(group).toBeFocused();
    await expect(group).toHaveAttribute('role', 'img');
  });

  test('a one-row table says "1 signal", not "1 signals"', async ({ page }, testInfo) => {
    await page.goto('/signal-tracker');
    await expect(page.locator('tr.signal-row:visible')).toHaveCount(3);
    if (isPhone(testInfo.project.name)) await page.getByRole('button', { name: /Filters/ }).click();
    await page.getByLabel('Ticker').fill('msft');
    await expect(page.locator('tr.signal-row:visible')).toHaveCount(1);
    await expect(page.locator('mat-panel-description').first()).toHaveText('1 signal');
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
