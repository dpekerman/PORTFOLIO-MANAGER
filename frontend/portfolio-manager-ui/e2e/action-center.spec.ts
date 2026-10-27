import type { Page } from '@playwright/test';
import {
  emptyOpenDecisions,
  openDecisionsResponse,
  pendingOpenDecisions,
  unavailableOpenDecisions,
} from './fixtures/dashboard';
import { expectNoHorizontalPageOverflow, settle } from './support/overflow';
import { expect, test } from './support/test';

const isPhone = (name: string) => name.startsWith('mobile');

/** Opens the dashboard with the Action Center expanded (phones collapse every section by default). */
async function openDashboard(page: Page): Promise<void> {
  await page.addInitScript(() => {
    for (const key of ['dashboard_collapse_action-center', 'dashboard_collapse_m_action-center']) {
      localStorage.setItem(key, 'false');
    }
  });
  await page.goto('/dashboard');
  await expect(page.getByRole('heading', { level: 1, name: 'Dashboard' })).toBeVisible();
  await expect(page.getByText('Action Center', { exact: true })).toBeVisible();
  await settle(page);
}

const counter = (page: Page, filter: string) => page.locator(`[data-testid="ac-counter-${filter}"]`);
const counterValue = (page: Page, filter: string) => counter(page, filter).locator('.ac-counter-count');
const total = (page: Page) => page.locator('[data-testid="ac-total"]');
const rows = (page: Page) => page.locator('app-portfolio-actions-widget [data-testid="ac-row"]');
const widget = (page: Page) => page.locator('app-portfolio-actions-widget');

test.describe('action center header counters and queue', () => {
  test('has one Action Center: no separate summary card, counts live in the header', async ({ page, errors }) => {
    await openDashboard(page);

    await expect(page.getByText('Portfolio action summary')).toHaveCount(0);
    await expect(page.locator('app-action-summary-widget')).toHaveCount(0);
    await expect(page.getByText('Market signals', { exact: true })).toHaveCount(0);
    // Total Open is a compact count beside the title, not a fourth tile.
    await expect(total(page)).toHaveText('4');
    await expect(page.locator('[data-testid="ac-counter-All"]')).toHaveCount(0);

    await expect(counterValue(page, 'Entry')).toHaveText('2');
    await expect(counterValue(page, 'ReduceExit')).toHaveText('1');
    await expect(counterValue(page, 'Risk')).toHaveText('1');
    await expect(rows(page)).toHaveCount(4);
    // The old RSI counters are gone.
    for (const retired of ['Oversold', 'Overbought', 'New today', 'Action req.']) {
      await expect(page.locator('#action-center-panel').getByText(retired)).toHaveCount(0);
    }
    expect(errors).toEqual([]);
  });

  test('the header sits directly above the Allocation card with no empty replacement card', async ({ page }) => {
    await openDashboard(page);
    const cards = await page.locator('.db-page mat-card.db-panel').evaluateAll((els) =>
      els.map((el) => ({ title: el.querySelector('.db-panel-title')?.textContent?.trim() ?? '', h: el.getBoundingClientRect().height })),
    );
    expect(cards.every((c) => c.h > 40), 'no empty card').toBe(true);
    const titles = cards.map((c) => c.title);
    expect(titles.indexOf('Market leadership')).toBeLessThan(titles.indexOf('Allocation vs targets'));
    expect(titles).not.toContain('Portfolio action summary');
  });

  test('counters are clickable category filters; the total never changes', async ({ page }) => {
    await openDashboard(page);
    await counter(page, 'Risk').click();
    await expect(rows(page)).toHaveCount(1);
    await expect(rows(page).first()).toContainText('ENB.TO');
    await expect(rows(page).first()).toContainText('Urgent');
    await expect(counter(page, 'Risk')).toHaveAttribute('aria-pressed', 'true');
    await expect(widget(page).locator('[data-testid="ac-showing"]')).toContainText('Risk Review');
    await expect(total(page)).toHaveText('4');

    await counter(page, 'Entry').click();
    await expect(rows(page)).toHaveCount(2);
    // Clicking the active counter again clears the filter.
    await counter(page, 'Entry').click();
    await expect(rows(page)).toHaveCount(4);
    await expect(widget(page).locator('[data-testid="ac-showing"]')).toHaveCount(0);
  });

  test('keeps only the useful Holdings / Watchlist filters, no duplicate category chips', async ({ page }) => {
    await openDashboard(page);
    await expect(widget(page).locator('.ac-chip')).toHaveText([/Holdings\s*3/, /Watchlist\s*1/]);
    for (const dup of ['Entry', 'ReduceExit', 'Risk', 'All']) {
      await expect(widget(page).locator(`[data-testid="ac-filter-${dup}"]`)).toHaveCount(0);
    }
    await widget(page).locator('[data-testid="ac-filter-Watchlist"]').click();
    await expect(rows(page)).toHaveCount(1);
    await expect(rows(page).first()).toContainText('CP.TO');
    // A category counter and Holdings/Watchlist are mutually exclusive views of the same set.
    await counter(page, 'Entry').click();
    await expect(rows(page)).toHaveCount(2);
  });

  test('default order is urgent holding risk, then holdings, then watchlist entries', async ({ page }) => {
    await openDashboard(page);
    expect(await rows(page).locator('.ac-symbol strong').allTextContents()).toEqual(['ENB.TO', 'WMT.TO', 'HPS-A.TO', 'CP.TO']);
  });

  test('rows show evidence and lifecycle, and flag stale baseline records honestly', async ({ page }) => {
    await openDashboard(page);
    const baseline = rows(page).filter({ hasText: 'CP.TO' });
    await expect(baseline).toContainText('Watchlist');
    await expect(baseline).toContainText('Signal Tracker / EOD');
    await expect(baseline).toContainText('stale');
    await expect(baseline).toContainText('Initial baseline');
    await expect(rows(page).filter({ hasText: 'HPS-A.TO' })).toContainText('Not Actioned');
    await expect(rows(page).filter({ hasText: 'ENB.TO' })).toContainText('Pending review');
  });

  test('WATCH, AVOID and informational signals never appear in the queue', async ({ page }) => {
    await openDashboard(page);
    const text = (await widget(page).textContent()) ?? '';
    for (const passive of ['BUY WATCH', 'AVOID', 'WAIT FOR', 'DO NOT CHASE', 'INFORMATIONAL', 'DEVELOPING']) {
      expect(text).not.toContain(passive);
    }
  });

  test('keeps the Signal Tracker link on every row', async ({ page }) => {
    await openDashboard(page);
    await expect(widget(page).locator('a.ac-open')).toHaveCount(4);
    await page.getByRole('link', { name: 'Open ENB.TO in Signal Tracker' }).click();
    await expect(page).toHaveURL(/\/signal-tracker\?ticker=ENB\.TO$/);
    await expect(page.getByRole('heading', { level: 1, name: 'Signal Tracker' })).toBeVisible();
  });

  test('a recommendation that changes between loads is replaced, not duplicated', async ({ page }) => {
    const changed = structuredClone(openDecisionsResponse);
    changed.items = changed.items.filter((d) => d.symbol !== 'WMT.TO');
    changed.items.unshift({ ...openDecisionsResponse.items[1], id: 9, actionLabel: 'Exit / Take Profit', actionId: 'EXIT/TAKE PROFIT' });
    let served = 0;
    await page.route('**/api/dashboard/action-center', (route) =>
      route.fulfill({ json: served++ === 0 ? openDecisionsResponse : changed }),
    );
    await openDashboard(page);
    await expect(rows(page).filter({ hasText: 'WMT.TO' })).toHaveCount(1);
    await page.getByRole('button', { name: 'Refresh dashboard' }).click();
    await expect(rows(page).filter({ hasText: 'WMT.TO' })).toContainText('Exit / Take Profit');
    await expect(rows(page).filter({ hasText: 'WMT.TO' })).toHaveCount(1);
    await expect(rows(page)).toHaveCount(4);
  });
});

