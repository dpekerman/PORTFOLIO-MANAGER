import { readFileSync } from 'node:fs';
import type { Page } from '@playwright/test';
import type { FinalActionResult, OpenDecisionsResponse } from '../src/app/core/models/portfolio.models';
import { openDecisionsResponse } from './fixtures/dashboard';
import { signalState } from './fixtures/signals-audit';
import { expectNoHorizontalPageOverflow, settle } from './support/overflow';
import { expect, test } from './support/test';

/**
 * Awaiting Data across the five surfaces (Portfolio, Watchlist, Dashboard, Action Center, Signal Tracker), all with deterministic
 * mocked data on every device project. A missing / stale canonical decision must never be a blank cell, a legacy BUY/SELL label or
 * an actionable historical signal, and Loading / Not evaluated / Awaiting Data / API error must stay distinguishable.
 */

const isPhone = (name: string) => name.startsWith('mobile');

const EXPECTED = '2026-10-26';

function fa(symbol: string, source: 'Portfolio' | 'Watchlist', over: Partial<FinalActionResult> = {}): FinalActionResult {
  return {
    symbol,
    source,
    itemId: null,
    role: 'Swing',
    finalAction: 'Hold / Monitor',
    finalActionId: 'HOLD/MONITOR',
    category: 'Advisory',
    direction: 'Neutral',
    severity: 'hold',
    priority: 'INFORMATIONAL',
    finalActionClass: 'ma-hold',
    trendSetup: 'Technical Caution',
    momentumShift: 'Active Sell Trigger',
    baseAction: 'Confirmed Sell Signal',
    gapStatus: 'No Gap',
    hoverDescription: 'Review stop-loss levels. Reminder only.',
    reason: 'Technical signal only. Original technical action: Trim / Review.',
    hasHardStructuralNegative: false,
    analysisPrice: 10,
    analysisTicker: symbol,
    analysisCurrency: 'CAD',
    trendSetupReason: '',
    momentumShiftReason: '',
    trendSetupClass: '',
    momentumShiftClass: '',
    gapScore: 0,
    gapPct: 0,
    isActionable: false,
    reasonCodes: ['TECHNICAL_ONLY_NO_PORTFOLIO_TRIGGER'],
    originalAction: 'Trim / Review',
    evaluationState: 'CurrentValid',
    evidenceHealth: 'Fresh',
    dataAsOfDate: EXPECTED,
    expectedSession: EXPECTED,
    lastSuccessfulEvaluationUtc: '2026-10-26T22:30:00Z',
    quoteVerification: 'Unavailable',
    ...over,
  };
}

const awaiting = (symbol: string, source: 'Portfolio' | 'Watchlist', codes: string[], health: FinalActionResult['evidenceHealth'], lastValid: string | null) =>
  fa(symbol, source, {
    finalAction: 'Awaiting Data',
    finalActionId: 'AWAITING DATA',
    finalActionClass: 'ma-standby',
    trendSetup: '',
    momentumShift: '',
    hoverDescription: 'Awaiting Data: no decision is made until a successful evaluation.',
    reason: `Awaiting Data: required evidence is unavailable. No decision is made until a successful evaluation.${lastValid ? ` Last valid action: ${lastValid}.` : ''}`,
    isActionable: false,
    reasonCodes: codes,
    originalAction: null,
    evaluationState: 'AwaitingData',
    evidenceHealth: health,
    lastValidAction: lastValid,
    dataAsOfDate: health === 'Stale' ? '2026-10-19' : null,
  });

const snapshot = (portfolio: FinalActionResult[], watchlist: FinalActionResult[] = []) => ({
  portfolio,
  watchlist,
  computedAtUtc: '2026-10-27T15:00:00Z',
});

async function mockFinalActions(page: Page, body: () => unknown | Promise<unknown>, status = 200): Promise<void> {
  await page.route(
    (url) => url.pathname === '/api/final-actions',
    async (route) => {
      const payload = await body();
      await route.fulfill({ status, json: payload as object });
    },
  );
}

