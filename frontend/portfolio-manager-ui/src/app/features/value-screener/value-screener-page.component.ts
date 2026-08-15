import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { EmptyState } from '@ui';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  ActionTrigger,
  TechnicalState,
  ValueScreenerRequest,
  ValueScreenerResult,
  ValueTier,
} from '../../core/models/portfolio.models';
import { GridColumnService } from '../../core/services/grid-column.service';
import { PortfolioApiService } from '../../core/services/portfolio-api.service';
import { GridColumnButtonComponent } from '../../shared/column-config-dialog/grid-column-btn.component';

type SourceMode = 'portfolio' | 'watchlist' | 'adhoc';

@Component({
  selector: 'app-value-screener-page',
  templateUrl: './value-screener-page.component.html',
  styleUrl: './value-screener-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    EmptyState,
    DecimalPipe,
    CurrencyPipe,
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatChipsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSortModule,
    MatTableModule,
    MatTooltipModule,
    GridColumnButtonComponent,
  ],
})
export class ValueScreenerPageComponent implements OnInit {
  private readonly api = inject(PortfolioApiService);
  private readonly snackBar = inject(MatSnackBar);

  // -- Data state -----------------------------------------------------------
  protected readonly loading = signal(false);
  protected readonly refreshing = signal(false);
  protected readonly clearing = signal(false);

  // Persisted results loaded from DB
  protected readonly portfolioResults = signal<ValueScreenerResult[]>([]);
  protected readonly watchlistResults = signal<ValueScreenerResult[]>([]);
  protected readonly portfolioRunAt = signal<string | null>(null);
  protected readonly watchlistRunAt = signal<string | null>(null);

  // Ad-hoc live results
  protected readonly adHocResults = signal<ValueScreenerResult[]>([]);

  // -- Source selection (mutually exclusive) --------------------------------
  protected readonly sourceMode = signal<SourceMode>('portfolio');
  protected readonly adHocInput = signal('');

  // -- Tier card filter (null = show all) -----------------------------------
  protected readonly activeTierFilter = signal<ValueTier | null>(null);

  // -- Sort state -----------------------------------------------------------
  protected readonly sortCol = signal<string>('score');
  protected readonly sortDir = signal<'asc' | 'desc'>('desc');

  protected readonly displayedColumns = inject(GridColumnService).getColumnKeys('value-screener');

  /** Results currently shown in the grid (filtered by tier, then sorted) */
  protected readonly activeResults = computed<ValueScreenerResult[]>(() => {
    const mode = this.sourceMode();
    let list: ValueScreenerResult[];
    if (mode === 'portfolio') list = [...this.portfolioResults()];
    else if (mode === 'watchlist') list = [...this.watchlistResults()];
    else list = [...this.adHocResults()];

    const tierFilter = this.activeTierFilter();
    if (tierFilter) list = list.filter((r) => r.tier === tierFilter);

    const col = this.sortCol();
    const dir = this.sortDir() === 'asc' ? 1 : -1;
    const tierOrder: Record<string, number> = { HighConviction: 0, FairValue: 1, ValueTrap: 2 };

    list.sort((a, b) => {
      switch (col) {
        case 'ticker':
          return a.symbol.localeCompare(b.symbol) * dir;
        case 'score': {
          const td = (tierOrder[a.tier] ?? 3) - (tierOrder[b.tier] ?? 3);
          return td !== 0 ? td * dir : (b.score - a.score) * dir;
        }
        case 'technicalState':
          return a.technicalState.localeCompare(b.technicalState) * dir;
        case 'actionTrigger':
          return a.actionTrigger.localeCompare(b.actionTrigger) * dir;
        default: {
          const td = (tierOrder[a.tier] ?? 3) - (tierOrder[b.tier] ?? 3);
          return td !== 0 ? td : b.score - a.score;
        }
      }
    });
    return list;
  });

  protected readonly allActiveResults = computed<ValueScreenerResult[]>(() => {
    const mode = this.sourceMode();
    if (mode === 'portfolio') return this.portfolioResults();
    if (mode === 'watchlist') return this.watchlistResults();
    return this.adHocResults();
  });

