import type { Locator, Page } from '@playwright/test';
import { expect, test } from './support/test';
import { expectNoHorizontalPageOverflow, settle } from './support/overflow';

/**
 * Card layout for the pages rolled out after the transactions pilot (see `responsive-cards.spec.ts`
 * for the contract). Each entry describes one Material table; the shared checks assert the same
 * guarantees for all of them:
 * - handset: `data-responsive="cards"`, hidden header row, every card inside the viewport, labelled
 *   cells, sort control that drives the page's own `matSortChange`, no page overflow;
 * - desktop/tablet: the untouched table (header row visible, no mobile controls).
 */
const HANDSET_PROJECTS = ['mobile-pixel', 'mobile-iphone'];

interface CardTableCase {
  /** Test group name. */
  name: string;
  path: string;
  landmark: (page: Page) => Locator;
  /** The `<table mat-table>` under test. */
  table: (page: Page) => Locator;
  /** Steps that make the table visible (expanders, tabs, ...). */
  open?: (page: Page) => Promise<void>;
  /** `aria-label` of the "Sort by" select (`Sort <label> by`). */
  sortLabel?: string;
  /** Registry label of a sortable column other than the default one. */
  sortOption?: string;
  /** Upper-case header text that must label a card cell. */
  cellLabel: string;
  /** Minimum header cells on desktop, proving no column was dropped there. */
  desktopMinHeaders: number;
  /** Grid has secondary columns behind "All fields". */
  hasSecondaryFields?: boolean;
}

const CASES: CardTableCase[] = [
  {
    name: 'portfolio stocks table',
    path: '/portfolio',
    landmark: (p) => p.getByRole('heading', { level: 2, name: /MY PORTFOLIO/ }),
    table: (p) => p.locator('table.portfolio-grid-table').first(),
    sortLabel: 'Sort Stocks by',
    sortOption: 'Mkt Value',
    cellLabel: 'TICKER',
    desktopMinHeaders: 20,
    hasSecondaryFields: true,
  },
  {
    name: 'portfolio options table',
    path: '/portfolio',
    landmark: (p) => p.getByRole('heading', { level: 2, name: /MY PORTFOLIO/ }),
    table: (p) => p.locator('table.options-grid-table'),
    sortLabel: 'Sort Options by',
    sortOption: 'Strike',
    cellLabel: 'UNDERLYING',
    desktopMinHeaders: 10,
    hasSecondaryFields: true,
  },
  {
    name: 'portfolio cash table',
    path: '/portfolio',
    landmark: (p) => p.getByRole('heading', { level: 2, name: /MY PORTFOLIO/ }),
    table: (p) => p.locator('#cash-table-content table'),
    open: async (p) => {
      await p.getByRole('button', { name: /Cash transactions/ }).click();
    },
    sortLabel: 'Sort Cash by',
    sortOption: 'Amount',
    cellLabel: 'DESCRIPTION',
    desktopMinHeaders: 4,
  },
  {
    name: 'watchlist table',
    path: '/watchlist',
    landmark: (p) => p.getByRole('heading', { level: 1, name: /MY WATCHLIST/ }),
    table: (p) => p.locator('table.watchlist-table'),
    sortLabel: 'Sort Watchlist by',
    sortOption: 'Last Price',
    cellLabel: 'TICKER',
    desktopMinHeaders: 15,
    hasSecondaryFields: true,
  },
];

for (const c of CASES) {
  test.describe(c.name, () => {
    test.beforeEach(async ({ page }) => {
      await page.goto(c.path);
      await c.landmark(page).first().waitFor();
      await settle(page);
      await c.open?.(page);
      await expect(c.table(page).locator('tr.mat-mdc-row').first()).toBeVisible();
    });

    test('renders as cards or as a table depending on the viewport', async ({ page }, info) => {
      const handset = HANDSET_PROJECTS.includes(info.project.name);
      const table = c.table(page);
      const headerRow = table.locator('tr.mat-mdc-header-row');

      if (handset) {
        await expect(table).toHaveAttribute('data-responsive', 'cards');
        await expect(headerRow).toBeHidden();
        await expect(page.locator(`.mtc-bar[aria-label="${c.sortLabel!.slice(5, -3)} display options"]`)).toBeVisible();
      } else {
        expect(await table.getAttribute('data-responsive')).toBeNull();
        await expect(headerRow).toBeVisible();
        expect(await headerRow.locator('th').count()).toBeGreaterThanOrEqual(c.desktopMinHeaders);
        await expect(page.locator('app-mobile-table-controls .mtc-bar')).toHaveCount(0);
      }
    });

    test('every card fits the viewport and the page never scrolls sideways', async ({
      page,
    }, info) => {
      test.skip(!HANDSET_PROJECTS.includes(info.project.name), 'cards are handset-only');
      const viewport = page.viewportSize()!.width;
      const rows = c.table(page).locator('tr.mat-mdc-row');
      const count = await rows.count();
      expect(count).toBeGreaterThan(0);
      for (let i = 0; i < count; i++) {
        const box = await rows.nth(i).boundingBox();
        expect(box, `row ${i} has a layout box`).not.toBeNull();
        expect(box!.x, `row ${i} left edge`).toBeGreaterThanOrEqual(-1);
        expect(box!.x + box!.width, `row ${i} right edge`).toBeLessThanOrEqual(viewport + 1);
      }
      await expectNoHorizontalPageOverflow(page);
    });

    test('labels the cells with their column headers', async ({ page }, info) => {
      test.skip(!HANDSET_PROJECTS.includes(info.project.name), 'cards are handset-only');
      const labels = await c
        .table(page)
        .locator('tr.mat-mdc-row')
        .first()
        .locator('td')
        .evaluateAll((cells) => cells.map((cell) => getComputedStyle(cell, '::before').content));
      expect(labels.some((l) => l.includes(c.cellLabel))).toBe(true);
    });

    test('the sort control re-sorts the cards without errors', async ({ page, errors }, info) => {
      test.skip(!HANDSET_PROJECTS.includes(info.project.name), 'cards are handset-only');
      const select = page
        .locator(`.mtc-bar[aria-label="${c.sortLabel!.slice(5, -3)} display options"]`)
        .locator('mat-select');
      await select.click();
      await page.getByRole('option', { name: c.sortOption!, exact: true }).click();
      await page.waitForTimeout(300);
      await expect(select).toContainText(c.sortOption!);
      expect(errors, 'runtime errors while sorting from the mobile controls').toEqual([]);
    });

    test('"All fields" widens the card without widening the page', async ({ page }, info) => {
      test.skip(
        !HANDSET_PROJECTS.includes(info.project.name) || !c.hasSecondaryFields,
        'only handset grids with secondary columns',
      );
      const cells = c.table(page).locator('tr.mat-mdc-row').first().locator('td');
      const before = await cells.count();
      await page
        .locator(`.mtc-bar[aria-label="${c.sortLabel!.slice(5, -3)} display options"]`)
        .getByRole('switch', { name: /Show all fields/i })
        .click();
      await page.waitForTimeout(300);
      expect(await cells.count()).toBeGreaterThan(before);
      await expectNoHorizontalPageOverflow(page);
    });
  });
}