const portfolioScenario = () =>
  snapshot(
    [
      awaiting('SHOP.TO', 'Portfolio', ['AWAITING_STALE_DATA', 'REVALIDATION_MISSED'], 'Stale', 'Add to Trade'),
      awaiting('RY.TO', 'Portfolio', ['AWAITING_REFRESH_FAILED'], 'Failed', 'Exit / Cut Loss'),
      fa('AAPL', 'Portfolio'),
      // MSFT (and the rest) deliberately have no result: "Not evaluated".
    ],
    [
      awaiting('BAM.TO', 'Watchlist', ['AWAITING_INVALID_PRICE'], 'Invalid', null),
      fa('SHOP', 'Watchlist', { finalAction: 'WAIT FOR PULLBACK', finalActionId: 'WAIT FOR PULLBACK', momentumShift: 'Neutral', reasonCodes: [], originalAction: null, reason: null }),
    ],
  );

const FORBIDDEN = /\b(ENTRY CANDIDATE|STARTER ENTRY|ADD CANDIDATE|EXIT REVIEW|TRIM WATCH|Cut Loss|Take Profit)\b/;

test.describe('Awaiting Data — Portfolio', () => {
  test('shows an Awaiting Data badge (never blank, never a legacy trade label) with the reason and last valid action', async ({ page, errors }, testInfo) => {
    await mockFinalActions(page, portfolioScenario);
    await page.goto('/portfolio');
    const shop = page.getByRole('button', { name: 'Final Action details for SHOP.TO: Awaiting Data' }).first();
    await expect(shop).toBeVisible();
    await expect(page.getByRole('button', { name: 'Final Action details for RY.TO: Awaiting Data' }).first()).toBeVisible();
    await expect(page.getByRole('button', { name: /Final Action details for AAPL: Hold \/ Monitor/ }).first()).toBeVisible();

    // Symbols without a result are "Not evaluated", not blank and not a decision.
    await expect(page.locator('[data-testid="fa-not-evaluated"]:visible').first()).toBeVisible();
    await expect(page.locator('[data-testid="fa-error"]')).toHaveCount(0);

    // Badge readability: not clipped.
    const clipped = await shop.evaluate((el) => el.scrollWidth > el.clientWidth + 1);
    expect(clipped, 'Awaiting Data badge is clipped').toBe(false);

    await shop.click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByRole('heading', { name: /Final Action — SHOP\.TO/ })).toBeVisible();
    await expect(dialog.locator('[data-testid="fad-action"]')).toContainText('Awaiting Data');
    await expect(dialog.locator('[data-testid="fad-action"]')).toContainText('Not actionable');
    await expect(dialog).toContainText('Last valid action (historical)');
    await expect(dialog).toContainText('Add to Trade');
    await expect(dialog).toContainText('Market data is older than the last completed session');
    await expect(dialog.locator('[data-testid="fad-quote"]')).toContainText('not independently verified');
    expect(((await dialog.textContent()) ?? '').replace('Add to Trade', '')).not.toMatch(FORBIDDEN);

    // Responsive + accessible dialog: inside the viewport, labelled, Esc closes.
    const box = await dialog.boundingBox();
    expect(box!.x).toBeGreaterThanOrEqual(0);
    expect(box!.x + box!.width).toBeLessThanOrEqual(page.viewportSize()!.width + 1);
    await expect.poll(() => dialog.evaluate((el) => el.contains(document.activeElement)), { message: 'focus moves into the dialog' }).toBe(true);
    await page.keyboard.press('Escape');
    await expect(dialog).toBeHidden();
    await expect(shop).toBeFocused();                                    // focus returns to the badge that opened it
    expect(errors, `runtime errors on ${testInfo.project.name}`).toEqual([]);
  });

  test('the technical status next to a Hold is explained, not rewritten', async ({ page }) => {
    await mockFinalActions(page, portfolioScenario);
    await page.goto('/portfolio');
    await page.getByRole('button', { name: /Final Action details for AAPL: Hold \/ Monitor/ }).first().click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toContainText('Technical signals');
    await expect(dialog).toContainText('Active Sell Trigger');            // the raw indicator stays visible
    await expect(dialog.locator('[data-testid="fad-conflict"]')).toContainText('not a portfolio decision');
    await expect(dialog).toContainText('Technical signal only — no portfolio-level trigger');
  });

  test('Loading and Not evaluated are distinct, and neither is a decision', async ({ page }) => {
    let release!: () => void;
    const gate = new Promise<void>((r) => (release = r));
    await mockFinalActions(page, async () => {
      await gate;
      return portfolioScenario();
    });
    await page.goto('/portfolio');
    await expect(page.locator('[data-testid="fa-loading"]:visible').first()).toBeVisible();
    await expect(page.getByText('Not evaluated')).toHaveCount(0);
    release();
    await expect(page.locator('[data-testid="fa-not-evaluated"]:visible').first()).toBeVisible();
    await expect(page.locator('[data-testid="fa-loading"]:visible')).toHaveCount(0);
  });

  test('an API error is labelled as such and never becomes a recommendation', async ({ page }) => {
    await mockFinalActions(page, () => ({ title: 'boom' }), 500);
    await page.goto('/portfolio');
    await expect(page.locator('[data-testid="fa-error"]:visible').first()).toContainText('API error');
    await expect(page.getByRole('button', { name: /Final Action details/ })).toHaveCount(0);
    await expect(page.locator('[data-testid="fa-not-evaluated"]')).toHaveCount(0);
    expect((await page.locator('body').textContent()) ?? '').not.toMatch(FORBIDDEN);
  });

  test('recovers on the next successful evaluation', async ({ page }) => {
    let served = 0;
    await mockFinalActions(page, () =>
      served++ === 0
        ? portfolioScenario()
        : snapshot([fa('SHOP.TO', 'Portfolio', { finalAction: 'Hold / Catalyst Monitoring', finalActionId: 'HOLD/CATALYST MONITORING' })]),
    );
    await page.goto('/portfolio');
    await expect(page.getByRole('button', { name: 'Final Action details for SHOP.TO: Awaiting Data' }).first()).toBeVisible();
    await page.reload();
    await expect(page.getByRole('button', { name: /Final Action details for SHOP\.TO: Hold \/ Catalyst Monitoring/ }).first()).toBeVisible();
    await expect(page.getByRole('button', { name: 'Final Action details for SHOP.TO: Awaiting Data' })).toHaveCount(0);
  });

  test('no horizontal overflow with Awaiting Data rows', async ({ page }) => {
    await mockFinalActions(page, portfolioScenario);
    await page.goto('/portfolio');
    await expect(page.getByRole('button', { name: 'Final Action details for SHOP.TO: Awaiting Data' }).first()).toBeVisible();
    await expectNoHorizontalPageOverflow(page);
  });
});

