import { readFileSync } from 'node:fs';
import type { Page } from '@playwright/test';
import { openTradesResponse, retireStopsAsPreviousTrade, swingState } from './fixtures/swing';
import { expectNoHorizontalPageOverflow, settle } from './support/overflow';
import { expect, test } from './support/test';

const isPhone = (name: string) => name.startsWith('mobile');

const rows = (page: Page) => page.locator('[data-testid="open-trades-table"] tr.mat-mdc-row');
const rowFor = (page: Page, symbol: string) => rows(page).filter({ hasText: symbol }).first();
const dialog = (page: Page) => page.locator('mat-dialog-container');

async function openSwing(page: Page) {
  await page.goto('/swing-trading');
  await expect(page.getByRole('heading', { level: 1, name: 'Swing Trading' })).toBeVisible();
  await expect(rows(page).first()).toBeAttached();
  await settle(page);
}

async function openManage(page: Page, symbol: string) {
  await rowFor(page, symbol).getByTestId('manage-risk-btn').click();
  await expect(dialog(page).getByRole('heading', { name: new RegExp(`Manage Risk — ${symbol}`) })).toBeVisible();
  await expect(dialog(page).getByTestId('mr-note')).toBeVisible();
}

async function tab(page: Page, name: string) {
  await dialog(page).getByRole('tab', { name }).click();
}

