import { expect, test } from './support/test';
import { expectNoHorizontalPageOverflow, settle } from './support/overflow';

for (const path of [
  '/transactions',
  '/watchlist',
  '/eod-signals',
  '/value-screener',
  '/portfolio-value-history',
]) {
  test(`${path}: filters are collapsed, keyboard accessible and fit when expanded`, async ({ page, errors }) => {
    await page.goto(path);
    const panel = page.locator('details.app-filter-panel').first();
    const summary = panel.locator(':scope > summary');
    await expect(summary).toBeVisible();
    await expect(panel).not.toHaveAttribute('open');
    const height = (await panel.boundingBox())!.height;
    expect(height).toBeLessThanOrEqual(60);
    await summary.focus();
    await page.keyboard.press('Enter');
    await expect(panel).toHaveAttribute('open', '');
    await settle(page);
    await expectNoHorizontalPageOverflow(page);
    await summary.focus();
    await page.keyboard.press('Space');
    await expect(panel).not.toHaveAttribute('open');
    expect(errors).toEqual([]);
  });
}

test('dashboard filter panels start collapsed and fit when opened', async ({ page, errors }) => {
  await page.addInitScript(() => {
    for (const section of ['action-center', 'market-leadership']) {
      localStorage.setItem(`dashboard_collapse_${section}`, 'false');
      localStorage.setItem(`dashboard_collapse_m_${section}`, 'false');
    }
  });
  await page.goto('/dashboard');
  for (const widget of ['app-portfolio-actions-widget', 'app-market-leadership-widget']) {
    const panel = page.locator(`${widget} details.app-filter-panel`);
    await expect(panel.locator('summary')).toBeVisible();
    await expect(panel).not.toHaveAttribute('open');
    await panel.locator('summary').click();
    await settle(page);
    await expectNoHorizontalPageOverflow(page);
  }
  expect(errors).toEqual([]);
});

for (const tab of ['Signal Tracker', 'Automation']) {
  test(`configuration ${tab}: filters start collapsed and expand without hiding actions`, async ({ page, errors }) => {
    await page.goto('/config');
    await page.getByRole('tab', { name: tab, exact: true }).click();
    const panel = page.locator('details.app-filter-panel:visible');
    await expect(panel).not.toHaveAttribute('open');
    await panel.locator('summary').click();
    await expect(panel.locator('input').first()).toBeVisible();
    await settle(page);
    await expectNoHorizontalPageOverflow(page);
    if (tab === 'Automation') {
      await expect(page.getByRole('button', { name: 'Clear History', exact: true })).toBeVisible();
    }
    expect(errors).toEqual([]);
  });
}

test('Swing tabs keep filters collapsed, preserve selections and align headers with values', async ({ page, errors }, info) => {
  await page.goto('/swing-trading');
  await expect(page.getByTestId('sum-mv').first()).toContainText('107,170.00');
  const amountsFit = await page.locator('.sw-amount').evaluateAll((amounts) =>
    amounts.every((amount) => {
      const tile = amount.closest('.sw-tile')!.getBoundingClientRect();
      const value = amount.getBoundingClientRect();
      return value.left >= tile.left && value.right <= tile.right;
    }),
  );
  expect(amountsFit).toBe(true);
  for (const [tab, tableId] of [
    ['Open Trades', 'open-trades-table'],
    ['Risk Monitor', 'risk-table'],
    ['Trade Journal', 'journal-table'],
  ]) {
    await page.getByRole('tab', { name: tab, exact: true }).click();
    const table = page.getByTestId(tableId);
    await expect(table.locator('tr.mat-mdc-row').first()).toBeVisible();
    const panel = page.locator('details.app-filter-panel:visible');
    const summary = panel.locator(':scope > summary');
    await expect(panel).not.toHaveAttribute('open');
    await expect(page.getByTestId(tableId === 'open-trades-table' ? 'swing-export' : tableId === 'risk-table' ? 'risk-export' : 'journal-export')).toBeVisible();
    await summary.click();
    const search = panel.locator('input[type="search"]');
    await search.fill(tab === 'Trade Journal' ? 'ABC.TO' : 'DIS.TO');
    await expect(table.locator('tr.mat-mdc-row').first()).toContainText(tab === 'Trade Journal' ? 'ABC.TO' : 'DIS.TO');
    const count = await table.locator('tr.mat-mdc-row').count();
    await summary.click();
    await expect(summary).toContainText('Active');
    await expect(search).toBeHidden();
    await expect(table.locator('tr.mat-mdc-row')).toHaveCount(count);
    await summary.click();
    await expect(search).toHaveValue(tab === 'Trade Journal' ? 'ABC.TO' : 'DIS.TO');
    await panel.getByRole('button', { name: 'Clear filters', exact: true }).click();
    await summary.click();
    if (info.project.name.startsWith('mobile')) {
      await expect(page.locator('.sw-tabs .mat-mdc-tab-header-pagination:visible')).toHaveCount(0);
      await expect(table).toHaveAttribute('data-responsive', 'cards');
      const buttons = table.locator('tr.mat-mdc-row').first().locator('.st-row-btn');
      for (const button of await buttons.all()) {
        expect((await button.boundingBox())!.height).toBeGreaterThanOrEqual(44);
      }
    } else {
      await expect(table.locator('th').first()).toHaveCSS('text-align', 'center');
      await expect(table.locator('td').first()).toHaveCSS('text-align', 'center');
      await expect(table.locator('td').first()).toHaveCSS('vertical-align', 'middle');
      await expect(table.locator('.mat-sort-header-container').first()).toHaveCSS('justify-content', 'center');
      await expect(table.locator('th').first()).toHaveCSS('white-space', 'nowrap');
    }
    const valueStack = table.locator('.st-cell-value').first();
    await expect(valueStack).toHaveCSS('align-items', info.project.name.startsWith('mobile') ? 'flex-end' : 'center');
    await expect(table.locator('.st-row-btn mat-icon')).toHaveCount(0);
    await settle(page);
    await expectNoHorizontalPageOverflow(page);
    await page.locator('.sw-head').scrollIntoViewIfNeeded();
    await page.screenshot({ path: test.info().outputPath(`${tab.replaceAll(' ', '-')}.png`), fullPage: true, scale: 'css' });
  }
  expect(errors).toEqual([]);
});