test.describe('Awaiting Data — Watchlist', () => {
  test('Awaiting Data and canonical statuses coexist; missing results are Not evaluated; errors are labelled', async ({ page }) => {
    await mockFinalActions(page, portfolioScenario);
    await page.goto('/watchlist');
    await expect(page.getByRole('button', { name: 'Final Action details for BAM.TO: Awaiting Data' }).first()).toBeVisible();
    await expect(page.getByRole('button', { name: 'Final Action details for SHOP: WAIT FOR PULLBACK' }).first()).toBeVisible();
    await expect(page.locator('[data-testid="fa-not-evaluated"]:visible').first()).toBeVisible();

    await page.getByRole('button', { name: 'Final Action details for BAM.TO: Awaiting Data' }).first().click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toContainText('The price is missing or invalid');
    await expect(dialog.locator('[data-testid="fad-action"]')).toContainText('Not actionable');
    await page.keyboard.press('Escape');

    await expectNoHorizontalPageOverflow(page);
  });

  test('API error is shown as such and never as a recommendation', async ({ page }) => {
    await mockFinalActions(page, () => ({ title: 'boom' }), 500);
    await page.goto('/watchlist');
    await expect(page.locator('[data-testid="fa-error"]:visible').first()).toContainText('API error');
    await expect(page.getByRole('button', { name: /Final Action details/ })).toHaveCount(0);
  });
});