test.describe('empty, pending and unavailable states', () => {
  test('shows "No current actionable decisions" only after a validated evaluation', async ({ page }) => {
    await page.route('**/api/dashboard/action-center', (route) => route.fulfill({ json: emptyOpenDecisions }));
    await openDashboard(page);
    await expect(total(page)).toHaveText('0');
    await expect(widget(page).locator('[data-testid="ac-empty"]')).toContainText('No current actionable decisions');
    await expect(page.locator('[data-testid="ac-validation-ico"]')).toHaveCount(0);
  });

  test('shows "Validation pending" - never a green all-clear - when recommendations await revalidation', async ({ page }) => {
    await page.route('**/api/dashboard/action-center', (route) => route.fulfill({ json: pendingOpenDecisions }));
    await openDashboard(page);
    await expect(widget(page).locator('[data-testid="ac-pending"]')).toContainText('Validation pending');
    await expect(widget(page).locator('[data-testid="ac-pending"]')).toContainText('6 recommendation(s)');
    await expect(widget(page)).not.toContainText('No current actionable decisions');
    await expect(widget(page).locator('.ac-empty-icon')).toHaveCount(0);
    await expect(page.locator('[data-testid="ac-validation-ico"]')).toBeVisible();
  });

  test('shows "Evaluation unavailable / stale" when the evaluation failed or is incomplete', async ({ page }) => {
    await page.route('**/api/dashboard/action-center', (route) => route.fulfill({ json: unavailableOpenDecisions }));
    await openDashboard(page);
    await expect(widget(page).locator('[data-testid="ac-unavailable"]')).toContainText('Evaluation unavailable / stale');
    await expect(widget(page)).not.toContainText('No current actionable decisions');
    await expect(page.locator('[data-testid="ac-validation-ico"]')).toBeVisible();
  });

  test('keeps existing decisions visible, with a stale banner, when the latest evaluation is unavailable', async ({ page }) => {
    const stale = { ...openDecisionsResponse, validation: unavailableOpenDecisions.validation };
    await page.route('**/api/dashboard/action-center', (route) => route.fulfill({ json: stale }));
    await openDashboard(page);
    await expect(rows(page)).toHaveCount(4);
    await expect(widget(page).locator('[data-testid="ac-banner"]')).toContainText('Evaluation unavailable / stale');
  });

  test('a failed load shows a retry instead of an all-clear', async ({ page }) => {
    await page.route('**/api/dashboard/action-center', (route) => route.fulfill({ status: 500, json: { title: 'boom' } }));
    await openDashboard(page);
    await expect(widget(page).locator('.ac-error')).toContainText('could not be loaded');
    await expect(page.getByRole('button', { name: 'Retry' })).toBeVisible();
    await expect(widget(page)).not.toContainText('No current actionable decisions');
  });
});

