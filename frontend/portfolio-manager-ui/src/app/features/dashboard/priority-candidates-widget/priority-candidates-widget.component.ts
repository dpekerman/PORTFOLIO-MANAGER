import { CurrencyPipe, DecimalPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  ActionScoreDto,
  RsiScanResult,
  ValueScreenerResult,
} from '../../../core/models/portfolio.models';
import { DashboardStore } from '../../../core/store/dashboard.store';
import { DecisionEngineService } from '../../../core/services/decision-engine.service';
import { PortfolioApiService } from '../../../core/services/portfolio-api.service';
import {
  PriorityCandidateRow,
  buildEligibleCandidates,
  mergeRsiMaps,
} from '../../../core/services/priority-candidate-eligibility';
import { ScannerStore } from '../../../core/store/scanner.store';
import { WatchlistRsiStore } from '../../../core/store/watchlist-rsi.store';
import { currencyCodeForAnalysis, formatTrendShift } from '../../../core/technical-display';

export { buildEligibleCandidates, mergeRsiMaps };
export type { PriorityCandidateRow };

export function isTechnicalDataPending(
  cachedMapSize: number,
  oversoldCount: number,
  overboughtCount: number,
): boolean {
  return cachedMapSize === 0 && oversoldCount === 0 && overboughtCount === 0;
}

@Component({
  selector: 'app-priority-candidates-widget',
  templateUrl: './priority-candidates-widget.component.html',
  styleUrl: './priority-candidates-widget.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CurrencyPipe, DecimalPipe, MatIconModule, MatProgressBarModule, MatTooltipModule],
})
export class PriorityCandidatesWidgetComponent implements OnInit {
  protected readonly dashboard = inject(DashboardStore);
  private readonly scanner = inject(ScannerStore);
  // Cache-only consumer: never call triggerRefresh()/enableAutoRefreshOnWatchlistChange() here.
  // Live re-analysis is triggered only by the Watchlist page or the bounded AppRefreshStore pipeline.
  private readonly watchlistRsi = inject(WatchlistRsiStore);
  private readonly api = inject(PortfolioApiService);
  private readonly engine = inject(DecisionEngineService);

  protected readonly scores = this.dashboard.actionScores;
  protected readonly loading = this.dashboard.actionScoresLoading;
  /** True while the full-watchlist technical cache has never been populated yet (no page visited it, no refresh run). */
  protected readonly technicalDataPending = computed(() =>
    isTechnicalDataPending(
      this.watchlistRsi.rsiMap().size,
      this.scanner.oversold().length,
      this.scanner.overbought().length,
    ),
  );

  private readonly valueScreenerMap = signal<Map<string, ValueScreenerResult>>(new Map());

  /** Same precedence as the Watchlist page: full-watchlist cache first, then oversold/overbought overlay. */
  private readonly rsiMap = computed<Map<string, RsiScanResult>>(() =>
    mergeRsiMaps(this.watchlistRsi.rsiMap(), [
      ...this.scanner.oversold(),
      ...this.scanner.overbought(),
    ]),
  );

  /** Watchlist-only, non-owned candidates that clear the canonical Final Action + score gate. */
  protected readonly eligibleCandidates = computed<PriorityCandidateRow[]>(() =>
    buildEligibleCandidates(this.scores(), this.rsiMap(), this.valueScreenerMap(), this.engine),
  );

  ngOnInit(): void {
    this.dashboard.loadActionScores();
    this.api.getLatestValueScreener().subscribe({
      next: (dto) => {
        const map = new Map<string, ValueScreenerResult>();
        for (const r of [...(dto.watchlist ?? []), ...(dto.portfolio ?? [])])
          map.set(r.symbol.toUpperCase(), r);
        this.valueScreenerMap.set(map);
      },
      error: () => {}, // Non-critical — candidates fall back to no value-screener context
    });
  }

  protected badgeCls(priorityLevel: string): string {
    return priorityLevel === 'HIGH' ? 'badge-high' : 'badge-watch';
  }

  protected badgeLabel(priorityLevel: string): string {
    return priorityLevel === 'HIGH' ? 'HIGH' : 'WATCH';
  }

  protected eodBadgeLabel(score: {
    latestEodSignalState?: string | null;
    latestEodIsNew?: boolean;
    latestEodIsInvalidated?: boolean;
  }): string {
    if (score.latestEodIsInvalidated) return 'EOD X';
    if (score.latestEodSignalState === 'Active') return score.latestEodIsNew ? 'EOD NEW' : 'EOD';
    return 'EOD DEV';
  }

  protected eodTooltip(score: {
    latestEodSignalState?: string | null;
    latestEodScanType?: string | null;
  }): string {
    return `Latest EOD signal\nState: ${score.latestEodSignalState ?? 'n/a'}\nScan: ${score.latestEodScanType ?? 'n/a'}\nScore boost: +2 technical points`;
  }

  /** Strip leading emoji + space from trend shift string. */
  protected trendLabel(raw: string): string {
    return formatTrendShift(raw, '');
  }

  /** CSS modifier class derived from the leading emoji. */
  protected trendDotCls(raw: string): string {
    if (raw.startsWith('🟢')) return 'dot-green';
    if (raw.startsWith('🟡')) return 'dot-yellow';
    if (raw.startsWith('🔴')) return 'dot-red';
    return 'dot-neutral';
  }

  protected analysisCurrency(score: ActionScoreDto): string {
    return currencyCodeForAnalysis(score.symbol, score.analysisCurrency);
  }
}
