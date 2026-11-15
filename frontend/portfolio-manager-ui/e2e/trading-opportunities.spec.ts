import type { Page } from '@playwright/test';
import { expect, test } from './support/test';
import { expectNoHorizontalPageOverflow, settle } from './support/overflow';
import { opportunityDetailResponse, opportunitySnapshotResponse } from './fixtures/opportunities';
import { setupQualityFixture } from '../src/app/features/trading-opportunities/testing/opportunity-fixtures';
import { join } from 'node:path';

// PM-029 Trading Opportunities. Data comes from e2e/fixtures/opportunities.ts (synthetic values).

const isDesktop = (name: string) => name === 'desktop-chrome';
const table = (page: Page) => page.getByTestId('opportunities-table');
const dataRows = (page: Page) => table(page).locator('tr.opp-row');
const rowFor = (page: Page, symbol: string) =>
  table(page).locator(`tr.opp-row[data-symbol="${symbol}"]`);
const visibleSymbols = async (page: Page) =>
  (
    await dataRows(page).evaluateAll((rows) => rows.map((r) => r.getAttribute('data-symbol')))
  ).sort();

async function openFilters(page: Page) {
  const panel = page.locator('.app-filter-panel');
  if (!(await panel.evaluate((d) => (d as HTMLDetailsElement).open))) {
    await panel.locator('> summary').click();
  }
}

async function selectFilterOption(page: Page, filterId: string, option: string) {
  await page.getByTestId(filterId).click();
  await page.getByRole('option', { name: option, exact: true }).click();
  await page.keyboard.press('Escape');
  await expect(page.getByRole('listbox')).toHaveCount(0);
}

async function addSyntheticScreenshotLabel(page: Page) {
  await page.addStyleTag({
    content: `
      [data-pm029-synthetic-watermark] {
        position: fixed;
        right: 4px;
        top: 4px;
        z-index: 2147483647;
        padding: 2px 5px;
        border: 1px solid #7f1d1d;
        border-radius: 4px;
        background: #fff7ed;
        color: #7f1d1d;
        font: 700 9px/1.2 sans-serif;
        letter-spacing: .04em;
        pointer-events: none;
      }
    `,
  });
  await page.evaluate(() => {
    const watermark = document.createElement('div');
    watermark.dataset['pm029SyntheticWatermark'] = 'true';
    watermark.textContent = 'SYNTHETIC | NOT PRODUCTION';
    watermark.setAttribute('aria-hidden', 'true');
    document.body.append(watermark);
  });
}