  protected readonly highConviction = computed(() =>
    this.allActiveResults().filter((r) => r.tier === 'HighConviction'),
  );
  protected readonly fairValue = computed(() =>
    this.allActiveResults().filter((r) => r.tier === 'FairValue'),
  );
  protected readonly valueTrap = computed(() =>
    this.allActiveResults().filter((r) => r.tier === 'ValueTrap'),
  );

  protected readonly lastRunAt = computed<string | null>(() => {
    const mode = this.sourceMode();
    if (mode === 'portfolio') return this.portfolioRunAt();
    if (mode === 'watchlist') return this.watchlistRunAt();
    return null;
  });

  ngOnInit(): void {
    // Load latest persisted data from DB without hitting Yahoo Finance
    this.loadLatest();
  }

  // -- Source selection --------------------------------------------------------
  selectPortfolio(): void {
    this.sourceMode.set('portfolio');
    this.activeTierFilter.set(null);
  }
  selectWatchlist(): void {
    this.sourceMode.set('watchlist');
    this.activeTierFilter.set(null);
  }
  selectAdhoc(): void {
    this.sourceMode.set('adhoc');
    this.activeTierFilter.set(null);
  }

  // -- Tier card click -------------------------------------------------------
  filterByTier(tier: ValueTier): void {
    this.activeTierFilter.update((current) => (current === tier ? null : tier));
  }

  // -- Sort change ----------------------------------------------------------
  onSortChange(sort: Sort): void {
    if (!sort.active || sort.direction === '') return;
    this.sortCol.set(sort.active);
    this.sortDir.set(sort.direction as 'asc' | 'desc');
  }

  // -- Load latest persisted data from DB ------------------------------------
  loadLatest(): void {
    this.loading.set(true);
    this.api.getLatestValueScreener().subscribe({
      next: (dto) => {
        this.portfolioResults.set(dto.portfolio ?? []);
        this.watchlistResults.set(dto.watchlist ?? []);
        this.portfolioRunAt.set(dto.portfolioRunAt);
        this.watchlistRunAt.set(dto.watchlistRunAt);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Value screener: failed to load latest', err);
        this.snackBar.open('Could not load saved data. Try refreshing.', 'OK', { duration: 4000 });
        this.loading.set(false);
      },
    });
  }

  // -- Refresh: re-run entire module and persist ----------------------------
  refresh(): void {
    this.refreshing.set(true);
    this.api.refreshValueScreener().subscribe({
      next: (dto) => {
        this.portfolioResults.set(dto.portfolio ?? []);
        this.watchlistResults.set(dto.watchlist ?? []);
        this.portfolioRunAt.set(dto.portfolioRunAt);
        this.watchlistRunAt.set(dto.watchlistRunAt);
        this.activeTierFilter.set(null);
        this.refreshing.set(false);
        this.snackBar.open('Value Screener refreshed and saved.', 'OK', { duration: 3000 });
      },
      error: (err) => {
        console.error('Value screener refresh error', err);
        this.snackBar.open('Refresh failed. Check backend logs.', 'OK', { duration: 4000 });
        this.refreshing.set(false);
      },
    });
  }

  // -- Ad-hoc live analysis trigger ----------------------------------------
  analyze(): void {
    const mode = this.sourceMode();
    if (mode !== 'adhoc') return;
    if (!this.adHocInput().trim()) {
      this.snackBar.open('Enter at least one ticker symbol in the Ad-Hoc field.', 'OK', {
        duration: 3000,
      });
      return;
    }
    const adHocSymbols = this.adHocInput()
      .split(/[\s,;]+/)
      .map((s) => s.trim().toUpperCase())
      .filter((s) => s.length > 0);

    const request: ValueScreenerRequest = {
      includePortfolio: false,
      includeWatchlist: false,
      adHocSymbols,
    };

    this.adHocResults.set([]);
    this.loading.set(true);
    this.api.runValueScreener(request).subscribe({
      next: (data) => {
        this.adHocResults.set(data);
        this.activeTierFilter.set(null);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Value screener ad-hoc error', err);
        this.snackBar.open('Analysis failed. Check backend logs.', 'OK', { duration: 4000 });
        this.loading.set(false);
      },
    });
  }

