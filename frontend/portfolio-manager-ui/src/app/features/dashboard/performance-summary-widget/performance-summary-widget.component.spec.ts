import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { PeriodPerformance, PerformanceSummaryResponse } from '../../../core/models/portfolio.models';
import { DashboardStore } from '../../../core/store/dashboard.store';
import { DemoModeStore } from '../../../core/store/demo-mode.store';
import { PerformanceSummaryWidgetComponent } from './performance-summary-widget.component';

const ytdUnavailable: PeriodPerformance = {
  period: 'YTD',
  status: 'Unavailable',
  reason: 'No baseline snapshot before the period start.',
  baselineDate: null,
  valueChange: null,
  netExternalFlow: null,
  investmentGainLoss: null,
  investmentGainLossPercent: null,
};

// Live case: 776,716 -> 814,113 with 40,000 of deposits => -2,603, never the raw +37,397.
const sinceStart: PeriodPerformance = {
  period: 'SinceStart',
  status: 'Available',
  reason: null,
  baselineDate: '2026-09-05',
  valueChange: 37_397,
  netExternalFlow: 40_000,
  investmentGainLoss: -2_603,
  investmentGainLossPercent: -0.35,
  baselineValue: 776_716,
  returnMethod: 'Time-weighted (daily snapshots, flows at end of day)',
};

function render(performance: PeriodPerformance[], summary: PerformanceSummaryResponse | null): HTMLElement {
  TestBed.configureTestingModule({
    providers: [
      {
        provide: DashboardStore,
        useValue: {
          data: signal({ summary: { totalValue: 814_113, performance } }),
          performanceSummary: signal(summary),
          performanceSummaryLoading: signal(false),
        },
      },
      { provide: DemoModeStore, useValue: { maskValue: (v: number) => v, maskPercent: (v: number) => v } },
    ],
  });
  const fixture = TestBed.createComponent(PerformanceSummaryWidgetComponent);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

const text = (el: HTMLElement, id: string) => el.querySelector(`[data-testid="${id}"]`)?.textContent?.replace(/\s+/g, ' ').trim();

describe('PerformanceSummaryWidgetComponent', () => {
  it('reports cash-adjusted gain with a labelled shorter period when YTD has no baseline', () => {
    const el = render([ytdUnavailable, sinceStart], {
      benchmarkStartDate: '2026-09-05',
      isFullYear: false,
      benchmarks: [{ name: 'S&P 500', symbol: '^GSPC', ytdReturnPct: 4.1 }],
    });

    expect(text(el, 'ps-return-label')).toContain('Return since');
    expect(text(el, 'ps-return-label')).toContain('2026');
    expect(text(el, 'ps-return-pct')).toBe('-0.35%');
    expect(text(el, 'ps-gain')).toBe('-$2,603');
    expect(text(el, 'ps-flow')).toBe('$40,000');
    expect(el.textContent).not.toContain('37,397');
    expect(text(el, 'ps-ytd-note')).toContain('Year to date is unavailable');
    expect(el.textContent).toContain('S&P 500 since');
  });

  it('hides benchmarks whose start date differs from the portfolio baseline', () => {
    const el = render([ytdUnavailable, sinceStart], {
      benchmarkStartDate: '2025-12-31',
      isFullYear: true,
      benchmarks: [{ name: 'S&P 500', symbol: '^GSPC', ytdReturnPct: 12 }],
    });

    expect(el.textContent).not.toContain('S&P 500');
  });

  it('labels a true YTD period and shows N/A instead of a made-up percent', () => {
    const ytd: PeriodPerformance = { ...sinceStart, period: 'YTD', baselineDate: '2025-12-31', investmentGainLossPercent: null };
    const el = render([ytd], { benchmarkStartDate: '2025-12-31', isFullYear: true, benchmarks: [] });

    expect(text(el, 'ps-return-label')).toBe('Portfolio YTD');
    expect(text(el, 'ps-return-pct')).toBe('N/A');
    expect(el.querySelector('[data-testid="ps-ytd-note"]')).toBeNull();
  });

  it('shows an honest unavailable state when no baseline exists at all', () => {
    const el = render([ytdUnavailable, { ...ytdUnavailable, period: 'SinceStart', reason: 'No snapshot exists on or after the cash ledger start.' }], null);

    expect(text(el, 'ps-unavailable')).toContain('Performance unavailable');
    expect(el.querySelector('[data-testid="ps-stats"]')).toBeNull();
  });
});
