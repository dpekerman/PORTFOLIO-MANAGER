import { expect, test, type Browser, type BrowserContext, type Page } from '@playwright/test';

/**
 * Read-only smoke of the real Swing Trading workspace over HTTPS. See playwright.live.config.ts.
 * The session comes from auth.setup.ts. Refresh tokens rotate, so ONE browser context is shared by every device
 * check and only the viewport changes (the app's phone/tablet/desktop layouts key off the viewport width).
 * Nothing here saves, imports or edits data.
 *
 * Prerequisite: the PM-033 migration is applied (it is, on the local database).
 */
const DEVICES = [
  { name: 'desktop-chrome', width: 1920, height: 1080, phone: false },
  { name: 'tablet', width: 800, height: 1280, phone: false },
  { name: 'mobile-s21', width: 384, height: 854, phone: true },
  { name: 'mobile-s23', width: 360, height: 780, phone: true },
] as const;

let context: BrowserContext;
let page: Page;
const serverErrors: string[] = [];

test.describe.configure({ mode: 'serial' });

test.beforeAll(async ({ browser }: { browser: Browser }) => {
  context = await browser.newContext({ storageState: '.auth/live.json', ignoreHTTPSErrors: true, viewport: { width: 1920, height: 1080 } });
  page = await context.newPage();
  page.on('response', (r) => {
    if (/\/api\/(swing-trading|risk)\//.test(r.url()) && r.status() >= 500) serverErrors.push(`${r.status()} ${r.url()}`);
  });
});

test.afterAll(async () => {
  await context.close();
});

async function noHorizontalOverflow() {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow, 'page must not scroll horizontally').toBeLessThanOrEqual(1);
}

async function openSwing() {
  await page.goto('/swing-trading');
  await expect(page.getByRole('heading', { level: 1, name: 'Swing Trading' })).toBeVisible();
  await expect(page.getByTestId('swing-summary')).toBeVisible();
  await expect(page.getByTestId('open-trades-table').or(page.getByTestId('swing-empty'))).toBeVisible();
}

for (const d of DEVICES) {
  test.describe(`Swing Trading live, read-only — ${d.name} (${d.width}x${d.height})`, () => {
    test.beforeEach(async () => {
      await page.setViewportSize({ width: d.width, height: d.height });
    });

    test('navigation, summary and tabs', async () => {
      await openSwing();
      await expect(page.getByTestId('swing-asof')).toBeVisible();
      for (const name of ['Open Trades', 'Risk Monitor', 'Trade Journal']) {
        await page.getByRole('tab', { name }).click();
        await expect(page.getByRole('tab', { name })).toHaveAttribute('aria-selected', 'true');
        await page.waitForTimeout(600);
        await noHorizontalOverflow();
        await page.screenshot({ path: `test-results-live/${d.name}-${name.replace(' ', '-')}.png`, fullPage: true });
      }
      expect(serverErrors, 'Swing endpoints returned 5xx').toEqual([]);
    });

    test('filters and Manage Risk open and close without saving', async () => {
      await openSwing();
      await page.getByRole('tab', { name: 'Open Trades' }).click();
      const rows = page.locator('[data-testid="open-trades-table"] tr.mat-mdc-row');
      if ((await rows.count()) === 0) {
        await expect(page.getByTestId('swing-empty')).toBeVisible();
        return;
      }
      const first = await rows.first().locator('strong').first().innerText();
      await page.getByTestId('swing-search').fill(first);
      await expect(rows.first()).toBeVisible();
      await page.getByTestId('swing-search').fill('');

      await rows.first().getByTestId('manage-risk-btn').click();
      const dlg = page.locator('mat-dialog-container');
      await expect(dlg.getByTestId('mr-note')).toBeVisible();
      await expect(page.getByText('Fully Protected')).toHaveCount(0);
      await dlg.getByRole('tab', { name: 'Broker stops' }).click();
      await expect(dlg.getByTestId('mr-coverage')).toBeVisible();
      if (d.phone) {
        const box = await dlg.boundingBox();
        expect(box!.x + box!.width).toBeLessThanOrEqual(d.width);
      }
      await page.screenshot({ path: `test-results-live/${d.name}-manage-risk.png` });
      await dlg.getByTestId('mr-close').click();
      await expect(dlg).toHaveCount(0);
    });

    test('Portfolio exposes the same Manage Risk dialog', async () => {
      await page.goto('/portfolio');
      const btn = page.getByTestId('portfolio-manage-risk').first();
      await expect(btn).toBeAttached({ timeout: 30_000 });
      await btn.scrollIntoViewIfNeeded();
      await btn.click();
      await expect(page.locator('mat-dialog-container').getByTestId('mr-note')).toBeVisible();
      await page.locator('mat-dialog-container').getByTestId('mr-close').click();
    });
  });
}
