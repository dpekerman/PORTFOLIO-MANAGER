import type { Page } from '@playwright/test';
import { expect, test } from './support/test';
import { expectNoHorizontalPageOverflow, settle } from './support/overflow';

// PM-029 Trading Opportunities. Data comes from e2e/fixtures/opportunities.ts (synthetic values).

const isDesktop = (name: string) => name === 'desktop-chrome';
const table = (page: Page) => page.getByTestId('opportunities-table');
const dataRows = (page: Page) => table(page).locator('tr.opp-row');
const rowFor = (page: Page, symbol: string) => table(page).locator(`tr.opp-row[data-symbol="${symbol}"]`);
const visibleSymbols = async (page: Page) =>
  (await dataRows(page).evaluateAll((rows) => rows.map((r) => r.getAttribute('data-symbol')))).sort();

async function openFilters(page: Page) {
  const panel = page.locator('.app-filter-panel');
  if (!(await panel.evaluate((d) => (d as HTMLDetailsElement).open))) {
    await panel.locator('> summary').click();
  }
}

test.describe('PM-029 Trading Opportunities', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/trading-opportunities');
    await expect(page.getByRole('heading', { level: 1, name: /Trading Opportunities/ })).toBeVisible();
    await expect(dataRows(page).first()).toBeVisible();
  });

  test('default view shows 70+ swing scores with coloured cells and signed sector heat', async ({ page, mocks, errors }) => {
    expect(await visibleSymbols(page)).toEqual(['PLW.TO', 'QRK.TO', 'ZEN.NE']);

    const strong = rowFor(page, 'QRK.TO').getByTestId('score-cell').first();
    await expect(strong).toHaveClass(/heat--strong/);
    await expect(strong).toContainText('88');
    const bg = await strong.evaluate((el) => getComputedStyle(el).backgroundColor);
    expect(bg).not.toMatch(/rgba\(0, 0, 0, 0\)|transparent/);

    const provisional = rowFor(page, 'PLW.TO').getByTestId('score-cell').first();
    await expect(provisional).toHaveClass(/heat--provisional/);
    await expect(provisional).toContainText('P');

    await expect(rowFor(page, 'QRK.TO').getByTestId('sector-heat')).toHaveClass(/sh--pos-2/);
    await expect(rowFor(page, 'QRK.TO').getByTestId('sector-heat')).toContainText('+3.6 pp');
    await expect(rowFor(page, 'PLW.TO').getByTestId('sector-heat')).toHaveClass(/sh--neg-2/);
    // Typographic minus sign (U+2212), as rendered by the badge.
    await expect(rowFor(page, 'PLW.TO').getByTestId('sector-heat')).toContainText('\u22122.4 pp');

    const summary = page.getByTestId('exclusion-summary');
    await expect(summary).toContainText('Showing 3 of 5');
    await expect(summary).toContainText('1 below threshold');
    await expect(page.getByTestId('reason-setup')).toContainText('1 unscorable');

    await expectNoHorizontalPageOverflow(page);
    expect(mocks.unmocked).toEqual([]);
    expect(errors).toEqual([]);
  });

  test('unscorable securities stay discoverable and distinct from weak ones', async ({ page, errors }) => {
    await page.getByTestId('reason-setup').click();
    expect(await visibleSymbols(page)).toEqual(['NVX.TO']);
    await expect(rowFor(page, 'NVX.TO').getByTestId('score-cell').first()).toContainText('N/A');
    await expect(rowFor(page, 'NVX.TO').getByTestId('score-cell').first()).toHaveClass(/heat--none/);
    await page.getByRole('button', { name: /Unscorable only/ }).click();

    await openFilters(page);
    await page.getByTestId('show-unavailable').locator('button').click();
    expect(await visibleSymbols(page)).toEqual(['NVX.TO', 'PLW.TO', 'QRK.TO', 'ZEN.NE']);

    await page.getByTestId('score-filter').click();
    await page.getByRole('option', { name: /All/ }).first().click();
    expect(await visibleSymbols(page)).toEqual(['BRQ.TO', 'NVX.TO', 'PLW.TO', 'QRK.TO', 'ZEN.NE']);
    await expect(rowFor(page, 'BRQ.TO').getByTestId('score-cell').first()).toHaveClass(/heat--weak/);

    await page.getByTestId('verified-only').locator('button').click();
    expect(await visibleSymbols(page)).not.toContain('PLW.TO');
    expect(errors).toEqual([]);
  });

  test('strategic perspective scores independently of a missing swing setup', async ({ page, errors }) => {
    await page.getByTestId('perspective-strategic').click();
    // NVX has no swing setup but a valid strategic score; ZEN lacks fundamentals for strategic.
    await openFilters(page);
    await page.getByTestId('score-filter').click();
    await page.getByRole('option', { name: /All/ }).first().click();
    await expect(rowFor(page, 'NVX.TO')).toBeVisible();
    await expect(page.getByTestId('exclusion-summary')).toContainText('1 unscorable');
    expect(errors).toEqual([]);
  });

  test('row opens the four-tab details with every authoritative decision', async ({ page, errors }, testInfo) => {
    await rowFor(page, 'PLW.TO').click();
    const panel = page.getByTestId('details-panel');
    await expect(panel).toBeVisible();
    await expect(panel).toHaveAttribute('role', isDesktop(testInfo.project.name) ? 'complementary' : 'dialog');

    await expect(panel.getByTestId('details-final-action')).toContainText('Multiple (2)');
    const decisions = panel.getByTestId('details-decisions');
    await expect(decisions.locator('li')).toHaveCount(2);
    await expect(decisions).toContainText('Portfolio · Strategic');
    await expect(decisions).toContainText('Portfolio · Swing');
    await expect(decisions).toContainText('Hold / Monitor');
    await expect(decisions).toContainText('Trim / Review');

    for (const tab of ['Overview', 'Technicals', 'Scores', 'Evidence']) {
      await panel.getByRole('tab', { name: tab }).click();
      await expect(panel.getByRole('tab', { name: tab })).toHaveAttribute('aria-selected', 'true');
    }
    await expect(panel).toContainText('pm029-v1');

    await expectNoHorizontalPageOverflow(page);
    await page.getByTestId('details-close').click();
    await expect(panel).toHaveCount(0);
    if (isDesktop(testInfo.project.name)) await expect(rowFor(page, 'PLW.TO')).toBeFocused();
    expect(errors).toEqual([]);
  });

  test('unavailable setup shows no fabricated levels and Escape closes the details', async ({ page, errors }) => {
    await page.getByTestId('reason-setup').click();
    await rowFor(page, 'NVX.TO').click();
    const panel = page.getByTestId('details-panel');
    await expect(panel).toContainText('No structural target');
    await expect(panel.getByTestId('details-final-action')).toContainText('Watch');
    await page.keyboard.press('Escape');
    await expect(panel).toHaveCount(0);
    expect(errors).toEqual([]);
  });

  test('columns selector opens from the page', async ({ page, errors }) => {
    await page.getByRole('button', { name: /^Columns \(\d+\)/ }).first().click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText('Sector Heat');
    await page.keyboard.press('Escape');
    await expect(dialog).toHaveCount(0);
    expect(errors).toEqual([]);
  });

  test('refresh runs a new read-only snapshot', async ({ page, mocks, errors }) => {
    await page.getByTestId('refresh-analysis').click();
    await expect.poll(() => mocks.requests.filter((r) => /POST \/api\/opportunities\/refresh/.test(r)).length).toBe(1);
    await expect(page.getByTestId('refresh-analysis')).toBeEnabled();
    expect(mocks.requests.some((r) => /^(POST|PUT|DELETE) \/api\/(cash|portfolio|transactions|risk|signals)/.test(r))).toBe(false);
    expect(errors).toEqual([]);
  });

  // Opt-in UAT evidence: PM029_SCREENSHOTS=<dir> npx playwright test trading-opportunities
  test('captures UAT screenshots', async ({ page }, testInfo) => {
    const dir = process.env['PM029_SCREENSHOTS'];
    test.skip(!dir, 'set PM029_SCREENSHOTS to capture');
    const name = testInfo.project.name;
    await settle(page);
    await page.screenshot({ path: `${dir}/${name}-01-heatmap.png`, fullPage: true });
    await rowFor(page, 'QRK.TO').click();
    await expect(page.getByTestId('opportunity-details')).toBeVisible();
    await settle(page);
    await page.screenshot({ path: `${dir}/${name}-02-details.png`, fullPage: true });
    await page.getByTestId('details-close').click();
    await rowFor(page, 'PLW.TO').click();
    await expect(page.getByTestId('details-decisions')).toBeVisible();
    await settle(page);
    await page.screenshot({ path: `${dir}/${name}-02b-details-multiple.png`, fullPage: true });
    await page.getByTestId('details-close').click();
    await page.getByRole('button', { name: /^Columns \(\d+\)/ }).first().click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await settle(page);
    await page.screenshot({ path: `${dir}/${name}-03-columns.png` });
  });
});
