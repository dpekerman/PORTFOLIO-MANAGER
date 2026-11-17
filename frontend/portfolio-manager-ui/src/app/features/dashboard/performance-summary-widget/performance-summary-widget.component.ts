import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PeriodPerformance } from '../../../core/models/portfolio.models';
import { DashboardStore } from '../../../core/store/dashboard.store';
import { DemoModeStore } from '../../../core/store/demo-mode.store';

/**
 * Upper dashboard performance card. It shows the SAME cash-flow-adjusted figures as the lower "Performance (cash in/out
 * adjusted)" section: the portfolio numbers come from `summary.performance` (YTD, else the explicitly labelled shorter
 * "since cash-ledger start" period). Nothing here subtracts start from current value itself. Benchmarks are shown only
 * when their start date equals the portfolio baseline date.
 */
@Component({
  selector: 'app-performance-summary-widget',
  templateUrl: './performance-summary-widget.component.html',
  styleUrl: './performance-summary-widget.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CurrencyPipe, DecimalPipe, MatIconModule, MatProgressBarModule, MatTooltipModule],
})
export class PerformanceSummaryWidgetComponent {
  protected readonly dashboard = inject(DashboardStore);
  private readonly demoMode = inject(DemoModeStore);
  protected readonly summary = this.dashboard.performanceSummary;
  protected readonly loading = this.dashboard.performanceSummaryLoading;

  /** Live portfolio total from the dashboard snapshot (the same end value the performance periods use). */
  protected readonly liveCurrentValue = computed(() => this.dashboard.data()?.summary.totalValue ?? 0);

  private readonly periods = computed(() => this.dashboard.data()?.summary.performance ?? []);
  protected readonly ytd = computed(() => this.periods().find((p) => p.period === 'YTD') ?? null);
  private readonly since = computed(() => this.periods().find((p) => p.period === 'SinceStart') ?? null);

  /** True year-to-date when available; otherwise the shorter period (or its Unavailable/Updating state). */
  protected readonly active = computed<PeriodPerformance | null>(() => {
    const ytd = this.ytd();
    return ytd?.status === 'Available' ? ytd : (this.since() ?? ytd);
  });
  protected readonly isYtd = computed(() => this.active()?.period === 'YTD');

  protected readonly ytdNote = computed(() => {
    const ytd = this.ytd();
    if (!ytd || ytd.status === 'Available') return null;
    return `Year to date is unavailable${ytd.reason ? `: ${ytd.reason}` : ''}`;
  });

  protected readonly benchmarks = computed(() => {
    const s = this.summary();
    const a = this.active();
    return s && a?.status === 'Available' && s.benchmarkStartDate === a.baselineDate ? s.benchmarks : [];
  });

  protected readonly todayLabel = new Date().toLocaleDateString('en-CA', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });

  protected readonly returnLabel = computed(() =>
    this.isYtd() ? 'Portfolio YTD' : `Return since ${this.fmtDate(this.active()?.baselineDate ?? '')}`,
  );
  protected readonly dollarLabel = computed(() =>
    this.isYtd() ? 'Investment gain/loss YTD' : 'Investment gain/loss',
  );
  protected readonly benchmarkSuffix = computed(() =>
    this.isYtd() ? 'YTD' : `since ${this.fmtDate(this.active()?.baselineDate ?? '')}`,
  );
  protected readonly startLabel = computed(() => (this.isYtd() ? 'Start of year' : 'Baseline'));

  protected signCls(v: number): string {
    return v >= 0 ? 'pos' : 'neg';
  }
  protected sign(v: number): string {
    return v >= 0 ? '+' : '';
  }
  protected maskVal(v: number): number {
    return this.demoMode.maskValue(v);
  }
  protected maskPct(v: number): number {
    return this.demoMode.maskPercent(v);
  }
  protected fmtDate(dateStr: string): string {
    if (!dateStr) return '—';
    const d = new Date(dateStr + 'T00:00:00');
    return d.toLocaleDateString('en-CA', { year: 'numeric', month: 'short', day: 'numeric' });
  }
}
