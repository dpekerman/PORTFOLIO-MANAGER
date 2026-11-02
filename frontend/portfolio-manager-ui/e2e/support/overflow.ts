import { expect, type Page } from '@playwright/test';

/** Sub-pixel rounding and scrollbars make an exact comparison flaky; 1px is below visual notice. */
const TOLERANCE_PX = 1;

export interface OverflowReport {
  viewport: number;
  /** Widest scrollable content width among the page's scroll roots. */
  scrollWidth: number;
  /** Which scroll root produced `scrollWidth` (`html` or the layout's `mat-sidenav-content`). */
  root: string;
  /** Elements reaching furthest past the viewport, widest first (max 5). */
  offenders: { desc: string; width: number; right: number }[];
}

/**
 * Measures horizontal overflow of the page.
 *
 * WHY not just `documentElement.scrollWidth`: the app shell puts all content inside
 * `mat-sidenav-content` (`overflow: auto`) under a hidden-overflow container, so the document
 * itself never scrolls sideways — that check would pass on every page even when the user has to
 * pan horizontally. We therefore measure each scroll root (the document and the sidenav content)
 * and report the widest.
 *
 * Elements inside their own horizontally scrollable wrapper (e.g. a table in `overflow-x: auto`)
 * are not counted as offenders: they scroll in place and do not widen the page.
 */
export async function measureOverflow(page: Page): Promise<OverflowReport> {
  return page.evaluate((tolerance) => {
    const viewport = window.innerWidth;
    const content = document.querySelector('mat-sidenav-content');
    const roots: { name: string; el: Element }[] = [
      { name: 'html', el: document.documentElement },
      ...(content ? [{ name: 'mat-sidenav-content', el: content }] : []),
    ];
    const widest = roots.reduce((a, b) => (b.el.scrollWidth > a.el.scrollWidth ? b : a));
    const offenders: OverflowReport['offenders'] = [];

    if (widest.el.scrollWidth > viewport + tolerance) {
      const scrollsInPlace = (el: Element): boolean => {
        for (let p = el.parentElement; p && p !== widest.el; p = p.parentElement) {
          const ox = getComputedStyle(p).overflowX;
          if (ox === 'auto' || ox === 'scroll' || ox === 'hidden' || ox === 'clip') return true;
        }
        return false;
      };
      const describe = (el: Element): string => {
        const cls = (el.getAttribute('class') ?? '')
          .trim()
          .split(/\s+/)
          .filter(Boolean)
          .slice(0, 3);
        return el.tagName.toLowerCase() + (cls.length ? '.' + cls.join('.') : '');
      };
      for (const el of Array.from(widest.el.querySelectorAll('*'))) {
        const rect = el.getBoundingClientRect();
        if (rect.width === 0 || rect.right <= viewport + tolerance) continue;
        if (scrollsInPlace(el)) continue;
        offenders.push({
          desc: describe(el),
          width: Math.round(rect.width),
          right: Math.round(rect.right),
        });
      }
      offenders.sort((a, b) => b.right - a.right);
    }
    return {
      viewport,
      scrollWidth: widest.el.scrollWidth,
      root: widest.name,
      offenders: offenders.slice(0, 5),
    };
  }, TOLERANCE_PX);
}

/**
 * Waits for the page to settle (network idle, web fonts, a short render window for signal-driven
 * views) then asserts there is no horizontal page overflow. The failure message lists the widest
 * offending elements so each failing route is an actionable backlog item.
 */
export async function expectNoHorizontalPageOverflow(page: Page): Promise<void> {
  await settle(page);
  const report = await measureOverflow(page);
  const detail = report.offenders
    .map((o) => `    ${o.desc} (width ${o.width}px, right edge ${o.right}px)`)
    .join('\n');
  expect(
    report.scrollWidth,
    `Horizontal overflow: ${report.root} content is ${report.scrollWidth}px wide in a ` +
      `${report.viewport}px viewport.\n  Widest offenders:\n` +
      (detail || '    (none outside self-scrolling containers)'),
  ).toBeLessThanOrEqual(report.viewport + TOLERANCE_PX);
}

/** Network idle + fonts ready + short settle; fonts change text metrics so measuring earlier is flaky. */
export async function settle(page: Page): Promise<void> {
  await page.waitForLoadState('networkidle');
  await page.evaluate(() => document.fonts.ready.then(() => undefined));
  await page.waitForTimeout(500);
}