const dashboardScenario = (): OpenDecisionsResponse => {
  const base = openDecisionsResponse.items[1]; // an Entry/Add holding decision
  const clone = (id: number, over: Record<string, unknown>) => ({ ...base, id, ...over });
  return {
    ...openDecisionsResponse,
    summary: { ...openDecisionsResponse.summary, entryAdd: 1, trimExit: 0, riskReview: 0, totalOpen: 1, awaitingData: 1, historicalWarnings: 1 },
    items: [clone(1, { symbol: 'LIVE.TO', effectiveState: 'CurrentValid', ageTradingDays: 0 })],
    notActionable: [
      clone(2, {
        symbol: 'BAD.TO',
        actionLabel: 'Add to Trade',
        effectiveState: 'AwaitingData',
        reasonCodes: ['AWAITING_STALE_DATA'],
        lastValidatedAtUtc: '2026-10-20T21:00:00Z',
      }),
      clone(3, {
        symbol: 'STRAT.TO',
        role: 'Strategic',
        bucket: 'TrimExit',
        actionLabel: 'Trim / Review',
        effectiveState: 'HistoricalWarning',
        isHistoricalWarning: true,
        reasonCodes: ['AWAITING_REFRESH_FAILED'],
      }),
    ],
  } as OpenDecisionsResponse;
};

test.describe('Awaiting Data — Dashboard and Action Center', () => {
  async function open(page: Page): Promise<void> {
    await page.addInitScript(() => {
      for (const key of ['dashboard_collapse_action-center', 'dashboard_collapse_m_action-center']) localStorage.setItem(key, 'false');
    });
    await page.route('**/api/dashboard/action-center', (route) => route.fulfill({ json: dashboardScenario() }));
    await page.goto('/dashboard');
    await expect(page.getByRole('heading', { level: 1, name: 'Dashboard' })).toBeVisible();
    await settle(page);
  }

  test('Awaiting Data and historical warnings are listed separately and never counted as decisions', async ({ page, errors }) => {
    const scans: string[] = [];
    page.on('request', (r) => {
      if (r.url().includes('/api/final-actions') && r.url().includes('fresh=true')) scans.push(r.url());
    });
    await open(page);

    await expect(page.locator('[data-testid="ac-total"]')).toHaveText('1');
    await expect(page.locator('[data-testid="ac-counter-Entry"] .ac-counter-count')).toHaveText('1');
    await expect(page.locator('[data-testid="ac-counter-ReduceExit"] .ac-counter-count')).toHaveText('0');
    await expect(page.locator('[data-testid="ac-counter-Risk"] .ac-counter-count')).toHaveText('0');
    await expect(page.locator('app-portfolio-actions-widget [data-testid="ac-row"]')).toHaveCount(1);

    const section = page.locator('[data-testid="ac-not-actionable"]');
    await expect(section).toContainText('1 awaiting data');
    await expect(section).toContainText('1 historical warning');
    await expect(section).toContainText('not counted as decisions');
    const rows = section.locator('[data-testid="ac-na-row"]');
    await expect(rows).toHaveCount(2);
    await expect(rows.filter({ hasText: 'BAD.TO' })).toContainText('Awaiting Data');
    await expect(rows.filter({ hasText: 'BAD.TO' })).toContainText('Last valid action: Add to Trade');
    await expect(rows.filter({ hasText: 'BAD.TO' })).toContainText('Market data is older than the last completed session');
    await expect(rows.filter({ hasText: 'STRAT.TO' })).toContainText('Historical warning');
    await expect(rows.filter({ hasText: 'STRAT.TO' })).toHaveClass(/ac-warning/);

    // Filtering by category never moves the not-actionable rows into the counts.
    await page.locator('[data-testid="ac-counter-ReduceExit"]').click();
    await expect(page.locator('[data-testid="ac-total"]')).toHaveText('1');
    await expect(rows).toHaveCount(2);

    expect(scans, 'opening the Dashboard must not trigger a market scan').toEqual([]);
    expect(errors).toEqual([]);
    await expectNoHorizontalPageOverflow(page);
  });
});

