import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { DashboardAllocation } from '../../core/models/portfolio.models';
import { AppRefreshService } from '../../core/services/app-refresh.service';
import { DashboardStateService } from '../../core/services/dashboard-state.service';
import { DemoModeService } from '../../core/services/demo-mode.service';
import { MarketLeadershipWidgetComponent } from './market-leadership-widget/market-leadership-widget.component';
import { PerformanceSummaryWidgetComponent } from './performance-summary-widget/performance-summary-widget.component';
import { PortfolioActionsWidgetComponent } from './portfolio-actions-widget/portfolio-actions-widget.component';
import { PriorityCandidatesWidgetComponent } from './priority-candidates-widget/priority-candidates-widget.component';
import { StateChangesWidgetComponent } from './state-changes-widget/state-changes-widget.component';

export type ChartRange = '1M' | '3M' | '6M' | 'YTD' | '1Y' | 'ALL';

export interface SvgChart {
  linePath: string;
  areaPath: string;
  isUp: boolean;
  baselineY: number;
  xLabels: { x: number; label: string }[];
  yLabels: { y: number; label: string; gridY: number }[];
  viewBox: string;
  padL: number;
}

@Component({
  selector: 'app-dashboard-page',
  templateUrl: './dashboard-page.component.html',
  styleUrl: './dashboard-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CurrencyPipe,
    DatePipe,
    DecimalPipe,
    MatButtonModule,
    MatButtonToggleModule,
    MatCardModule,
    MatIconModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTooltipModule,
    RouterLink,
    PortfolioActionsWidgetComponent,
    StateChangesWidgetComponent,
    MarketLeadershipWidgetComponent,
    PriorityCandidatesWidgetComponent,
    PerformanceSummaryWidgetComponent,
  ],
})
export class DashboardPageComponent {
  protected readonly dashboard = inject(DashboardStateService);
  private readonly demoMode = inject(DemoModeService);
  protected readonly appRefresh = inject(AppRefreshService);

  protected readonly snapshot = this.dashboard.data;
  protected readonly chartRanges: ChartRange[] = ['1M', '3M', '6M', 'YTD', '1Y', 'ALL'];
  protected readonly selectedRange = signal<ChartRange>('3M');
  /** Number of top/bottom movers to show (3, 5, 7, 10). */
  protected readonly moversCount = signal<number>(5);
  protected readonly moversOptions = [3, 5, 7, 10];
  /** Whether the RSI signals detail table is expanded. */
  protected readonly rsiExpanded = signal(true);
  /** Whether the Market Leadership section is expanded. */
  protected readonly leadershipExpanded = signal(false);
  /** Active tab in the Allocation vs Targets panel: 'sector' | 'role'. */
  protected readonly allocTab = signal<'sector' | 'role'>('sector');
  /** Sector table sort column and direction. Default: percent desc (highest actual first). */
  protected readonly sectorSortCol = signal<'label' | 'percent' | 'targetPercent' | 'delta'>(
    'percent',
  );
  protected readonly sectorSortDir = signal<1 | -1>(-1);

  protected readonly sortedSectorAllocation = computed(() => {
    const items = this.snapshot()?.allocation ?? [];
    const col = this.sectorSortCol();
    const dir = this.sectorSortDir();
    return [...items].sort((a, b) => {
      const av = a[col] as string | number;
      const bv = b[col] as string | number;
      if (typeof av === 'string') return av.localeCompare(bv as string) * dir;
      return ((av as number) - (bv as number)) * dir;
    });
  });

  protected toggleSectorSort(col: 'label' | 'percent' | 'targetPercent' | 'delta'): void {
    if (this.sectorSortCol() === col) {
      this.sectorSortDir.update((d) => (d === 1 ? -1 : 1));
    } else {
      this.sectorSortCol.set(col);
      this.sectorSortDir.set(col === 'label' ? 1 : -1);
    }
  }

  // ── Portfolio-only movers ──────────────────────────────────────────────────
  protected readonly portfolioTopMovers = computed(() =>
    (this.snapshot()?.topMovers ?? []).filter((m) => m.isPortfolio).slice(0, this.moversCount()),
  );
  protected readonly portfolioBottomMovers = computed(() =>
    (this.snapshot()?.bottomMovers ?? []).filter((m) => m.isPortfolio).slice(0, this.moversCount()),
  );
  // ── Watchlist-only movers (not already in portfolio) ──────────────────────
  protected readonly watchlistTopMovers = computed(() =>
    (this.snapshot()?.topMovers ?? [])
      .filter((m) => m.isWatchlist && !m.isPortfolio)
      .slice(0, this.moversCount()),
  );
  protected readonly watchlistBottomMovers = computed(() =>
    (this.snapshot()?.bottomMovers ?? [])
      .filter((m) => m.isWatchlist && !m.isPortfolio)
      .slice(0, this.moversCount()),
  );

  // ── Portfolio Actions & State Changes counts ──────────────────────────────
  protected readonly actionsCount = computed(() => this.dashboard.portfolioActions().length);
  protected readonly stateChangesCount = computed(() => this.dashboard.stateChanges().length);

