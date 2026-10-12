import { expect, test } from './support/test';
import { expectNoHorizontalPageOverflow, settle } from './support/overflow';

/**
 * The card layout for Material tables, proven on the transactions stock table (the pilot).
 *
 * Phone projects must get cards: no header row, every row inside the viewport, no page overflow,
 * and the controls that replace the hidden header (sort select, direction toggle, "All fields").
 * Desktop and tablet must be byte-for-byte the old table — that is the hard guarantee of the
 * design, so the same spec asserts the negative there.
 */
const HANDSET_PROJECTS = ['mobile-pixel', 'mobile-iphone'];

/** First Material table on the page is the stock-transactions one. */
const stockTable = (page: import('@playwright/test').Page) =>
  page.locator('table.mat-mdc-table').first();

test.describe('transactions table responsiveness', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/transactions');
    await page.getByRole('heading', { level: 2, name: /TRANSACTIONS/ }).waitFor();
    await settle(page);
  });

  test('renders as cards or as a table depending on the viewport', async ({ page }, testInfo) => {
    const handset = HANDSET_PROJECTS.includes(testInfo.project.name);
    const table = stockTable(page);
    await expect(table).toBeVisible();

    const headerRow = table.locator('tr.mat-mdc-header-row');
    const rows = table.locator('tr.mat-mdc-row');
    await expect(rows.first()).toBeVisible();

    if (handset) {
      await expect(table).toHaveAttribute('data-responsive', 'cards');
      await expect(headerRow).toBeHidden();
    } else {
      expect(await table.getAttribute('data-responsive')).toBeNull();
      await expect(headerRow).toBeVisible();
      // Unchanged desktop layout: every registered, visible column still renders.
      expect(await headerRow.locator('th').count()).toBeGreaterThan(10);
      await expect(page.locator('app-mobile-table-controls .mtc-bar')).toHaveCount(0);
    }
  });

  test('every card fits the viewport and the page never scrolls sideways', async ({
    page,
  }, testInfo) => {
    test.skip(!HANDSET_PROJECTS.includes(testInfo.project.name), 'cards are handset-only');

    const viewport = page.viewportSize()!.width;
    const rows = stockTable(page).locator('tr.mat-mdc-row');
    const count = await rows.count();
    expect(count).toBeGreaterThan(1);

    for (let i = 0; i < count; i++) {
      const box = await rows.nth(i).boundingBox();
      expect(box, `row ${i} has a layout box`).not.toBeNull();
      expect(box!.x, `row ${i} left edge`).toBeGreaterThanOrEqual(-1);
      expect(box!.x + box!.width, `row ${i} right edge`).toBeLessThanOrEqual(viewport + 1);
    }

    await expectNoHorizontalPageOverflow(page);
  });

  test('labels every value cell with its column header', async ({ page }, testInfo) => {
    test.skip(!HANDSET_PROJECTS.includes(testInfo.project.name), 'cards are handset-only');

    // ::before content is not in the DOM, so it is read back through the computed style.
    const labels = await stockTable(page)
      .locator('tr.mat-mdc-row')
      .first()
      .locator('td')
      .evaluateAll((cells) =>
        cells.map((cell) => getComputedStyle(cell, '::before').content ?? 'none'),
      );

    expect(labels.filter((c) => c !== 'none' && c !== 'normal' && c !== '""').length).toBeGreaterThan(
      3,
    );
    expect(labels.some((c) => c.includes('TICKER'))).toBe(true);
  });

  test('the sort control reorders the cards', async ({ page, errors }, testInfo) => {
    test.skip(!HANDSET_PROJECTS.includes(testInfo.project.name), 'cards are handset-only');

    const firstCard = () => stockTable(page).locator('tr.mat-mdc-row').first().innerText();
    const before = await firstCard();

    await page.locator('app-mobile-table-controls mat-select').click();
    await page.getByRole('option', { name: 'Ticker', exact: true }).click();
    await page.waitForTimeout(400);
    const ascending = await firstCard();

    await page.getByRole('button', { name: /Sort direction/i }).click();
    await page.waitForTimeout(400);
    const descending = await firstCard();

    expect(ascending).not.toBe(descending);
    expect([ascending, descending]).toContain(ascending);
    expect(before.length).toBeGreaterThan(0);
    expect(errors, 'runtime errors while sorting from the mobile controls').toEqual([]);
  });

  test('"All fields" reveals the secondary columns without widening the page', async ({
    page,
  }, testInfo) => {
    test.skip(!HANDSET_PROJECTS.includes(testInfo.project.name), 'cards are handset-only');

    const cells = stockTable(page).locator('tr.mat-mdc-row').first().locator('td');
    const before = await cells.count();

    await page.getByRole('switch', { name: /Show all fields/i }).click();
    await page.waitForTimeout(400);

    expect(await cells.count()).toBeGreaterThan(before);
    await expectNoHorizontalPageOverflow(page);
  });
});