test.describe('Signal Tracker — lifecycle vs effective state', () => {
  test.beforeEach(() => {
    signalState.rows[0] = {
      ...signalState.rows[0],
      status: 'Active',
      signalType: 'FA:ADD/STAGED ENTRY',
      signalTypeLabel: 'Add / Staged Entry',
      effectiveState: 'AwaitingData',
      isActionable: false,
      evidenceHealth: 'Stale',
      dataAsOfDate: '2026-10-19',
      lastValidatedAtUtc: '2026-10-20T21:00:00Z',
      reasonCodes: ['AWAITING_STALE_DATA'],
    };
    signalState.rows[1] = { ...signalState.rows[1], status: 'Active', effectiveState: 'PriorSessionValid', isActionable: true, evidenceHealth: 'Unknown', reasonCodes: [] };
  });

  test('shows the lifecycle (Active) and the current state (Awaiting Data, not actionable) as separate tags; history stays', async ({ page }, testInfo) => {
    await page.goto('/signal-tracker');
    const lmt = page.locator('tr.signal-row:visible', { hasText: 'LMT.TO' });
    await expect(lmt).toBeVisible();
    await expect(lmt.locator('[data-testid="lifecycle-tag"]')).toHaveText('Lifecycle: Active');
    await expect(lmt.locator('[data-testid="effective-tag"]')).toContainText('Current: Awaiting Data');
    await expect(lmt.locator('[data-testid="effective-tag"]')).toContainText('not actionable');
    await expect(lmt.locator('[data-testid="effective-tag"]')).toHaveAttribute('aria-label', /Lifecycle: Active\. Current state: Awaiting Data \(not actionable\)/);
    // the historical record is untouched: original signal type, date and price remain.
    await expect(lmt).toContainText('Add / Staged Entry');
    if (!isPhone(testInfo.project.name)) await expect(lmt).toContainText('28.71');   // price is a secondary field on phone cards
    await expect(lmt).toContainText(/\d{4}-\d{2}-\d{2}/);                              // original signal date still shown

    const msft = page.locator('tr.signal-row:visible', { hasText: 'MSFT' });
    await expect(msft.locator('[data-testid="effective-tag"]')).toContainText('prior-session');
    await expect(msft.locator('[data-testid="effective-tag"]')).not.toContainText('not actionable');
    await settle(page);
    await expectNoHorizontalPageOverflow(page);
  });

  test('CSV export carries lifecycle status and effective state separately, without implying actionability', async ({ page }) => {
    await page.goto('/signal-tracker');
    await expect(page.locator('tr.signal-row:visible')).toHaveCount(3);
    await page.getByRole('button', { name: 'Export signals' }).click();
    const download = page.waitForEvent('download');
    await page.getByRole('menuitem', { name: /Export to CSV/ }).click();
    const csv = readFileSync(await (await download).path(), 'utf8');
    const [header, ...lines] = csv.replace(/^\uFEFF/, '').trim().split('\r\n');
    const cols = header.split(',');
    const lmt = lines.find((l) => l.startsWith('LMT.TO'))!.split(',');
    const at = (name: string) => lmt[cols.indexOf(name)];
    expect(cols).toEqual(expect.arrayContaining(['Lifecycle Status', 'Effective State', 'Actionable', 'Evidence Health', 'Last Evaluated (UTC)', 'Data As Of', 'Reason Codes']));
    expect(at('Lifecycle Status')).toBe('Active');
    expect(at('Effective State')).toBe('Awaiting Data');
    expect(at('Actionable')).toBe('No');
    expect(at('Evidence Health')).toBe('Stale');
    expect(at('Data As Of')).toBe('2026-10-19');
    expect(at('Reason Codes')).toBe('AWAITING_STALE_DATA');
    const msft = lines.find((l) => l.startsWith('MSFT'))!.split(',');
    expect(msft[cols.indexOf('Actionable')]).toBe('Yes');
  });

  test('a stale historical recommendation remains available and is not shown as a current decision on the phone cards', async ({ page }, testInfo) => {
    await page.goto('/signal-tracker');
    const table = page.locator('table.signals-table');
    if (isPhone(testInfo.project.name)) await expect(table).toHaveAttribute('data-responsive', 'cards');
    await expect(page.locator('tr.signal-row:visible', { hasText: 'LMT.TO' })).toBeVisible();
    await expect(page.locator('[data-testid="effective-tag"]:visible', { hasText: 'not actionable' })).toHaveCount(1);
  });
});