test.describe('PM-029 Trading Opportunities', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/trading-opportunities');
    await expect(
      page.getByRole('heading', { level: 1, name: /Trading Opportunities/ }),
    ).toBeVisible();
    await expect(dataRows(page).first()).toBeVisible();
  });

  test('default view shows 70+ swing scores with coloured cells and signed sector heat', async ({
    page,
    mocks,
    errors,
  }, testInfo) => {
    expect(await visibleSymbols(page)).toEqual(['PLW.TO', 'QRK.TO', 'ZEN.NE']);

    const strong = rowFor(page, 'QRK.TO').getByTestId('score-cell').first();
    await expect(strong).toHaveClass(/heat--strong/);
    await expect(strong).toContainText('88');
    const bg = await strong.evaluate((el) => getComputedStyle(el).backgroundColor);
    expect(bg).not.toMatch(/rgba\(0, 0, 0, 0\)|transparent/);

    const provisional = rowFor(page, 'PLW.TO').getByTestId('score-cell').first();
    await expect(provisional).toHaveClass(/heat--provisional/);
    await expect(provisional).toContainText('P');

    await expect(rowFor(page, 'QRK.TO').getByTestId('sector-heat')).toHaveClass(
      /sh--benchmark-strong/,
    );
    await expect(rowFor(page, 'QRK.TO').getByTestId('sector-heat')).toContainText('+3.6 pp');
    const heatBg = await rowFor(page, 'QRK.TO')
      .getByTestId('sector-heat')
      .evaluate((el) => getComputedStyle(el).backgroundColor);
    expect(heatBg).not.toBe(bg);
    await expect(rowFor(page, 'PLW.TO').getByTestId('sector-heat')).toHaveClass(
      /sh--benchmark-strong/,
    );
    // Typographic minus sign (U+2212), as rendered by the badge.
    await expect(rowFor(page, 'PLW.TO').getByTestId('sector-heat')).toContainText('\u22122.4 pp');

    const summary = page.getByTestId('exclusion-summary');
    await expect(summary).toContainText('Showing 3 of 5');
    await expect(summary).toContainText('2 below threshold');
    await expect(summary).not.toContainText('unscorable');

    await expectNoHorizontalPageOverflow(page);
    if (testInfo.project.name === 'tablet') {
      await expect(page.getByTestId('tablet-grid-scroll-hint')).toBeVisible();
      const { clientWidth, scrollWidth } = await page
        .locator('.grid-scroll')
        .evaluate((element) => ({
          clientWidth: element.clientWidth,
          scrollWidth: element.scrollWidth,
        }));
      expect(scrollWidth).toBeGreaterThan(clientWidth);
    }
    expect(mocks.unmocked).toEqual([]);
    expect(errors).toEqual([]);
  });

  test('a missing trade setup does not hide a technical Swing score', async ({ page, errors }) => {
    await page.getByTestId('score-filter').click();
    await page.getByRole('option', { name: 'All (incl. unavailable)', exact: true }).click();
    await expect
      .poll(() => visibleSymbols(page))
      .toEqual(['BRQ.TO', 'CCA.TO', 'PLW.TO', 'QRK.TO', 'ZEN.NE']);
    await expect(rowFor(page, 'CCA.TO').getByTestId('score-cell').first()).toContainText('28');
    await expect(rowFor(page, 'CCA.TO').getByTestId('score-cell').first()).toHaveClass(
      /heat--weak/,
    );
    await expect(rowFor(page, 'BRQ.TO').getByTestId('score-cell').first()).toHaveClass(
      /heat--weak/,
    );
    expect(errors).toEqual([]);
  });

  test('strategic perspective scores independently of a missing swing setup', async ({
    page,
    errors,
  }) => {
    await page.getByTestId('perspective-strategic').click();
    // CCA has no Swing setup but a valid technical score; ZEN lacks fundamentals for Strategic.
    await openFilters(page);
    await page.getByTestId('score-filter').click();
    await page.getByRole('option', { name: /All/ }).first().click();
    await expect(rowFor(page, 'CCA.TO')).toBeVisible();
    await expect(page.getByTestId('exclusion-summary')).toContainText('1 unscorable');
    expect(errors).toEqual([]);
  });

  test('shows a strategy-appropriate scoring disclaimer', async ({ page }) => {
    const disclaimer = page.getByTestId('scoring-disclaimer');
    await expect(disclaimer).toContainText('Swing-v2 is an evidence-adjusted technical ranking');

    await page.getByTestId('perspective-strategic').click();
    await expect(disclaimer).toContainText(
      'Strategic scores summarize available long-term trend, technical, fundamental, and valuation evidence.',
    );
    await expect(disclaimer).not.toContainText('Swing-v2');

    await page.getByTestId('perspective-swing').click();
    await expect(disclaimer).toContainText('Swing-v2 is an evidence-adjusted technical ranking');
  });

  test('keeps QRK heat, score evidence, target and scanner stop consistent across tabs', async ({
    page,
    errors,
  }) => {
    const qrk = rowFor(page, 'QRK.TO');
    await expect(qrk.getByTestId('sector-heat')).toContainText('+3.6 pp');
    await qrk.click();
    const panel = page.getByTestId('details-panel');

    await expect(panel.getByTestId('setup-target-source')).toContainText('52-week high');
    await expect(panel).toContainText('Scanner Dynamic stop');
    await expect(panel).toContainText('CAD 45.31');
    await expect(panel).toContainText('CAD 57.84');
    await expect(panel).toContainText('3.33 : 1');
    await expect(panel).toContainText('nearest eligible scanner structural level');

    await panel.getByRole('tab', { name: 'Technicals' }).click();
    await expect(panel).toContainText('Relative strength 20D+3.6 pp');
    await expect(panel).toContainText('Relative strength 60D+5.8 pp');
    await expect(panel).toContainText('Dynamic stop (scanner)');
    await expect(panel).toContainText('CAD 45.31');

    await panel.getByRole('tab', { name: 'Scores' }).click();
    await expect(panel).toContainText('70.00 / 80.00 technical points');
    const s2 = panel
      .locator('.score-block[data-perspective="Swing"] details.component')
      .filter({ hasText: 'Relative Strength' });
    await expect(s2).toContainText('17.00 / 20.00');
    await s2.locator('summary').click();
    await expect(s2).toContainText('20D vs sector');
    await expect(s2).toContainText('+3.6 pp');
    await expect(s2).toContainText('+5.8 pp');

    await panel.getByRole('tab', { name: 'Evidence' }).click();
    await expect(panel).toContainText('XIT.TO · Technology benchmark');
    await expect(panel).toContainText('Synthetic E2E fixture');
    await expect(panel).toContainText('Fixture metadata only; not production mapping validation.');
    await expect(panel).toContainText('Price return (split-adjusted, dividends excluded)');

    expect(errors).toEqual([]);
  });

  test('filters by industry, risk, account, role, and freshness with a reachable reset action', async ({
    page,
  }) => {
    await openFilters(page);

    await selectFilterOption(page, 'industry-filter', 'Banking');
    await expect.poll(() => visibleSymbols(page)).toEqual(['PLW.TO']);
    await page.getByTestId('reset-opportunity-filters').click();

    await selectFilterOption(page, 'risk-filter', 'High');
    await expect(table(page).locator('.no-data')).toContainText(
      'No securities match the current filters',
    );
    await page.getByTestId('empty-action').click();
    await expect.poll(() => visibleSymbols(page)).toEqual(['PLW.TO', 'QRK.TO', 'ZEN.NE']);

    await selectFilterOption(page, 'account-filter', 'RRSP_E2E');
    await expect.poll(() => visibleSymbols(page)).toEqual(['PLW.TO']);
    await page.getByTestId('reset-opportunity-filters').click();

    await selectFilterOption(page, 'role-filter', 'Strategic');
    await expect.poll(() => visibleSymbols(page)).toEqual(['PLW.TO']);
    await page.getByTestId('reset-opportunity-filters').click();

    await selectFilterOption(page, 'freshness-filter', 'Stale');
    await expect(table(page).locator('.no-data')).toContainText(
      'No securities match the current filters',
    );
    await page.getByTestId('empty-action').click();
    await expect.poll(() => visibleSymbols(page)).toEqual(['PLW.TO', 'QRK.TO', 'ZEN.NE']);
  });

  test('row opens the four-tab details with every authoritative decision', async ({
    page,
    errors,
  }, testInfo) => {
    await rowFor(page, 'PLW.TO').click();
    const panel = page.getByTestId('details-panel');
    await expect(panel).toBeVisible();
    await expect(panel).toHaveAttribute(
      'role',
      isDesktop(testInfo.project.name) ? 'complementary' : 'dialog',
    );

    await expect(panel.getByTestId('details-final-action')).toContainText('Multiple (2)');
    await expect(panel.getByTestId('setup-target-source')).toContainText(
      'Reference/source: 52-week high',
    );
    await expect(panel).toContainText('Target quality');
    await expect(panel).toContainText('Not assessed; no approved quality model is active.');
    if (testInfo.project.name.startsWith('mobile-')) {
      await expect(panel.getByTestId('details-tab-scroll-hint')).toBeVisible();
    }
    await expect(panel.getByTestId('setup-risk-reward')).toBeVisible();
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
    await expect(panel).toContainText('pm029-v2');

    await expectNoHorizontalPageOverflow(page);
    await page.getByTestId('details-close').click();
    await expect(panel).toHaveCount(0);
    if (isDesktop(testInfo.project.name)) await expect(rowFor(page, 'PLW.TO')).toBeFocused();
    expect(errors).toEqual([]);
  });

  test('unavailable setup shows no fabricated levels beside the independent technical score', async ({
    page,
    errors,
  }) => {
    await page.getByTestId('score-filter').click();
    await page.getByRole('option', { name: 'All (incl. unavailable)', exact: true }).click();
    await rowFor(page, 'CCA.TO').click();
    const panel = page.getByTestId('details-panel');
    await expect(panel).toContainText('No structural target');
    await expect(panel.getByTestId('details-final-action')).toContainText('Watch');
    await panel.getByRole('tab', { name: 'Scores' }).click();
    await expect(panel).toContainText('28/100');
    await expect(panel).toContainText('22.00 / 80.00 technical points');
    await expect(panel).toContainText('60.00 / 80.00 evidence available');
    await expect(panel).toContainText('Trend & Structure');
    await expect(panel).toContainText('0.00 / 25.00');
    await expect(panel).toContainText('8.00 / 15.00');
    await expect(panel).toContainText('4.00 / 10.00');
    await expect(panel).toContainText('10.00 / 10.00');
    await expect(panel).toContainText('Sector benchmark is not mapped');
    await expect(panel).not.toContainText('SectorHeat:Unmapped');
    await expect(panel).toContainText('Market setup and execution readiness are separate');
    await expect(panel).not.toContainText('Reward : risk');
    await page.keyboard.press('Escape');
    await expect(panel).toHaveCount(0);
    expect(errors).toEqual([]);
  });

  test('columns selector opens from the page', async ({ page, errors }) => {
    await page
      .getByRole('button', { name: /^Columns \(\d+\)/ })
      .first()
      .click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText('Swing · Trading Opportunities');
    await expect(dialog).toContainText('Sector / Theme Heat');
    await expect(dialog).toContainText('Market R');
    await expect(dialog).toContainText('Manual R');
    await page.keyboard.press('Escape');
    await expect(dialog).toHaveCount(0);
    await page.getByTestId('perspective-strategic').click();
    await page
      .getByRole('button', { name: /^Columns \(\d+\)/ })
      .first()
      .click();
    await expect(page.getByRole('dialog')).toContainText('Strategic · Trading Opportunities');
    await page.keyboard.press('Escape');
    expect(errors).toEqual([]);
  });

  test('manual R scenarios remain isolated by symbol and strategy perspective', async ({
    page,
  }) => {
    const qrk = rowFor(page, 'QRK.TO');
    await qrk.click();
    await page.getByTestId('manual-r-entry').fill('50');
    await page.getByTestId('manual-r-stop').fill('45');
    await page.getByTestId('manual-r-target').fill('60');
    await expect(page.getByTestId('manual-r-result')).toContainText('2.00 : 1');
    await page.getByTestId('details-close').click();

    await page.getByTestId('perspective-strategic').click();
    await qrk.click();
    await expect(page.getByTestId('manual-r-entry')).toHaveValue('');
    await page.getByTestId('manual-r-entry').fill('50');
    await page.getByTestId('manual-r-stop').fill('45');
    await page.getByTestId('manual-r-target').fill('55');
    await expect(page.getByTestId('manual-r-result')).toContainText('1.00 : 1');
    await page.getByTestId('details-close').click();

    await page.getByTestId('perspective-swing').click();
    await qrk.click();
    await expect(page.getByTestId('manual-r-result')).toContainText('2.00 : 1');
  });

  test('manual R cannot change either score or portfolio evidence', async ({ page }, testInfo) => {
    const qrk = rowFor(page, 'QRK.TO');
    const isHandset = testInfo.project.name.startsWith('mobile-');
    const evidenceColumns = isHandset
      ? ['.mat-column-swing', '.mat-column-finalAction', '.mat-column-sectorHeat']
      : [
          '.mat-column-swing',
          '.mat-column-rr',
          '.mat-column-fit',
          '.mat-column-finalAction',
          '.mat-column-sectorHeat',
        ];
    expect(await qrk.locator('.mat-column-rr').count()).toBe(isHandset ? 0 : 1);
    const beforeGridEvidence = await Promise.all(
      evidenceColumns.map((selector) => qrk.locator(selector).innerText()),
    );

    await qrk.click();
    const panel = page.getByTestId('details-panel');
    const marketR = panel.getByTestId('setup-risk-reward');
    const beforeMarketR = await marketR.getAttribute('aria-label');
    await panel.getByRole('tab', { name: 'Scores' }).click();
    await expect(panel.locator('.score-block').first()).toBeVisible();
    const beforeScores = await panel.locator('.score-block').allInnerTexts();

    const returnToOverview = async () => {
      if (isHandset) await panel.locator('.mat-mdc-tab-header-pagination-before').click();
      await panel.getByRole('tab', { name: 'Overview' }).click();
    };
    await returnToOverview();
    for (const label of ['Entry (CAD)', 'Stop (CAD)', 'Target (CAD)']) {
      await expect(panel.getByText(label, { exact: true })).toBeVisible();
    }
    await page.getByTestId('manual-r-entry').fill('50');
    await page.getByTestId('manual-r-stop').fill('45');
    await page.getByTestId('manual-r-target').fill('60');
    await expect(page.getByTestId('manual-r-result')).toContainText('2.00 : 1');

    await panel.getByRole('tab', { name: 'Scores' }).click();
    await expect(panel.locator('.score-block').first()).toBeVisible();
    expect(await panel.locator('.score-block').allInnerTexts()).toEqual(beforeScores);
    await returnToOverview();
    expect(await marketR.getAttribute('aria-label')).toBe(beforeMarketR);
    const afterGridEvidence = await Promise.all(
      evidenceColumns.map((selector) => rowFor(page, 'QRK.TO').locator(selector).innerText()),
    );
    expect(afterGridEvidence).toEqual(beforeGridEvidence);
    await page.getByTestId('details-close').click();
  });

  test('keeps score filters independent between Swing and Strategic profiles', async ({ page }) => {
    await selectFilterOption(page, 'score-filter', 'All (incl. unavailable)');
    await page.getByTestId('perspective-strategic').click();
    await expect(page.getByTestId('score-filter')).toContainText(/Attractive/);
    await page.getByTestId('perspective-swing').click();
    await expect(page.getByTestId('score-filter')).toContainText('All (incl. unavailable)');
  });

  test('synthetic opportunity market sessions never fall on weekends', () => {
    const snapshot = opportunitySnapshotResponse();
    const dates = [
      snapshot.dataAsOfSession,
      ...snapshot.rows.flatMap((row) => [
        row.freshness.dataAsOfDate,
        row.freshness.expectedSession,
      ]),
    ].filter((date): date is string => !!date);
    const detail = opportunityDetailResponse(snapshot.rows[0].symbol, snapshot.snapshotId);
    if ('status' in detail) throw new Error('Expected a synthetic opportunity detail fixture.');
    if (detail.sectorRelative) {
      dates.push(detail.sectorRelative.expectedSession);
      dates.push(detail.sectorRelative.d20.startSession);
      dates.push(detail.sectorRelative.d20.endSession);
    }
    for (const date of dates) {
      expect([0, 6]).not.toContain(new Date(`${date}T00:00:00Z`).getUTCDay());
    }
  });

  test('refresh runs a new read-only snapshot', async ({ page, mocks, errors }) => {
    await page.getByTestId('refresh-analysis').click();
    await expect
      .poll(() => mocks.requests.filter((r) => /POST \/api\/opportunities\/refresh/.test(r)).length)
      .toBe(1);
    await expect(page.getByTestId('refresh-analysis')).toBeEnabled();
    expect(
      mocks.requests.some((r) =>
        /^(POST|PUT|DELETE) \/api\/(cash|portfolio|transactions|risk|signals)/.test(r),
      ),
    ).toBe(false);
    expect(errors).toEqual([]);
  });

  // Opt-in UAT evidence: PM029_SCREENSHOTS=<dir> npx playwright test trading-opportunities
  test('final functional changes preserve inactive quality, names, sorting and fund context', async ({
    page,
    mocks,
    errors,
  }, testInfo) => {
    const directory = process.env['PM029_SCREENSHOTS'];
    const capture = async (label: string) => {
      await settle(page);
      await expectNoHorizontalPageOverflow(page);
      if (directory) {
        if ((await page.locator('[data-pm029-synthetic-watermark]').count()) === 0)
          await addSyntheticScreenshotLabel(page);
        await settle(page);
        await page.screenshot({
          path: join(directory, `SYNTHETIC-${testInfo.project.name}-${label}.png`),
          fullPage: true,
        });
      }
    };
    await expect(rowFor(page, 'QRK.TO').getByTestId('company-name')).toBeVisible();
    await expect(rowFor(page, 'QRK.TO').getByTestId('setup-quality')).toContainText('Unavailable');
    await capture('01-inactive-swing');
    await rowFor(page, 'QRK.TO').click();
    await expect(page.getByTestId('setup-quality-details')).toContainText('historical snapshot');
    await capture('02-inactive-details');
    await page.keyboard.press('Escape');

    const snapshot = opportunitySnapshotResponse();
    snapshot.rows[0].setupQuality = setupQualityFixture({
      currency: snapshot.rows[0].currency,
      entry: snapshot.rows[0].setup.entry,
      proposedStop: snapshot.rows[0].setup.stop,
      support: snapshot.rows[0].setup.stop,
      proposedTarget: snapshot.rows[0].setup.target,
      resistance: snapshot.rows[0].setup.target,
      credibleTarget: snapshot.rows[0].setup.target,
      marketRewardRisk: snapshot.rows[0].setup.rewardRisk,
      conservativeRewardRisk: snapshot.rows[0].setup.rewardRisk,
    });
    for (const [symbol, status, ratio] of [
      ['PLW.TO', 'Caution', 1.7],
      ['ZEN.NE', 'Poor', 1.2],
    ] as const) {
      const row = snapshot.rows.find((candidate) => candidate.symbol === symbol)!;
      const entry = row.setup.entry!;
      const stop = row.setup.stop!;
      const resistance = entry + (entry - stop) * ratio;
      row.setupQuality = setupQualityFixture({
        status,
        entry,
        proposedStop: stop,
        support: stop,
        proposedTarget: row.setup.target,
        resistance,
        credibleTarget: resistance,
        marketRewardRisk: row.setup.rewardRisk,
        conservativeRewardRisk: ratio,
      });
    }
    const fund = snapshot.rows.find((row) => row.symbol === 'BRQ.TO')!;
    const originalAction = structuredClone(fund.finalAction);
    fund.companyName = 'Synthetic Fund';
    fund.instrumentType = 'ETF';
    fund.swing = {
      ...fund.swing,
      score: null,
      status: 'NotEvaluated',
      band: 'None',
      reasonCodes: ['EtfScoringDeferred'],
    };
    fund.strategic = {
      ...fund.strategic,
      score: null,
      status: 'NotEvaluated',
      band: 'None',
      reasonCodes: ['EtfScoringDeferred'],
    };
    const detail = opportunityDetailResponse('QRK.TO', snapshot.snapshotId);
    if ('status' in detail) throw new Error('Expected synthetic detail.');
    detail.row = snapshot.rows[0];
    await page.route('**/api/opportunities', (route) => route.fulfill({ json: snapshot }));
    await page.route('**/api/opportunities/QRK.TO?*', (route) => route.fulfill({ json: detail }));
    await page.reload();
    await expect(rowFor(page, 'QRK.TO').getByTestId('setup-quality')).toContainText('Good');
    await expect(rowFor(page, 'PLW.TO').getByTestId('setup-quality')).toContainText('Caution');
    await expect(rowFor(page, 'ZEN.NE').getByTestId('setup-quality')).toContainText('Poor');
    await capture('03a-proposed-qualities-grid');
    await rowFor(page, 'QRK.TO').click();
    await expect(page.getByTestId('setup-quality-details')).toContainText('Confirmed support');
    await capture('03-proposed-policy-simulation');
    await page.getByTestId('setup-quality-details').locator('p').last().scrollIntoViewIfNeeded();
    await capture('03b-setup-evidence-bottom');
    await page.keyboard.press('Escape');
    await page.getByTestId('score-filter').click();
    await page.getByRole('option', { name: 'All (incl. unavailable)', exact: true }).click();
    await expect(rowFor(page, 'BRQ.TO').getByTestId('score-cell').first()).toContainText(
      'Not evaluated',
    );
    await expect(page.getByTestId('reason-EtfScoringDeferred')).toContainText(
      'ETF scoring deferred',
    );
    expect(fund.finalAction).toEqual(originalAction);
    const order = await dataRows(page).evaluateAll((rows) =>
      rows.map((row) => row.getAttribute('data-symbol')),
    );
    expect(order[0]).toBe('QRK.TO');
    expect(order.at(-1)).toBe('BRQ.TO');
    await capture('04-fund-deferral');
    await page.getByTestId('perspective-strategic').click();
    await expect(page.getByTestId('setup-quality')).toHaveCount(0);
    const topStrategic = snapshot.rows.reduce((best, row) =>
      (row.strategic.score ?? -1) > (best.strategic.score ?? -1) ? row : best,
    );
    await expect(dataRows(page).first()).toHaveAttribute('data-symbol', topStrategic.symbol);
    await capture('05-strategic-default');
    await expectNoHorizontalPageOverflow(page);
    expect(mocks.unmocked).toEqual([]);
    expect(
      mocks.requests.some((request) => /^POST \/api\/opportunities\/refresh/.test(request)),
    ).toBe(false);
    expect(errors).toEqual([]);
  });

  test('captures UAT screenshots', async ({ page }, testInfo) => {
    const dir = process.env['PM029_SCREENSHOTS'];
    test.skip(!dir, 'set PM029_SCREENSHOTS to capture');
    test.setTimeout(90_000);
    const name = testInfo.project.name;
    const fullPage = !name.startsWith('mobile-');
    const capture = async (label: string, options: { fullPage?: boolean } = {}) => {
      await page.screenshot({ path: `${dir}/SYNTHETIC-${name}-${label}.png`, ...options });
    };
    await addSyntheticScreenshotLabel(page);
    await settle(page);
    await capture('01-heatmap', { fullPage: true });
    await rowFor(page, 'QRK.TO').click();
    await expect(page.getByTestId('opportunity-details')).toBeVisible();
    await settle(page);
    await capture('02-overview', { fullPage });
    if (name.startsWith('mobile-')) {
      await page.getByTestId('setup-risk-reward').scrollIntoViewIfNeeded();
      await settle(page);
      await capture('02a-overview-levels');
    }
    await page.getByTestId('manual-r-entry').fill('50');
    await page.getByTestId('manual-r-stop').fill('45');
    await page.getByTestId('manual-r-target').fill('60');
    await expect(page.getByTestId('manual-r-result')).toContainText('2.00 : 1');
    await page.getByTestId('manual-r').scrollIntoViewIfNeeded();
    await settle(page);
    await capture('02b-manual-r', { fullPage });
    await page.getByTestId('manual-r-stop').fill('50');
    await expect(page.getByTestId('manual-r-result')).toContainText('—');
    await expect(page.getByTestId('manual-r-result')).toContainText(
      'use positive prices with stop below entry',
    );
    await settle(page);
    await capture('02g-manual-r-invalid', { fullPage });
    await page.getByTestId('manual-r-stop').fill('45');
    await expect(page.getByTestId('manual-r-result')).toContainText('2.00 : 1');
    for (const [index, tab] of ['Technicals', 'Scores', 'Evidence'].entries()) {
      await page.getByRole('tab', { name: tab }).click();
      await expect(page.getByRole('tab', { name: tab })).toHaveAttribute('aria-selected', 'true');
      await settle(page);
      await capture(`02${String.fromCharCode(99 + index)}-${tab.toLowerCase()}`, { fullPage });
    }
    await page.keyboard.press('Escape');
    await expect(page.getByTestId('opportunity-details')).toHaveCount(0);
    await rowFor(page, 'PLW.TO').click();
    await expect(page.getByTestId('details-decisions')).toBeVisible();
    await settle(page);
    await capture('02f-details-multiple', { fullPage });
    await page.getByTestId('details-close').click();
    await page
      .getByRole('button', { name: /^Columns \(\d+\)/ })
      .first()
      .click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await settle(page);
    await capture('03-columns');
    await page.getByRole('dialog').getByRole('button', { name: 'Close', exact: true }).click();
    await expect(page.getByRole('dialog')).toHaveCount(0);
    await page.getByTestId('perspective-strategic').click();
    await expect(page.getByTestId('perspective-strategic')).toHaveClass(
      /mat-button-toggle-checked/,
    );
    await settle(page);
    await capture('04-strategic-defaults', { fullPage: true });

    await page.getByTestId('perspective-strategic').click();
    await page.getByTestId('score-filter').click();
    await page.getByRole('option', { name: 'All (incl. unavailable)', exact: true }).click();
    await settle(page);
    await capture('05-strategic-grid', { fullPage: true });
    await page
      .getByRole('button', { name: /^Columns \(\d+\)/ })
      .first()
      .click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await settle(page);
    await capture('05a-strategic-columns');
    await page.keyboard.press('Escape');
    await expect(page.getByRole('dialog')).toHaveCount(0);

    await page.getByTestId('perspective-swing').click();
    await page.getByTestId('score-filter').click();
    await page.getByRole('option', { name: 'All (incl. unavailable)', exact: true }).click();
    await rowFor(page, 'QRK.TO').click();
    await expect(page.getByTestId('details-panel')).toBeVisible();
    await page.getByTestId('manual-r-entry').fill('50');
    await page.getByTestId('manual-r-stop').fill('45');
    await page.getByTestId('manual-r-target').fill('60');
    await expect(page.getByTestId('manual-r-result')).toContainText('2.00 : 1');
    await page.getByTestId('details-close').click();
    await expect(page.getByTestId('details-panel')).toHaveCount(0);
    await expect(rowFor(page, 'QRK.TO')).toContainText('2.00 : 1');
    await settle(page);
    await capture('06-manual-r-grid', { fullPage: true });

    await page.getByTestId('score-filter').click();
    await page.getByRole('option', { name: 'All (incl. unavailable)', exact: true }).click();
    await rowFor(page, 'CCA.TO').click();
    await expect(page.getByTestId('details-panel')).toBeVisible();
    await settle(page);
    await capture('07-cca-overview', { fullPage });
    await page.getByRole('tab', { name: 'Technicals' }).click();
    await settle(page);
    await capture('07a-cca-technicals', { fullPage });
    await page.getByRole('tab', { name: 'Scores' }).click();
    await settle(page);
    await capture('07b-cca-swing-no-setup', { fullPage });
    await page.getByRole('tab', { name: 'Evidence' }).click();
    await settle(page);
    await capture('07c-cca-evidence', { fullPage });
  });
});