  protected readonly filteredChartPoints = computed(() => {
    const all = this.snapshot()?.valueHistory ?? [];
    if (all.length === 0 || this.selectedRange() === 'ALL') return all;
    const today = new Date();
    const cutoff = new Date(today);
    switch (this.selectedRange()) {
      case '1M':
        cutoff.setMonth(today.getMonth() - 1);
        break;
      case '3M':
        cutoff.setMonth(today.getMonth() - 3);
        break;
      case '6M':
        cutoff.setMonth(today.getMonth() - 6);
        break;
      case 'YTD':
        cutoff.setMonth(0, 1);
        cutoff.setHours(0, 0, 0, 0);
        break;
      case '1Y':
        cutoff.setFullYear(today.getFullYear() - 1);
        break;
    }
    return all.filter((p) => p.date >= cutoff.toISOString().slice(0, 10));
  });

  protected readonly svgChart = computed((): SvgChart | null => {
    const pts = this.filteredChartPoints();
    if (pts.length < 2) return null;

    const W = 960,
      H = 210,
      padT = 8,
      padB = 28,
      padL = 66,
      padR = 4;
    const iW = W - padL - padR,
      iH = H - padT - padB;

    const vals = pts.map((p) => this.demoMode.maskValue(p.totalValue));
    const rawMin = Math.min(...vals),
      rawMax = Math.max(...vals);
    const spread = rawMax - rawMin || rawMax * 0.01 || 1;
    const lo = rawMin - spread * 0.08,
      hi = rawMax + spread * 0.08;
    const yRange = hi - lo;

    const toX = (i: number) => padL + (i / (pts.length - 1)) * iW;
    const toY = (v: number) => padT + iH - ((v - lo) / yRange) * iH;

    const coords = pts.map((p, i) => ({
      x: toX(i),
      y: toY(this.demoMode.maskValue(p.totalValue)),
    }));

    let line = `M${coords[0].x.toFixed(1)},${coords[0].y.toFixed(1)}`;
    for (let i = 1; i < coords.length; i++) {
      const a = coords[i - 1],
        b = coords[i];
      const mx = ((a.x + b.x) / 2).toFixed(1);
      line += ` C${mx},${a.y.toFixed(1)} ${mx},${b.y.toFixed(1)} ${b.x.toFixed(1)},${b.y.toFixed(1)}`;
    }
    const last = coords[coords.length - 1];
    const bottom = (padT + iH).toFixed(1);
    const area = `${line} L${last.x.toFixed(1)},${bottom} L${padL},${bottom} Z`;

    const step = Math.max(1, Math.floor(pts.length / 5));
    const xLabels = pts
      .filter((_, i) => i === 0 || i === pts.length - 1 || i % step === 0)
      .map((p) => {
        const i = pts.indexOf(p);
        const d = new Date(p.date + 'T00:00:00');
        return {
          x: toX(i),
          label: d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' }),
        };
      });

    // 4 evenly spaced Y-axis labels
    const yLabelCount = 4;
    const yLabels = Array.from({ length: yLabelCount + 1 }, (_, k) => {
      const fraction = k / yLabelCount;
      const val = lo + fraction * yRange;
      const y = padT + iH - fraction * iH;
      return {
        y,
        gridY: y,
        label:
          val >= 1_000_000 ? `$${(val / 1_000_000).toFixed(2)}M` : `$${Math.round(val / 1000)}k`,
      };
    });

    return {
      linePath: line,
      areaPath: area,
      isUp: vals[vals.length - 1] >= vals[0],
      baselineY: toY(vals[0]),
      xLabels,
      yLabels,
      viewBox: `0 0 ${W} ${H}`,
      padL,
    };
  });

  protected value(v: number): number {
    return this.demoMode.maskValue(v);
  }
  protected percent(v: number): number {
    return this.demoMode.maskPercent(v);
  }
  protected signCls(v: number): string {
    return v >= 0 ? 'positive' : 'negative';
  }
  protected signIcon(v: number): string {
    return v >= 0 ? 'north' : 'south';
  }

  /** Format market index price: large numbers as integer, small as 2 decimals. Prices only (>0). */
  protected fmtIdx(price: number): string {
    if (price <= 0) return '—';
    return price > 500
      ? price.toLocaleString('en-US', { maximumFractionDigits: 0 })
      : price.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  /** Format market index change — handles negative values correctly. */
  protected fmtChg(change: number): string {
    const abs = Math.abs(change);
    return abs > 500
      ? abs.toLocaleString('en-US', { maximumFractionDigits: 0 })
      : abs.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  protected allocStatusClass(status: string): string {
    switch (status) {
      case 'good':
        return 'alloc-good';
      case 'watch-over':
      case 'watch-under':
        return 'alloc-watch';
      case 'over':
      case 'under':
        return 'alloc-off';
      default:
        return 'alloc-none';
    }
  }

  protected sumTargets(items: DashboardAllocation[]): number {
    return items.reduce((acc, a) => acc + a.targetPercent, 0);
  }

  protected sumActual(items: DashboardAllocation[]): number {
    return items.reduce((acc, a) => acc + a.percent, 0);
  }

  protected refresh(): void {
    this.appRefresh.refreshAll();
  }
}