test.describe('PM-033 Swing Trading workspace', () => {
  test('refreshes quotes first, shows progress, reloads trades and journal, and keeps completion feedback', async ({ page, errors }) => {
    await openSwing(page);
    await page.getByRole('tab', { name: 'Trade Journal', exact: true }).click();
    await settle(page);
    let releaseQuotes!: () => void;
    const quotesGate = new Promise<void>((resolve) => { releaseQuotes = resolve; });
    const requests: string[] = [];
    page.on('request', (request) => {
      const path = new URL(request.url()).pathname;
      if (['/api/data/refresh', '/api/swing-trading/open-trades', '/api/swing-trading/journal'].includes(path)) requests.push(path);
    });
    await page.route('**/api/data/refresh', async (route) => {
      await quotesGate;
      await route.fallback();
    });
    const refresh = page.getByTestId('swing-refresh');
    await refresh.click();
    await expect(refresh).toBeDisabled();
    await expect(refresh).toContainText('Refreshing');
    await expect(refresh).toHaveAttribute('aria-busy', 'true');
    await expect(page.getByTestId('swing-refresh-status')).toContainText('Refreshing quotes');
    await expect.poll(() => requests).toEqual(['/api/data/refresh']);
    await page.route('**/api/swing-trading/open-trades', (route) => {
      const response = openTradesResponse();
      return route.fulfill({ json: { ...response, summary: { ...response.summary, openTrades: 9 } } });
    });
    releaseQuotes();
    await expect(page.getByTestId('swing-refresh-status')).toContainText('Last refreshed');
    await expect(refresh).toBeEnabled();
    await expect(page.getByTestId('sum-open')).toHaveText('9');
    expect(requests[0]).toBe('/api/data/refresh');
    expect(requests.filter((path) => path === '/api/data/refresh')).toHaveLength(1);
    expect(requests).toContain('/api/swing-trading/open-trades');
    expect(requests).toContain('/api/swing-trading/journal');
    await expectNoHorizontalPageOverflow(page);
    expect(errors).toEqual([]);
  });

  test('shows refresh failure on any tab and allows retry without false success', async ({ page }) => {
    await openSwing(page);
    await page.getByRole('tab', { name: 'Risk Monitor', exact: true }).click();
    await page.route('**/api/data/refresh', (route) => route.fulfill({ status: 500, json: { error: 'Test failure' } }));
    await page.getByTestId('swing-refresh').click();
    await expect(page.getByTestId('swing-refresh-error')).toContainText('Server unavailable');
    await expect(page.getByTestId('swing-refresh-status')).not.toContainText('Last refreshed');
    await expect(page.getByTestId('swing-refresh')).toBeEnabled();
    await page.unroute('**/api/data/refresh');
    await page.getByTestId('swing-refresh').click();
    await expect(page.getByTestId('swing-refresh-status')).toContainText('Last refreshed');
    await expect(page.getByTestId('swing-refresh-error')).toHaveCount(0);
  });

  test('shows the four summary metrics, as-of time and EOD wording without page overflow', async ({ page, errors }, info) => {
    await openSwing(page);
    await expect(page.getByTestId('sum-open')).toHaveText('8');
    await expect(page.getByTestId('sum-mv').first()).toContainText('CAD');
    await expect(page.getByTestId('sum-pnl').first()).toContainText('%');
    await expect(page.getByTestId('sum-attention')).not.toHaveText('—');
    await expect(page.getByTestId('swing-asof')).toContainText('Market data as of');
    await expect(page.getByTestId('swing-asof')).toContainText('Some prices are stale');
    await expect(page.getByTestId('swing-asof')).toContainText('no intraday monitoring');
    await expect(page.getByText(/Stop Downside/i)).toHaveCount(0);
    const tiles = page.getByTestId('swing-summary').locator('.sw-tile');
    await expect(tiles).toHaveCount(4);
    const styles = await tiles.evaluateAll((elements) => elements.map((el) => {
      const style = getComputedStyle(el);
      const label = el.querySelector('.sw-label')!.getBoundingClientRect();
      const values = Array.from(el.querySelectorAll('.sw-value')).map((value) => value.getBoundingClientRect());
      return {
        background: style.backgroundColor,
        border: style.borderTopColor,
        labelBelowValues: values.every((value) => value.bottom <= label.top),
      };
    }));
    expect(new Set(styles.map((style) => style.background)).size).toBe(4);
    expect(new Set(styles.map((style) => style.border)).size).toBe(4);
    expect(styles.every((style) => style.labelBelowValues)).toBe(true);
    const first = (await tiles.nth(0).boundingBox())!;
    const third = (await tiles.nth(2).boundingBox())!;
    if (info.project.name === 'desktop-chrome') {
      expect(third.y).toBe(first.y);
    } else {
      expect(third.y).toBeGreaterThan(first.y);
    }
    await expectNoHorizontalPageOverflow(page);
    expect(errors).toEqual([]);
  });

  test('lists every Swing position, highest risk first, and never claims full protection', async ({ page }, info) => {
    await openSwing(page);
    await expect(rows(page)).toHaveCount(8);
    await expect(rows(page).first()).toContainText('DIS.TO');
    await expect(rows(page).first().getByTestId('severity-chip')).toHaveText('Critical');
    await expect(page.getByText('Fully Protected')).toHaveCount(0);
    await expect(rowFor(page, 'TA.TO')).toContainText('Mixed');
    if (isPhone(info.project.name)) await page.getByLabel('Show all fields on each card').check();
    await expect(rowFor(page, 'WELL.TO')).toContainText('Unresolved');
    await expect(rowFor(page, 'EMP-A.TO')).toContainText('Stale');
    await expect(rowFor(page, 'EFR.TO')).toContainText('No Stop');
    await expect(rowFor(page, 'WELL.TO')).toContainText('Alert-Only Stop');
  });

  test('shows Stop Loss, Current Price, Protection and Manage Risk on every card/row', async ({ page }, info) => {
    await openSwing(page);
    const first = rows(page).first();
    await expect(first.getByTestId('protection-chip')).toBeVisible();
    const btn = first.getByTestId('manage-risk-btn');
    await expect(btn).toBeVisible();
    await expect(first).toContainText(/11\.50/);
    if (isPhone(info.project.name)) {
      const box = await btn.boundingBox();
      expect(box!.height).toBeGreaterThanOrEqual(44);
      await expect(page.locator('[data-testid="open-trades-table"]')).toHaveAttribute('data-responsive', 'cards');
    }
  });

  test('aligns protection details and risk badges with consistent spacing', async ({ page }, info) => {
    await openSwing(page);
    await settle(page);
    const first = rowFor(page, 'DIS.TO');
    const protection = first.locator('.mat-column-protection .st-status-stack');
    const risk = first.locator('.mat-column-risk .st-status-stack');
    await expect(protection).toBeVisible();
    await expect(risk).toBeVisible();
    const checkStack = async (stack: ReturnType<Page['locator']>) => {
      const measurements = await stack.evaluate((el) => {
        const children = Array.from(el.children).map((child) => child.getBoundingClientRect());
        const chips = Array.from(el.querySelectorAll('.st-status-chips .st-chip')).map((chip) => chip.getBoundingClientRect());
        return {
          gap: children[1].top - children[0].bottom,
          aligned: Math.abs(children[1].left - children[0].left) <= 1,
          chipGaps: chips.slice(1).map((chip, i) => chip.top - chips[i].bottom),
          contained: Array.from(el.querySelectorAll('.st-chip')).every((chip) => chip.scrollWidth <= chip.clientWidth + 1),
        };
      });
      expect(measurements.gap).toBeCloseTo(4, 0);
      expect(measurements.aligned).toBe(true);
      expect(measurements.contained).toBe(true);
      for (const gap of measurements.chipGaps) expect(gap).toBeCloseTo(4, 0);
    };
    await checkStack(protection);
    await checkStack(risk);
    if (!isPhone(info.project.name)) {
      expect((await protection.boundingBox())!.y).toBe((await risk.boundingBox())!.y);
    }
    await page.getByRole('tab', { name: 'Risk Monitor', exact: true }).click();
    const monitorRisk = page.getByTestId('risk-table').locator('tr.mat-mdc-row').first().locator('.mat-column-severity .st-status-stack');
    await expect(monitorRisk).toBeVisible();
    await checkStack(monitorRisk);
    await expectNoHorizontalPageOverflow(page);
  });

  test('filters by account, search, protection, risk status and attention, and can be cleared', async ({ page }) => {
    await openSwing(page);
    await page.getByTestId('swing-account').click();
    await page.getByRole('option', { name: 'Corp_TD' }).click();
    await expect(rows(page)).toHaveCount(3);

    await page.getByTestId('swing-reset').click();
    await expect(rows(page)).toHaveCount(8);

    await page.getByTestId('swing-search').fill('disney');
    await expect(rows(page)).toHaveCount(1);
    await page.getByTestId('swing-search').fill('zzz');
    await expect(page.getByTestId('swing-no-match')).toBeVisible();
    await page.getByTestId('swing-reset').click();

    await page.getByTestId('swing-protection').click();
    await page.getByRole('option', { name: 'Partially Covered' }).click();
    await expect(rows(page)).toHaveCount(1);
    await expect(rows(page).first()).toContainText('NOVO.TO');
    await page.getByTestId('swing-reset').click();

    await page.getByTestId('swing-risk').click();
    await page.getByRole('option', { name: 'Approaching stop' }).click();
    await expect(rows(page)).toHaveCount(1);
    await expect(rows(page).first()).toContainText('NVDA.TO');
    await page.getByTestId('swing-reset').click();

    await page.getByTestId('swing-attention').click();
    await expect(rows(page)).toHaveCount(4);
  });

  test('sorts the table by a column header', async ({ page }, info) => {
    test.skip(isPhone(info.project.name), 'Phones sort through the card controls (covered by the responsive-cards specs)');
    await openSwing(page);
    await page.getByRole('columnheader', { name: 'Symbol' }).click();
    await expect(rows(page).first()).toContainText('DIS.TO');
    await page.getByRole('columnheader', { name: 'Symbol' }).click();
    await expect(rows(page).first()).toContainText('WELL.TO');
  });

  test('shows the canonical Final Action read from the existing engine', async ({ page }) => {
    await openSwing(page);
    await expect(rowFor(page, 'DIS.TO')).toContainText('Hold Swing');
    await page.getByTestId('swing-final-action').click();
    await page.getByRole('option', { name: 'Hold / Trail Stop' }).click();
    await expect(rows(page)).toHaveCount(4);
  });

  test('exports exactly the filtered rows, with unknown coverage left blank', async ({ page }) => {
    await openSwing(page);
    await page.getByTestId('swing-search').fill('DIS.TO');
    await expect(rows(page)).toHaveCount(1);
    const [download] = await Promise.all([page.waitForEvent('download'), (async () => {
      await page.getByTestId('swing-export').click();
      await page.getByTestId('swing-export-csv').click();
    })()]);
    const csv = readFileSync(await download.path(), 'utf8').split('\n');
    expect(csv).toHaveLength(2);
    const header = csv[0].split(',');
    const cells = csv[1].split(',');
    expect(cells[header.indexOf('Symbol')]).toBe('DIS.TO');
    expect(cells[header.indexOf('Broker Stop Trigger')]).toBe('11.85');
    expect(cells[header.indexOf('Covered Quantity')]).toBe('');
    expect(cells[header.indexOf('Coverage Status')]).toBe('Unknown');
    expect(cells[header.indexOf('Planned Stop')]).toBe('');
    expect(cells[header.length - 1]).toBe('CAD');
  });
});

