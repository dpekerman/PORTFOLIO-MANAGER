import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DashboardStateService } from '../../../core/services/dashboard-state.service';
import { DemoModeService } from '../../../core/services/demo-mode.service';

@Component({
  selector: 'app-performance-summary-widget',
  templateUrl: './performance-summary-widget.component.html',
  styleUrl: './performance-summary-widget.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CurrencyPipe, DecimalPipe, MatIconModule, MatProgressBarModule, MatTooltipModule],
})
export class PerformanceSummaryWidgetComponent {
  protected readonly dashboard = inject(DashboardStateService);
  private readonly demoMode = inject(DemoModeService);
  protected readonly summary = this.dashboard.performanceSummary;
  protected readonly loading = this.dashboard.performanceSummaryLoading;

  /** Live portfolio total from the dashboard snapshot (matches the hero value). */
  protected readonly liveCurrentValue = computed(
    () => this.dashboard.data()?.summary.totalValue ?? 0,
  );

  /** YTD dollar change recomputed from live value so it matches the displayed current figure. */
  protected readonly ytdDollar = computed(() => {
    const s = this.summary();
    if (!s) return 0;
    const cur = this.liveCurrentValue() || s.portfolioCurrentValue;
    return cur - s.portfolioStartValue;
  });

  /** YTD % recomputed from live value. */
  protected readonly ytdPct = computed(() => {
    const s = this.summary();
    if (!s || s.portfolioStartValue === 0) return 0;
    return (this.ytdDollar() / s.portfolioStartValue) * 100;
  });

  protected readonly todayLabel = new Date().toLocaleDateString('en-CA', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });

  protected signCls(v: number): string {
    return v >= 0 ? 'pos' : 'neg';
  }
  protected sign(v: number): string {
    return v >= 0 ? '+' : '';
  }
  protected maskVal(v: number): number {
    return this.demoMode.maskValue(v);
  }
  protected fmtDate(dateStr: string): string {
    if (!dateStr) return '—';
    const d = new Date(dateStr + 'T00:00:00');
    return d.toLocaleDateString('en-CA', { year: 'numeric', month: 'short', day: 'numeric' });
  }
}
