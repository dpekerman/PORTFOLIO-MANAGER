import { CurrencyPipe, DecimalPipe, NgClass } from '@angular/common';
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
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  ActionTrigger,
  TechnicalState,
  ValueScreenerRequest,
  ValueScreenerResult,
  ValueTier,
} from '../../core/models/portfolio.models';
import { PortfolioApiService } from '../../core/services/portfolio-api.service';

type SourceMode = 'portfolio' | 'watchlist' | 'adhoc';

@Component({
  selector: 'app-value-screener-page',
  templateUrl: './value-screener-page.component.html',
  styleUrl: './value-screener-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    NgClass,
    DecimalPipe,
    CurrencyPipe,
    FormsModule,
    MatButtonModule,
    MatChipsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatTableModule,
    MatTooltipModule,
  ],
})
export class ValueScreenerPageComponent implements OnInit {
  private readonly api = inject(PortfolioApiService);
  private readonly snackBar = inject(MatSnackBar);

  // -- Data state -----------------------------------------------------------
  protected readonly loading = signal(false);
  protected readonly results = signal<ValueScreenerResult[]>([]);
  protected readonly lastAnalyzedAt = signal<string | null>(null);

  // -- Source selection (mutually exclusive) --------------------------------
  protected readonly sourceMode = signal<SourceMode>('portfolio');
  protected readonly adHocInput = signal('');

  protected readonly includePortfolio = computed(() => this.sourceMode() === 'portfolio');
  protected readonly includeWatchlist = computed(() => this.sourceMode() === 'watchlist');

  protected readonly displayedColumns = [
    'ticker',
    'description',
    'technicalState',
    'score',
    'actionTrigger',
  ];

  protected readonly filteredSortedResults = computed<ValueScreenerResult[]>(() => {
    const list = [...this.results()];
    // Default: tier priority (HighConviction first) then score descending
    const tierOrder: Record<string, number> = { HighConviction: 0, FairValue: 1, ValueTrap: 2 };
    list.sort((a, b) => {
      const td = (tierOrder[a.tier] ?? 3) - (tierOrder[b.tier] ?? 3);
      return td !== 0 ? td : b.score - a.score;
    });
    return list;
  });

  protected readonly highConviction = computed(() =>
    this.results().filter((r) => r.tier === 'HighConviction'),
  );
  protected readonly fairValue = computed(() =>
    this.results().filter((r) => r.tier === 'FairValue'),
  );
  protected readonly valueTrap = computed(() =>
    this.results().filter((r) => r.tier === 'ValueTrap'),
  );

  ngOnInit(): void {
    // Auto-run analysis on page load with Portfolio selected (mirrors RSI Scanner UX)
    this.analyze();
  }

  // -- Source selection: Portfolio & Watchlist auto-execute; Ad-Hoc needs manual trigger ----------
  selectPortfolio(): void {
    this.sourceMode.set('portfolio');
    this.analyze();
  }
  selectWatchlist(): void {
    this.sourceMode.set('watchlist');
    this.analyze();
  }
  selectAdhoc(): void {
    this.sourceMode.set('adhoc');
  }

  // -- Analysis trigger ----------------------------------------------------
  analyze(): void {
    const mode = this.sourceMode();
    if (mode === 'adhoc' && !this.adHocInput().trim()) {
      this.snackBar.open('Enter at least one ticker symbol in the Ad-Hoc field.', 'OK', {
        duration: 3000,
      });
      return;
    }
    const adHocSymbols =
      mode === 'adhoc'
        ? this.adHocInput()
            .split(/[\s,;]+/)
            .map((s) => s.trim().toUpperCase())
            .filter((s) => s.length > 0)
        : [];

    const request: ValueScreenerRequest = {
      includePortfolio: mode === 'portfolio',
      includeWatchlist: mode === 'watchlist',
      adHocSymbols,
    };

    // Clear previous results before starting fresh analysis
    this.results.set([]);
    this.loading.set(true);
    this.api.runValueScreener(request).subscribe({
      next: (data) => {
        this.results.set(data);
        this.lastAnalyzedAt.set(new Date().toLocaleTimeString());
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Value screener error', err);
        this.snackBar.open('Analysis failed. Check backend logs.', 'OK', { duration: 4000 });
        this.loading.set(false);
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