test.describe('PM-033 Risk Monitor', () => {
  test('puts breaches first and describes them as observations, not executions', async ({ page }) => {
    await openSwing(page);
    await page.getByRole('tab', { name: 'Risk Monitor' }).click();
    const banner = page.getByTestId('risk-breach-banner');
    await expect(banner).toContainText('at or below a recorded stop');
    await expect(banner).toContainText('market observation');
    await expect(banner).toContainText('not proof');
    await expect(banner).not.toContainText(/was executed|has been filled|order filled/i);
    const monitor = page.locator('[data-testid="risk-table"] tr.mat-mdc-row');
    await expect(monitor.first()).toContainText('DIS.TO');
    await expect(monitor.first().getByTestId('severity-chip')).toHaveText('Critical');
    await expect(monitor.first()).toContainText(/unknown/i);
    await expect(monitor.first().getByTestId('manage-risk-btn')).toBeVisible();
    await expectNoHorizontalPageOverflow(page);
  });

  test('stale evidence is labelled and produces no breach for that position', async ({ page }) => {
    await openSwing(page);
    await page.getByRole('tab', { name: 'Risk Monitor' }).click();
    const empA = page.locator('[data-testid="risk-table"] tr.mat-mdc-row').filter({ hasText: 'EMP-A.TO' });
    await expect(empA).toContainText('Stale');
    await expect(empA).not.toContainText('At/below stop');
  });
});