test.describe('layout on every device', () => {
  test('fits the viewport without horizontal overflow and keeps tap targets usable', async ({ page }, testInfo) => {
    await openDashboard(page);
    await expectNoHorizontalPageOverflow(page);
    const viewport = page.viewportSize()!.width;
    for (const el of [counter(page, 'Entry'), counter(page, 'Risk'), total(page), rows(page).first()]) {
      await el.scrollIntoViewIfNeeded();
      const box = (await el.boundingBox())!;
      expect(box.x).toBeGreaterThanOrEqual(0);
      expect(box.x + box.width).toBeLessThanOrEqual(viewport + 1);
    }
    for (const f of ['Entry', 'ReduceExit', 'Risk']) {
      expect((await counter(page, f).boundingBox())!.height).toBeGreaterThanOrEqual(30);
    }
    if (isPhone(testInfo.project.name)) {
      const open = page.locator('a.ac-open').first();
      await open.scrollIntoViewIfNeeded();
      expect((await open.boundingBox())!.height).toBeGreaterThanOrEqual(40);
    }
  });

  test('counters stay visible while the card is collapsed and expand it when used', async ({ page }) => {
    await page.addInitScript(() => {
      for (const key of ['dashboard_collapse_action-center', 'dashboard_collapse_m_action-center']) localStorage.setItem(key, 'true');
    });
    await page.goto('/dashboard');
    await expect(counter(page, 'Risk')).toBeVisible();
    await expect(rows(page)).toHaveCount(0);
    await counter(page, 'Risk').click();
    await expect(rows(page)).toHaveCount(1);
  });
});

test.describe('signal tracker lifecycle', () => {
  test('unaudited recommendations are reachable through the toggle and status filter', async ({ page }) => {
    const queries: string[] = [];
    page.on('request', (r) => {
      if (/\/api\/signals-audit(\?|$)/.test(r.url())) queries.push(r.url());
    });
    await page.goto('/signal-tracker');
    await expect(page.getByRole('heading', { level: 1, name: 'Signal Tracker' })).toBeVisible();
    const unaudited = page.getByRole('checkbox', { name: 'Include unaudited recommendations' });
    // Filters start collapsed on handsets only.
    if (!(await unaudited.isVisible())) await page.getByRole('button', { name: /Filters/ }).click();

    await unaudited.check();
    await expect.poll(() => queries.some((q) => q.includes('includeUnaudited=true'))).toBe(true);

    await page.getByRole('combobox', { name: 'Status' }).click();
    await page.getByRole('option', { name: 'Open (Active)' }).click();
    await expect.poll(() => queries.some((q) => q.includes('status=Open'))).toBe(true);
  });

  test('landing view shows only the newest signal day', async ({ page }) => {
    const queries: string[] = [];
    page.on('request', (r) => {
      if (/\/api\/signals-audit(\?|$)/.test(r.url())) queries.push(r.url());
    });
    await page.goto('/signal-tracker');
    await expect(page.getByRole('heading', { level: 1, name: 'Signal Tracker' })).toBeVisible();
    await expect
      .poll(() => queries.some((q) => /from=(\d{4}-\d{2}-\d{2})&to=\1(&|$)/.test(q) && q.includes('pageSize=25')))
      .toBe(true);
  });

  test('drill-down from the Action Center filters by ticker and includes unaudited records', async ({ page }) => {
    const queries: string[] = [];
    page.on('request', (r) => {
      if (/\/api\/signals-audit(\?|$)/.test(r.url())) queries.push(r.url());
    });
    await page.goto('/signal-tracker?ticker=LMT.TO');
    await expect(page.getByRole('heading', { level: 1, name: 'Signal Tracker' })).toBeVisible();
    await expect.poll(() => queries.some((q) => q.includes('ticker=LMT.TO') && q.includes('includeUnaudited=true'))).toBe(true);
  });
});