  // -- Export CSV -----------------------------------------------------------
  exportCsv(): void {
    const rows = this.activeResults();
    if (rows.length === 0) return;

    const headers = [
      'Symbol',
      'Description',
      'Origin',
      'Tier',
      'Score',
      'Technical State',
      'Action Trigger',
      'Earnings Yield %',
      'FCF Yield %',
      'P/B',
      'Piotroski',
      'ROIC %',
      'Div Yield %',
      'Price',
      'RSI',
      '52W High',
      '52W Low',
      'Sector',
      'Analyzed At',
    ];

    const escape = (v: string | number) => {
      const s = String(v);
      return s.includes(',') || s.includes('"') || s.includes('\n')
        ? `"${s.replace(/"/g, '""')}"`
        : s;
    };

    const csvRows = [
      headers.map(escape).join(','),
      ...rows.map((r) =>
        [
          r.symbol,
          r.description,
          r.origin,
          r.tier,
          r.score,
          this.techStateLabel(r.technicalState),
          this.actionLabel(r.actionTrigger),
          r.earningsYield.toFixed(2),
          r.fcfYieldProxy.toFixed(2),
          r.priceToBook.toFixed(2),
          r.piotroskiScore,
          r.roicProxy.toFixed(2),
          r.dividendYield.toFixed(2),
          r.currentPrice.toFixed(2),
          r.currentRsi.toFixed(1),
          r.week52High,
          r.week52Low,
          r.sector,
          r.analyzedAt,
        ]
          .map(escape)
          .join(','),
      ),
    ];

    const csv = csvRows.join('\n');
    const blob = new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `value-screener-${this.sourceMode()}-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  // -- Clear persisted data ------------------------------------------------
  clearData(): void {
    if (
      !confirm(
        'Clear all saved Value Screener data for Portfolio and Watchlist? This cannot be undone.',
      )
    )
      return;
    this.clearing.set(true);
    this.api.clearValueScreenerData().subscribe({
      next: () => {
        this.portfolioResults.set([]);
        this.watchlistResults.set([]);
        this.portfolioRunAt.set(null);
        this.watchlistRunAt.set(null);
        this.activeTierFilter.set(null);
        this.clearing.set(false);
        this.snackBar.open('Saved data cleared. Use Refresh to run a new analysis.', 'OK', {
          duration: 4000,
        });
      },
      error: () => {
        this.clearing.set(false);
        this.snackBar.open('Failed to clear data.', 'Dismiss', { duration: 4000 });
      },
    });
  }

  // -- Label helpers -------------------------------------------------------
  tierLabel(tier: ValueTier): string {
    const m: Record<ValueTier, string> = {
      HighConviction: 'High-Conviction',
      FairValue: 'Fair Value',
      ValueTrap: 'Value Trap',
    };
    return m[tier];
  }
  tierClass(tier: ValueTier): string {
    const m: Record<ValueTier, string> = {
      HighConviction: 'tier-high',
      FairValue: 'tier-fair',
      ValueTrap: 'tier-trap',
    };
    return m[tier];
  }

  techStateLabel(state: TechnicalState): string {
    const m: Record<TechnicalState, string> = {
      DeepValueReversal: 'Deep Value Reversal',
      OverboughtMomentum: 'Overbought Momentum',
      OverboughtPullback: 'Overbought Pullback',
      SidewaysConsolidation: 'Sideways Consolidation',
      MeanReversion: 'Mean Reversion',
      HighVolumeExhaustion: 'High-Volume Exhaustion',
    };
    return m[state] ?? state;
  }

  techStateTooltip(state: TechnicalState): string {
    const m: Record<TechnicalState, string> = {
      DeepValueReversal:
        'Deep Value Reversal: The stock has been beaten down and ignored for a long time (making it fundamentally cheap), but it is finally printing its very first technical signs of bottoming out. Buyers are stepping back in, and the long-term price chart is starting to curve upward.',
      OverboughtMomentum:
        'Overbought Momentum: The stock is rocketing upward rapidly. It is technically "stretched" too high too fast, but the buying pressure is so intense that the trend is overriding standard exhaustion limits and continuing to climb.',
      OverboughtPullback:
        'Overbought Pullback: The stock recently experienced a massive, vertical spike. Over the last day or two, the price started dropping slightly as traders locked in profits, which is actively cooling down your short-term indicators.',
      SidewaysConsolidation:
        'Sideways Consolidation: The stock price is bouncing around inside a tight, predictable flat box, moving left-to-right. It is essentially resting and gathering energy before its next major directional move.',
      MeanReversion:
        'Mean Reversion: The stock stretched way too far away from its mathematical average price (like its 20-day or 50-day moving average). It is now snapping back like a rubber band toward its normal baseline.',
      HighVolumeExhaustion:
        'High-Volume Exhaustion: The stock had a chaotic, massive surge on extreme trading volume (like a retail-driven short squeeze), but it completely ran out of new buyers at the peak. The price is now sliding backward because the buying power is totally spent.',
    };
    return m[state] ?? state;
  }

  techStateClass(state: TechnicalState): string {
    const m: Record<TechnicalState, string> = {
      DeepValueReversal: 'state-reversal',
      OverboughtMomentum: 'state-overbought',
      OverboughtPullback: 'state-pullback',
      SidewaysConsolidation: 'state-sideways',
      MeanReversion: 'state-mean',
      HighVolumeExhaustion: 'state-exhaustion',
    };
    return m[state] ?? 'state-neutral';
  }

  actionLabel(action: ActionTrigger): string {
    const m: Record<ActionTrigger, string> = {
      AccumulateYield: 'Accumulate Yield',
      AccumulateValue: 'Accumulate Value',
      BuyLimitAlert: 'Buy Limit Alert',
      HoldRideTrend: 'Hold / Ride Trend',
      ValueTrapWarning: 'Value Trap Warning',
      Observe: 'Observe',
    };
    return m[action] ?? action;
  }

  actionTooltip(action: ActionTrigger): string {
    const m: Record<ActionTrigger, string> = {
      AccumulateYield:
        'Accumulate Yield: Tailored specifically for blue-chip dividend payers (like major Canadian banks or utilities). The stock is fundamentally stable but trading at a temporary discount. Buy more shares right here to lock in a higher-than-average dividend payout percentage.',
      AccumulateValue:
        'Accumulate Value: The company possesses elite financial health metrics (a high Piotroski F-Score and strong cash flows) and is trading below what its business is actually worth. This price is a steal for the long haul; start steadily buying and hoarding shares.',
      BuyLimitAlert:
        'Buy Limit Alert: The stock is fundamentally excellent but currently stuck in a choppy or sideways chart. Set an automatic buy order at a specific, lower support baseline and let the market come to you, rather than chasing it at market price.',
      HoldRideTrend:
        'Hold / Ride Trend: The asset is technically overbought, but the upward trend is incredibly healthy. Do not sell your winners early, and do not try to short this. Sit on your hands and let the momentum run.',
      ValueTrapWarning:
        "Value Trap Warning: The ultimate emergency brake. The stock looks incredibly cheap on paper or is experiencing a heavy wave of retail hype, but its internal balance sheet is structurally decaying (negative cash flow, rising debt, low efficiency). Do not touch this; it's cheap for a reason and highly likely to go lower.",
      Observe:
        'Observe: The asset shows stable pricing relative to its capital layout but no strong entry signal yet. Monitor for a better entry point or a score improvement.',
    };
    return m[action] ?? action;
  }

  actionClass(action: ActionTrigger): string {
    if (action === 'AccumulateYield' || action === 'AccumulateValue') return 'action-buy';
    if (action === 'BuyLimitAlert') return 'action-limit';
    if (action === 'ValueTrapWarning') return 'action-trap';
    if (action === 'HoldRideTrend') return 'action-hold';
    return 'action-observe';
  }

  // -- Score breakdown tooltip ---------------------------------------------
  scoreTooltip(r: ValueScreenerResult): string {
    return [
      `Score: ${r.score}/10`,
      `  Earnings Yield (${r.earningsYield.toFixed(1)}%): +${r.scoreEarningsYield}pts`,
      `  FCF Yield proxy (${r.fcfYieldProxy.toFixed(1)}%): +${r.scoreFcfYield}pts`,
      `  Price/Book (${r.priceToBook > 0 ? r.priceToBook.toFixed(2) : 'N/A'}): +${r.scorePriceToBook}pts`,
      `  Piotroski (${r.piotroskiScore}/9): +${r.scorePiotroski}pts`,
      `  ROIC proxy (${r.roicProxy.toFixed(1)}%): +${r.scoreRoic}pts`,
      r.dividendYield > 0 ? `  Dividend Yield: ${r.dividendYield.toFixed(2)}%` : '',
    ]
      .filter(Boolean)
      .join('\n');
  }
}