test.describe('PM-033 Manage Risk dialog', () => {
  test('keeps the planned stop and the broker stop distinct and says it never touches the broker', async ({ page }) => {
    await openSwing(page);
    await openManage(page, 'NOVO.TO');
    await expect(dialog(page).getByTestId('mr-note')).toContainText('never places, changes or cancels an order');
    await expect(dialog(page).getByTestId('mr-mismatch')).toContainText('does not change the broker order');
    await tab(page, 'Broker stops');
    await expect(dialog(page).getByTestId('mr-coverage')).toContainText('Partial');
  });

  test('validates the plan, saves it, shows the audit history and updates the grid', async ({ page }) => {
    await openSwing(page);
    await openManage(page, 'EFR.TO');

    const stop = dialog(page).getByTestId('mr-plan-stop');
    await stop.fill('-5');
    await expect(dialog(page).getByText('must be a positive price')).toBeVisible();
    await expect(dialog(page).getByTestId('mr-plan-save')).toBeDisabled();

    await stop.fill('13.9');
    await expect(dialog(page).getByTestId('mr-plan-warnings')).toContainText('at or above the latest price');
    await stop.fill('12.5');
    await dialog(page).getByTestId('mr-plan-target').fill('16');
    await dialog(page).getByTestId('mr-plan-save').click();
    await expect(dialog(page).getByTestId('mr-saved')).toContainText('No broker order was changed');

    await tab(page, 'History');
    await expect(dialog(page).getByTestId('mr-history')).toContainText('PlanCreated');
    await expect(dialog(page).getByTestId('mr-history')).toContainText('PlannedStopPrice: — → 12.5');

    await dialog(page).getByTestId('mr-close').click();
    await expect(dialog(page)).toHaveCount(0);
    await expect(rowFor(page, 'EFR.TO')).toContainText('12.50');
    await expect(rowFor(page, 'EFR.TO')).toContainText('Alert-Only Stop');
  });

  test('keeps risk plan notes single-line and preserves their saved values', async ({ page }) => {
    await openSwing(page);
    await openManage(page, 'EFR.TO');
    await settle(page);
    const notes = [
      { label: 'Stop rationale', value: 'Below support' },
      { label: 'Trade rationale / catalyst', value: 'Earnings momentum' },
      { label: 'Risk notes', value: 'Review position size' },
    ];
    const auditField = dialog(page).getByLabel('Reason for this change (audit)', { exact: true });
    const auditHeight = await auditField.locator('xpath=ancestor::mat-form-field').evaluate((el) => el.getBoundingClientRect().height);
    for (const { label, value } of notes) {
      const field = dialog(page).getByLabel(label, { exact: true });
      await expect(field).toHaveJSProperty('tagName', 'INPUT');
      const height = await field.locator('xpath=ancestor::mat-form-field').evaluate((el) => el.getBoundingClientRect().height);
      expect(height).toBeCloseTo(auditHeight, 0);
      await field.fill(value);
    }
    await expectNoHorizontalPageOverflow(page);
    await dialog(page).getByTestId('mr-plan-save').click();
    await expect(dialog(page).getByTestId('mr-saved')).toContainText('No broker order was changed');
    await dialog(page).getByTestId('mr-close').click();
    await expect(dialog(page)).toHaveCount(0);
    await openManage(page, 'EFR.TO');
    for (const { label, value } of notes) {
      await expect(dialog(page).getByLabel(label, { exact: true })).toHaveValue(value);
    }
  });

  test('keeps Save plan and Close on one footer row and only offers Save plan on its tab', async ({ page }) => {
    await openSwing(page);
    await openManage(page, 'EFR.TO');
    await settle(page);
    const footer = dialog(page).locator('mat-dialog-actions');
    const save = footer.getByTestId('mr-plan-save');
    const close = footer.getByTestId('mr-close');
    await expect(save).toBeVisible();
    await expect(close).toBeVisible();
    const saveBox = (await save.boundingBox())!;
    const closeBox = (await close.boundingBox())!;
    expect(Math.abs(saveBox.y - closeBox.y)).toBeLessThanOrEqual(1);
    expect(saveBox.x + saveBox.width).toBeLessThanOrEqual(closeBox.x);
    await tab(page, 'Broker stops');
    await expect(save).toHaveCount(0);
    await expect(close).toBeVisible();
    const addStop = footer.getByTestId('mr-add-stop');
    await expect(addStop).toBeVisible();
    const addBox = (await addStop.boundingBox())!;
    const brokerCloseBox = (await close.boundingBox())!;
    expect(Math.abs(addBox.y - brokerCloseBox.y)).toBeLessThanOrEqual(1);
    expect(addBox.x + addBox.width).toBeLessThanOrEqual(brokerCloseBox.x);
    expect(addBox.x).toBeGreaterThanOrEqual(0);
    expect(brokerCloseBox.x + brokerCloseBox.width).toBeLessThanOrEqual(page.viewportSize()!.width);
    await tab(page, 'History');
    await expect(save).toHaveCount(0);
    await expect(addStop).toHaveCount(0);
    await dialog(page).getByRole('tab', { name: 'History' }).press('Home');
    await dialog(page).getByRole('tab', { name: 'Risk plan' }).press('Enter');
    await expect(save).toBeVisible();
    await close.click();
    await expect(dialog(page)).toHaveCount(0);
  });

  test('records a partially covering broker stop and reflects it in the grid', async ({ page }) => {
    await openSwing(page);
    await openManage(page, 'EFR.TO');
    await tab(page, 'Broker stops');
    await expect(dialog(page).getByTestId('mr-no-stops')).toBeVisible();
    await dialog(page).getByTestId('mr-add-stop').click();
    await dialog(page).getByTestId('mr-stop-trigger').fill('10.75');
    await dialog(page).getByTestId('mr-stop-covered').fill('2000');
    await expect(dialog(page).getByTestId('mr-stop-covered-err')).toContainText('Exceeds the account quantity');
    await expect(dialog(page).getByTestId('mr-stop-save')).toBeDisabled();
    await dialog(page).getByTestId('mr-stop-covered').fill('400');
    await dialog(page).getByTestId('mr-stop-save').click();
    await expect(dialog(page).getByTestId('mr-coverage')).toContainText('Partial');
    await expect(dialog(page).getByTestId('mr-stop')).toContainText('Not broker-verified');
    await dialog(page).getByTestId('mr-close').click();
    await expect(rowFor(page, 'EFR.TO')).toContainText('Partially Covered');
    await expect(rowFor(page, 'EFR.TO')).not.toContainText('Fully Protected');
  });

  test('a stop saved without a covered quantity stays Unknown', async ({ page }) => {
    await openSwing(page);
    await openManage(page, 'EFR.TO');
    await tab(page, 'Broker stops');
    await dialog(page).getByTestId('mr-add-stop').click();
    await dialog(page).getByTestId('mr-stop-trigger').fill('10.75');
    await dialog(page).getByTestId('mr-stop-save').click();
    await expect(dialog(page).getByTestId('mr-coverage')).toContainText('Unknown');
    await expect(dialog(page).getByTestId('mr-stop')).toContainText('Unknown');
  });

  test('a concurrent change shows a conflict with a reload action instead of overwriting', async ({ page }) => {
    await openSwing(page);
    await openManage(page, 'EFR.TO');
    swingState.conflictNext = true;
    await dialog(page).getByTestId('mr-plan-stop').fill('12.5');
    await dialog(page).getByTestId('mr-plan-save').click();
    await expect(dialog(page).getByTestId('mr-error')).toContainText('changed by someone else');
    await expect(dialog(page).getByRole('button', { name: 'Reload latest' })).toBeVisible();
  });

  test('"it filled" is recorded as a user report and the order leaves the active list', async ({ page }) => {
    await openSwing(page);
    await openManage(page, 'DIS.TO');
    await tab(page, 'Broker stops');
    await dialog(page).getByRole('button', { name: 'It filled' }).click();
    await expect(dialog(page).getByTestId('mr-transition')).toContainText('not verified with the broker');
    await dialog(page).getByTestId('mr-transition-confirm').click();
    await expect(dialog(page).getByTestId('mr-saved')).toContainText('not verified');
    await expect(dialog(page).getByTestId('mr-no-stops')).toBeVisible();
  });

  test('a previous trade\'s order is not active and prompts a broker check', async ({ page }) => {
    retireStopsAsPreviousTrade('TFSA_L_TD', 'DIS.TO');
    await openSwing(page);
    await expect(rowFor(page, 'DIS.TO')).toContainText('No Stop');
    await openManage(page, 'DIS.TO');
    await expect(dialog(page).getByTestId('mr-closed-warning')).toContainText('may still be open');
  });

  test('is keyboard operable and closes with Escape', async ({ page }, info) => {
    test.skip(isPhone(info.project.name), 'Keyboard flow is a desktop/tablet-with-keyboard concern');
    await openSwing(page);
    await rowFor(page, 'DIS.TO').getByTestId('manage-risk-btn').focus();
    await page.keyboard.press('Enter');
    await expect(dialog(page)).toBeVisible();
    await expect(dialog(page).getByRole('tab', { name: 'Risk plan' })).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(dialog(page)).toHaveCount(0);
  });

  test('fits the phone width with 44px touch targets', async ({ page }, info) => {
    test.skip(!isPhone(info.project.name), 'Phone-only layout');
    await openSwing(page);
    await openManage(page, 'DIS.TO');
    await settle(page);
    const box = await dialog(page).boundingBox();
    const vp = page.viewportSize()!;
    expect(box!.x).toBeGreaterThanOrEqual(0);
    expect(box!.x + box!.width).toBeLessThanOrEqual(vp.width);
    expect(box!.width).toBeGreaterThanOrEqual(vp.width - 20);
    const save = await dialog(page).getByTestId('mr-plan-save').boundingBox();
    // 44 CSS px; the S21 emulation scales the 384px viewport slightly, so allow rounding.
    expect(save!.height).toBeGreaterThanOrEqual(43);
    await expectNoHorizontalPageOverflow(page);
  });
});

