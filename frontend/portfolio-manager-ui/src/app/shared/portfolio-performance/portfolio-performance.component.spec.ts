import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { PeriodPerformance } from '../../core/models/portfolio.models';
import { DashboardStore } from '../../core/store/dashboard.store';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { PortfolioPerformanceComponent } from './portfolio-performance.component';

const base: PeriodPerformance = {
  period: 'Day',
  status: 'Available',
  reason: null,
  baselineDate: '2026-09-01',
  valueChange: 10_000,
  netExternalFlow: 10_000,
  investmentGainLoss: 0,
  investmentGainLossPercent: null,
};

function render(performance: PeriodPerformance[] | undefined): HTMLElement {
  TestBed.configureTestingModule({
    providers: [
      { provide: DashboardStore, useValue: { data: signal({ summary: { performance } }) } },
      { provide: DemoModeStore, useValue: { maskValue: (v: number) => v, maskPercent: (v: number) => v } },
    ],
  });
  const fixture = TestBed.createComponent(PortfolioPerformanceComponent);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('PortfolioPerformanceComponent', () => {
  it('renders nothing when the snapshot has no performance block', () => {
    expect(render(undefined).querySelector('[data-testid="portfolio-performance"]')).toBeNull();
  });

  it('shows a $10,000 deposit as value change and flow with zero gain and N/A percent', () => {
    const el = render([base]);
    expect(el.textContent).toContain('$10,000');
    expect(el.querySelector('[data-testid="pp-gain"]')?.textContent?.trim()).toBe('$0');
    expect(el.querySelector('[data-testid="pp-pct"]')?.textContent?.trim()).toBe('N/A');
  });

  it('shows the simple percent when no external flow occurred', () => {
    const el = render([{ ...base, netExternalFlow: 0, valueChange: 500, investmentGainLoss: 500, investmentGainLossPercent: 0.5 }]);
    expect(el.querySelector('[data-testid="pp-pct"]')?.textContent?.trim()).toBe('0.50%');
  });

  it('shows Updating and Unavailable states instead of numbers', () => {
    const el = render([
      { ...base, period: 'Month', status: 'Unavailable', reason: 'No baseline', valueChange: null, netExternalFlow: null, investmentGainLoss: null },
      { ...base, period: 'YTD', status: 'Updating', reason: 'pending', valueChange: null, netExternalFlow: null, investmentGainLoss: null },
    ]);
    const states = Array.from(el.querySelectorAll('[data-testid="pp-state"]')).map((s) => s.textContent?.trim());
    expect(states).toEqual(['Unavailable', 'Updating…']);
    expect(el.querySelector('[data-testid="pp-gain"]')).toBeNull();
  });
});
