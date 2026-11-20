import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { DashboardStore } from '../../core/store/dashboard.store';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { PeriodPerformance, PerformancePeriod } from '../../core/models/portfolio.models';

const PERIOD_LABELS: Record<PerformancePeriod, string> = {
  Day: 'Today',
  Week: 'This week',
  Month: 'This month',
  YTD: 'Year to date',
  SinceStart: 'Since ledger start',
};

/** Portfolio-level Value Change, Net External Flow and Investment Gain/Loss, shared by Dashboard and Portfolio. */
@Component({
  selector: 'app-portfolio-performance',
  templateUrl: './portfolio-performance.component.html',
  styleUrl: './portfolio-performance.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CurrencyPipe, DecimalPipe],
})
export class PortfolioPerformanceComponent {
  private readonly dashboard = inject(DashboardStore);
  private readonly demoMode = inject(DemoModeStore);

  // SinceStart feeds the Dashboard's upper YTD card only; this grid shows the four standard periods.
  protected readonly periods = computed(() =>
    (this.dashboard.data()?.summary.performance ?? []).filter((p) => p.period !== 'SinceStart'),
  );

  protected label(p: PeriodPerformance): string {
    return PERIOD_LABELS[p.period];
  }
  protected value(v: number): number {
    return this.demoMode.maskValue(v);
  }
  protected percent(v: number): number {
    return this.demoMode.maskPercent(v);
  }
  protected signCls(v: number): string {
    return v >= 0 ? 'positive' : 'negative';
  }
}
