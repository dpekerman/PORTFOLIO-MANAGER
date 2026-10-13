import type { Page } from '@playwright/test';
import { expect, test } from './support/test';
import { expectNoHorizontalPageOverflow, settle } from './support/overflow';

/**
 * Dashboard on phones and the tablet: every section expanded (market leadership starts collapsed),
 * then the page must not overflow and no dashboard card may scroll sideways internally. Desktop is
 * deliberately not asserted here: its layout is covered by the generic smoke/overflow specs.
 */
const SECTIONS = [
  'portfolio-value-history',
  'top-movers',
  'market-indices',
  'allocation',
  'market-leadership',
  'market-signals',
  'upcoming-earnings',
  'action-center',
  'signal-changes',
  'priority-candidates',
  'ytd-performance',
  'latest-eod-signals',
];

async function openExpandedDashboard(page: Page): Promise<void> {
  await page.addInitScript((ids) => {
    for (const id of ids) localStorage.setItem(`dashboard_collapse_${id}`, 'false');
  }, SECTIONS);
  await page.goto('/dashboard');
  await expect(page.getByRole('heading', { level: 1, name: 'Dashboard' })).toBeVisible();
  await expect(page.getByText('Market signals', { exact: true })).toBeVisible();
  await settle(page);
}

test.describe('dashboard on small screens', () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name === 'desktop-chrome', 'handset and tablet layouts only');
  });

  test('has no horizontal page overflow with every section expanded', async ({ page, errors }) => {
    await openExpandedDashboard(page);
    await expectNoHorizontalPageOverflow(page);
    expect(errors).toEqual([]);
  });

  test('no dashboard card scrolls sideways internally', async ({ page }) => {
    await openExpandedDashboard(page);
    // Platform fonts differ (the Linux CI runner renders text wider than Windows), so widen text a
    // little: a layout that only fits with one platform's metrics fails here on every OS.
    await page.addStyleTag({ content: '.db-page * { letter-spacing: 0.04em !important; }' });
    const offenders = await page.evaluate(() => {
      const found: string[] = [];
      const describe = (el: Element) => {
        const own =
          el.tagName.toLowerCase() +
          '.' +
          (el.getAttribute('class') ?? '').trim().split(/\s+/).slice(0, 2).join('.');
        // Name the offender by its widget/heading so a CI failure is actionable without a local repro.
        const host = el.closest('app-portfolio-actions-widget, app-market-leadership-widget, app-priority-candidates-widget, app-performance-summary-widget, section, mat-card');
        const title = el.querySelector('h1, h2, h3, h4, .db-panel-title')?.textContent?.trim().slice(0, 40);
        return `${own}${host && host !== el ? ` in ${host.tagName.toLowerCase()}` : ''}${title ? ` "${title}"` : ''}`;
      };
      const roots = document.querySelectorAll(
        '.db-page, app-portfolio-actions-widget, app-market-leadership-widget, ' +
          'app-priority-candidates-widget, app-performance-summary-widget',
      );
      const seen = new Set<Element>();
      for (const root of Array.from(roots)) {
        for (const el of [root, ...Array.from(root.querySelectorAll('*'))]) {
          if (seen.has(el)) continue;
          seen.add(el);
          const h = el as HTMLElement;
          if (h.clientWidth === 0) continue;
          const style = getComputedStyle(h);
          const scrolls = style.overflowX === 'auto' || style.overflowX === 'scroll';
          const isPanel = h.matches('mat-card, .db-panel, .db-chart-card, table');
          if ((scrolls || isPanel) && h.scrollWidth > h.clientWidth + 1) {
            found.push(`${describe(h)} (${h.scrollWidth} > ${h.clientWidth})`);
          }
        }
      }
      return found;
    });
    expect(offenders, `Cards scrolling sideways:\n  ${offenders.join('\n  ')}`).toEqual([]);
  });

  test('dense sections stay readable and inside the viewport', async ({ page }) => {
    await openExpandedDashboard(page);
    const viewport = page.viewportSize()!.width;

    const signalCard = page.locator('.db-sig-table tbody tr').first();
    const actionCard = page.locator('app-portfolio-actions-widget .pa-table tbody tr').first();
    const eodCard = page.locator('.db-eod-table tbody tr').first();
    const trackerCard = page.locator('app-market-leadership-widget .ml-row').first();

    for (const card of [signalCard, actionCard, eodCard, trackerCard]) {
      await card.scrollIntoViewIfNeeded();
      await expect(card).toBeVisible();
      const box = (await card.boundingBox())!;
      expect(box.x).toBeGreaterThanOrEqual(0);
      expect(box.x + box.width).toBeLessThanOrEqual(viewport + 1);
    }

    await expect(trackerCard).toContainText('S&P 500 ETF');
    await expect(actionCard).toContainText('ENB.TO');
    await expect(eodCard).toContainText('RY.TO');
  });

  test('handset renders the dense tables as labelled cards', async ({ page }, testInfo) => {
    test.skip(testInfo.project.name === 'tablet', 'handset-only table layout');
    await openExpandedDashboard(page);
    for (const selector of [
      '.db-sig-table tbody tr',
      '.db-eod-table tbody tr',
      'app-portfolio-actions-widget .pa-table tbody tr',
    ]) {
      const row = page.locator(selector).first();
      await expect(row).toHaveCSS('display', 'grid');
    }
    await expect(page.locator('.db-sig-table thead').first()).toBeHidden();
  });
});
