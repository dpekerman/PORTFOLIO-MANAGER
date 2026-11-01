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
  test('keeps previous-episode broker reminders visible and requires an audited resolution', async ({ page, errors }) => {
    retireStopsAsPreviousTrade('TFSA_L_TD', 'DIS.TO');
    await openSwing(page);
    await page.getByRole('tab', { name: 'Risk Monitor', exact: true }).click();
    const reminders = page.getByTestId('broker-order-reminders');
    await expect(reminders).toContainText('DIS.TO');
    await expect(reminders).toContainText('not protection for a new Swing episode');
    await reminders.getByRole('button', { name: 'Review broker order' }).click();
    await tab(page, 'Broker stops');
    await dialog(page).locator('summary').filter({ hasText: 'Inactive and previous-trade records' }).click();
    await dialog(page).getByRole('button', { name: 'I cancelled it', exact: true }).click();
    await expect(dialog(page).getByTestId('mr-transition-confirm')).toBeDisabled();
    await dialog(page).getByTestId('mr-transition').getByRole('textbox').fill('Synthetic broker cancellation confirmation');
    await dialog(page).getByTestId('mr-transition-confirm').click();
    await expect(dialog(page).getByTestId('mr-saved')).toContainText('Status recorded as reported by you');
    await dialog(page).getByTestId('mr-close').click();
    await expect(reminders).toHaveCount(0);
    await expectNoHorizontalPageOverflow(page);
    expect(errors).toEqual([]);
  });
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
    if (isPhone(info.project.name)) {
      const showAll = page.getByLabel('Show all fields on each card');
      await showAll.click();
      await expect(showAll).toHaveAttribute('aria-checked', 'true');
    }
    await expect(rowFor(page, 'WELL.TO')).toContainText('Unresolved');
    await expect(rowFor(page, 'EMP-A.TO')).toContainText('Stale');
    await expect(rowFor(page, 'EFR.TO').getByTestId('protection-chip')).toHaveText('None');
    await expect(rowFor(page, 'WELL.TO').getByTestId('protection-chip')).toHaveText('Planned only');
    await expect(rowFor(page, 'TA.TO').getByTestId('protection-chip')).toHaveText('Ambiguous');
    await expect(rowFor(page, 'NOVO.TO').getByTestId('protection-chip')).toHaveText('Partial');
    await expect(rowFor(page, 'DIS.TO').getByTestId('protection-chip')).toHaveText('Unknown');
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

  test('aligns protection and risk badges with consistent spacing and no overflow', async ({ page }, info) => {
    await openSwing(page);
    await settle(page);
    const first = rowFor(page, 'DIS.TO');
    const protection = first.getByTestId('protection-chip');
    const risk = first.locator('.mat-column-risk .st-status-stack');
    await expect(protection).toBeVisible();
    await expect(risk).toBeVisible();
    const protectionFits = await protection.evaluate((el) => el.scrollWidth <= el.clientWidth + 1);
    expect(protectionFits).toBe(true);
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
    await checkStack(risk);
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
    await expect(rowFor(page, 'EFR.TO').getByTestId('protection-chip')).toHaveText('Planned only');
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
    await expect(rowFor(page, 'EFR.TO').getByTestId('protection-chip')).toHaveText('Partial');
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
    await expect(dialog(page).getByTestId('mr-transition-confirm')).toBeDisabled();
    await dialog(page).getByTestId('mr-transition').getByRole('textbox').fill('Confirmed on synthetic broker statement');
    await expect(dialog(page).getByTestId('mr-transition-confirm')).toBeEnabled();
    await dialog(page).getByTestId('mr-transition-confirm').click();
    await expect(dialog(page).getByTestId('mr-saved')).toContainText('not verified');
    await expect(dialog(page).getByTestId('mr-no-stops')).toBeVisible();
  });

  test('a previous trade\'s order is not active and prompts a broker check', async ({ page }) => {
    retireStopsAsPreviousTrade('TFSA_L_TD', 'DIS.TO');
    await openSwing(page);
    await expect(rowFor(page, 'DIS.TO').getByTestId('protection-chip')).toHaveText('None');
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

async function showAllFields(page: Page, name: string) {
  if (!isPhone(name)) return;
  const showAll = page.getByLabel('Show all fields on each card').first();
  if ((await showAll.getAttribute('aria-checked')) !== 'true') await showAll.click();
}

test.describe('PM-016 Swing grid cleanup', () => {
  test('price shows no repeated basis label; EOD basis, as-of and source live in the details; stale keeps a visible warning', async ({ page }, info) => {
    await openSwing(page);
    await showAllFields(page, info.project.name);
    await expect(page.getByText('EOD close')).toHaveCount(0);
    await expect(page.getByText('Live', { exact: true })).toHaveCount(0);
    const dis = rowFor(page, 'DIS.TO');
    await expect(dis.getByTestId('price-warning')).toHaveCount(0);
    const label = await dis.getByTestId('price-value').getAttribute('aria-label');
    expect(label).toContain('End-of-day close (not a live intraday quote)');
    expect(label).toContain('2026-10-28 20:00 UTC');
    const stale = rowFor(page, 'EMP-A.TO').getByTestId('price-warning');
    await expect(stale).toBeVisible();
    await expect(stale).toContainText('Stale');
    await expectNoHorizontalPageOverflow(page);
  });

  test('a breached Planned Stop is red with one flag, a full explanation and no broker-execution claim', async ({ page }, info) => {
    await openSwing(page);
    await showAllFields(page, info.project.name);
    const cell = rowFor(page, 'WELL.TO').getByTestId('planned-stop-cell');
    await expect(cell).toHaveAttribute('data-state', 'Breached');
    await expect(cell.getByTestId('planned-stop-flag')).toHaveCount(1);
    await expect(cell).toContainText('4.70');
    const red = await cell.evaluate((el) => getComputedStyle(el).backgroundColor);
    expect(red).not.toBe('rgba(0, 0, 0, 0)');
    const flag = cell.getByTestId('planned-stop-flag');
    const name = (await flag.getAttribute('aria-label'))!;
    expect(name).toContain('Planned Stop 4.70');
    expect(name).toContain('4.60');
    expect(name).toContain('2026-10-28 20:00 UTC');
    expect(name).toContain('BREACHED');
    expect(name).toContain('does not show that a broker order triggered or filled');
    await flag.click();
    await expect(cell.locator('[role="note"]')).toContainText('BREACHED');
    // The whole row is not painted red, and a clear stop has no flag.
    await expect(rowFor(page, 'NOVO.TO').getByTestId('planned-stop-flag')).toHaveCount(0);
    await expectNoHorizontalPageOverflow(page);
  });

  test('the breach detail opens from the keyboard', async ({ page }, info) => {
    test.skip(isPhone(info.project.name), 'Keyboard interaction is covered on desktop and tablet');
    await openSwing(page);
    const flag = rowFor(page, 'WELL.TO').getByTestId('planned-stop-flag');
    await flag.focus();
    await expect(flag).toBeFocused();
    await expect(flag).toHaveAttribute('aria-expanded', 'false');
    await page.keyboard.press('Enter');
    await expect(flag).toHaveAttribute('aria-expanded', 'true');
  });

  test('stale prices show Unverified instead of a breach or an all-clear', async ({ page }, info) => {
    await openSwing(page);
    await showAllFields(page, info.project.name);
    const cell = rowFor(page, 'EMP-A.TO').getByTestId('planned-stop-cell');
    await expect(cell).toHaveAttribute('data-state', 'Unverified');
    await expect(cell.getByTestId('planned-stop-flag')).toContainText('Unverified');
    expect(await cell.evaluate((el) => el.classList.contains('ps-breached'))).toBe(false);
    expect(await cell.getByTestId('planned-stop-flag').getAttribute('aria-label')).toContain('neither confirmed nor ruled out');
  });

  test('a broker-stop-only breach keeps its risk flag but does not colour the Planned Stop', async ({ page }, info) => {
    await openSwing(page);
    await showAllFields(page, info.project.name);
    const dis = rowFor(page, 'DIS.TO');
    await expect(dis).toContainText('At/below stop');
    await expect(dis.getByTestId('planned-stop-cell')).toHaveCount(0);
    await expect(dis.getByTestId('broker-stop-chip')).toContainText('Unknown');
  });

  test('broker stop and protection states stay distinct', async ({ page }, info) => {
    await openSwing(page);
    await showAllFields(page, info.project.name);
    await expect(rowFor(page, 'WELL.TO').getByTestId('broker-stop-chip')).toHaveText('Planned only');
    await expect(rowFor(page, 'EFR.TO').getByTestId('broker-stop-chip')).toHaveText('Missing');
    await expect(rowFor(page, 'NOVO.TO').getByTestId('broker-stop-chip')).toHaveText('Partial');
    await expect(rowFor(page, 'MAL.TO').getByTestId('broker-stop-chip')).toHaveText('Unknown');
    await expect(page.getByText('Fully Protected')).toHaveCount(0);
  });

  test('Final Action uses the shared badge and still opens its details', async ({ page }, info) => {
    await openSwing(page);
    await showAllFields(page, info.project.name);
    const badge = rowFor(page, 'DIS.TO').getByTestId('final-action-badge');
    await expect(badge).toContainText('Hold Swing');
    await expect(badge).toHaveAttribute('aria-label', /Final Action details for DIS\.TO: Hold Swing/);
    await badge.click();
    await expect(dialog(page)).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(dialog(page)).toHaveCount(0);
  });

  test('Manage Risk still opens from the cleaned-up row', async ({ page }) => {
    await openSwing(page);
    await openManage(page, 'WELL.TO');
  });

  test('Breached Planned Stops quick filter shows confirmed breaches only and works with other filters', async ({ page }, info) => {
    await openSwing(page);
    await page.getByRole('tab', { name: 'Risk Monitor', exact: true }).click();
    const monitorRows = page.getByTestId('risk-table').locator('tr.mat-mdc-row');
    await expect(monitorRows).toHaveCount(8);
    const quick = page.getByTestId('swing-breached-planned');
    await expect(quick).toContainText('Breached Planned Stops (1)');
    await expect(quick).toHaveAttribute('aria-pressed', 'false');

    await quick.click();
    await expect(quick).toHaveAttribute('aria-pressed', 'true');
    await expect(monitorRows).toHaveCount(1);
    await expect(monitorRows.first()).toContainText('WELL.TO');
    await expect(page.getByTestId('risk-count')).toContainText('1 of 8');
    await expect(page.getByTestId('risk-count')).toContainText('1 more with an unverified price are not counted as breached');

    // Broker-only breach (DIS.TO) and the stale row are not included; compatible filters still apply.
    await page.getByTestId('swing-account').click();
    await page.getByRole('option', { name: 'TFSA_L_TD' }).click();
    await expect(page.getByTestId('risk-no-match')).toContainText('No Swing position has a confirmed breached Planned Stop');
    await expect(page.getByTestId('risk-no-match')).toContainText('1 with a stale or unavailable price cannot be verified');

    await page.getByTestId('swing-reset').click();
    await expect(monitorRows).toHaveCount(8);
    await expect(quick).toHaveAttribute('aria-pressed', 'false');
    await expectNoHorizontalPageOverflow(page);
  });

  test('the quick filter also applies to Open Trades because the filter bar is shared', async ({ page }) => {
    await openSwing(page);
    await page.getByTestId('swing-breached-planned').click();
    await expect(rows(page)).toHaveCount(1);
    await expect(rows(page).first()).toContainText('WELL.TO');
    await expect(page.getByTestId('swing-count')).toContainText('1 of 8');
    await page.getByTestId('swing-search').fill('zzz');
    await expect(page.getByTestId('swing-no-match')).toContainText('No Swing position has a confirmed breached Planned Stop');
  });

  test('fits the phone width with no badge overflow and keeps touch targets usable', async ({ page }, info) => {
    test.skip(!isPhone(info.project.name), 'Phone-only layout check');
    await openSwing(page);
    await showAllFields(page, info.project.name);
    const flag = rowFor(page, 'WELL.TO').getByTestId('planned-stop-flag');
    await expect(flag).toBeVisible();
    expect((await flag.boundingBox())!.height).toBeGreaterThanOrEqual(32);
    const quick = page.getByTestId('swing-breached-planned');
    expect((await quick.boundingBox())!.height).toBeGreaterThanOrEqual(44);
    const overflow = await page.locator('[data-testid="open-trades-table"]').evaluate((table) =>
      Array.from(table.querySelectorAll('.st-chip, app-final-action-badge')).some((el) => {
        const r = el.getBoundingClientRect();
        return r.right > window.innerWidth + 1 || r.left < -1;
      }));
    expect(overflow).toBe(false);
    await expectNoHorizontalPageOverflow(page);
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