test.describe('PM-033 Trade Journal', () => {
  test('lists completed trades without merging separate episodes and shows details and history', async ({ page }) => {
    await openSwing(page);
    await page.getByRole('tab', { name: 'Trade Journal' }).click();
    const jr = page.locator('[data-testid="journal-table"] tr.mat-mdc-row');
    await expect(jr).toHaveCount(3);
    await expect(jr.filter({ hasText: 'ABC.TO' })).toHaveCount(2);
    await expect(jr.filter({ hasText: 'UNL.TO' })).toContainText('Unlinked');

    await jr.first().getByTestId('journal-details-btn').click();
    await expect(dialog(page).getByRole('heading', { name: /Trade details/ })).toBeVisible();
    await expect(dialog(page)).toContainText('Earnings catalyst');
    await expect(dialog(page).getByTestId('journal-history')).toContainText('PlanCreated');
    await page.keyboard.press('Escape');

    await page.getByTestId('journal-outcome').click();
    await page.getByRole('option', { name: 'Loss' }).click();
    await expect(jr).toHaveCount(1);
    await expectNoHorizontalPageOverflow(page);
  });

  test('exports the journal with numeric prices and blank unknowns', async ({ page }) => {
    await openSwing(page);
    await page.getByRole('tab', { name: 'Trade Journal' }).click();
    await expect(page.getByTestId('journal-count')).toContainText('3 of 3');
    const [download] = await Promise.all([page.waitForEvent('download'), (async () => {
      await page.getByTestId('journal-export').click();
      await page.getByTestId('journal-export-csv').click();
    })()]);
    const csv = readFileSync(await download.path(), 'utf8').split('\n');
    expect(csv).toHaveLength(4);
    expect(csv[0]).toContain('Realized P/L');
    expect(csv.find((l) => l.startsWith('ABC.TO,TFSA_D_TD,2026-06-01'))).toContain('140.00');
  });
});

test.describe('PM-033 Portfolio entry point', () => {
  test('opens the same shared Manage Risk dialog from the Portfolio grid', async ({ page }) => {
    await page.goto('/portfolio');
    const btn = page.getByTestId('portfolio-manage-risk').first();
    await expect(btn).toBeAttached();
    await btn.scrollIntoViewIfNeeded();
    await btn.click();
    await expect(dialog(page).getByRole('heading', { name: /Manage Risk —/ })).toBeVisible();
    await expect(dialog(page).getByTestId('mr-note')).toBeVisible();
  });
});
