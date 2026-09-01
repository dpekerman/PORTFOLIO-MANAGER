import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';

import * as XLSX from 'xlsx';
import {
  PriceStructureResult,
  RsiScanResult,
  ValueScreenerResult,
  WatchlistSummary,
} from '../../core/models/portfolio.models';
import {
  priceStructureLabel as formatPriceStructureLabel,
  priceStructureTooltip as formatPriceStructureTooltip,
  priceStructureSortRank,
} from '../../core/price-structure-display';
import { AppRefreshService } from '../../core/services/app-refresh.service';
import { AuthStateService } from '../../core/services/auth-state.service';
import {
  DecisionEngineService,
  GapStatus,
  PageDecision,
  WatchlistValueContext,
} from '../../core/services/decision-engine.service';
import { DemoModeService } from '../../core/services/demo-mode.service';
import { GridColumnService } from '../../core/services/grid-column.service';
import { PortfolioApiService } from '../../core/services/portfolio-api.service';
import { ScannerStateService } from '../../core/services/scanner-state.service';
import { ScreenRefreshService } from '../../core/services/screen-refresh.service';
import { WatchlistRsiStateService } from '../../core/services/watchlist-rsi-state.service';
import { WatchlistStateService } from '../../core/services/watchlist-state.service';
import { GridColumnButtonComponent } from '../../shared/column-config-dialog/grid-column-btn.component';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog.component';
import { ScreenRefreshProgressComponent } from '../../shared/screen-refresh-progress/screen-refresh-progress.component';
import { WatchlistCardSkeletonComponent } from '../../shared/skeleton/watchlist-card-skeleton.component';
import {
  TransactionNotesDialogComponent,
  TransactionNotesDialogData,
  TransactionNotesDialogResult,
} from '../transactions/transaction-notes-dialog/transaction-notes-dialog.component';
import {
  AddWatchlistDialogComponent,
  AddWatchlistDialogResult,
} from './add-watchlist-dialog.component';
import { WatchlistCardComponent } from './watchlist-card.component';

type ViewMode = 'card' | 'grid';
type SortColumn =
  | 'symbol'
  | 'company'
  | 'role'
  | 'earningsDate'
  | 'price'
  | 'change'
  | 'changePct'
  | 'sector'
  | 'rsi'
  | 'buyScore'
  | 'trendSetup'
  | 'momentumShift'
  | 'channel'
  | 'priceStructure'
  | 'finalAction'
  | 'technical'
  | 'valueScore'
  | 'valueStatus'
  | 'reversalP'
  | 'maStatus'
  | 'fib38_2'
  | 'fib50'
  | 'fib61_8'
  | 'fib78_6'
  | 'fibZone'
  | 'fibStatus'
  | 'fibDist';
type SortDir = 'asc' | 'desc';

@Component({
  selector: 'app-watchlist-page',
  templateUrl: './watchlist-page.component.html',
  styleUrl: './watchlist-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    CurrencyPipe,
    DatePipe,
    DecimalPipe,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSortModule,
    MatTableModule,
    MatTooltipModule,
    WatchlistCardComponent,
    WatchlistCardSkeletonComponent,
    GridColumnButtonComponent,
    ScreenRefreshProgressComponent,
  ],
})
export class WatchlistPageComponent {
  protected readonly watchlist = inject(WatchlistStateService);
  private readonly demoMode = inject(DemoModeService);
  private readonly dialog = inject(MatDialog);
  private readonly api = inject(PortfolioApiService);
  private readonly scanner = inject(ScannerStateService);
  private readonly watchlistRsi = inject(WatchlistRsiStateService);
  protected readonly authState = inject(AuthStateService);
  protected readonly appRefresh = inject(AppRefreshService);

  /** Mini refresh popup for the RSI enrichment pass (independent of the unified app refresh). */
  protected readonly screenRefresh = new ScreenRefreshService('watchlist');

  protected readonly trackById = (_: number, w: WatchlistSummary): number => w.item.id;

  // ── Value Screener data map (symbol → result) ─────────────────────────────
  // Loaded from latest persisted DB snapshot to provide Technical / Value Score columns
  protected readonly vsMap = signal<Map<string, ValueScreenerResult>>(new Map());
  private readonly engine = inject(DecisionEngineService);
  protected readonly rsiLoading = this.watchlistRsi.rsiLoading;

  protected readonly viewMode = signal<ViewMode>('grid');
  protected readonly filterText = signal('');
  protected readonly filterTrendSetup = signal('');
  protected readonly filterFinalAction = signal('');
  protected readonly filterFavorites = signal(false);
  protected readonly tierFilter = signal<string>('Active');
  protected readonly tiers = ['All', 'Active', 'Strategic', 'Universe'];
  protected readonly sortCol = signal<SortColumn>('symbol');
  protected readonly sortDir = signal<SortDir>('asc');
  protected readonly roles = [
    'Core',
    'Strategic',
    'Strategic-Income',
    'Swing',
    'Speculative',
    'Options',
  ];

  constructor() {
    // Load latest Value Screener results for watchlist context
    this.api.getLatestValueScreener().subscribe({
      next: (dto) => {
        const map = new Map<string, ValueScreenerResult>();
        for (const r of [...(dto.watchlist ?? []), ...(dto.portfolio ?? [])]) {
          map.set(r.symbol.toUpperCase(), r);
        }
        this.vsMap.set(map);
      },
      error: () => {}, // Non-critical
    });

    // Bridge the RSI enrichment loading state into the mini-popup (indeterminate — no ticker count).
    effect(() => {
      if (this.rsiLoading()) {
        this.screenRefresh.startRefresh(0);
      } else {
        this.screenRefresh.completeRefresh();
      }
    });
  }

  /** Actually stops the in-flight RSI scan, rather than just hiding the mini-popup. */
  protected onScreenRefreshCancelled(): void {
    this.watchlistRsi.cancelRefresh();
  }

  protected readonly rsiMap = computed<Map<string, RsiScanResult>>(() => {
    const map = new Map<string, RsiScanResult>(this.watchlistRsi.rsiMap());
    for (const r of [...this.scanner.oversold(), ...this.scanner.overbought()])
      map.set(r.symbol.toUpperCase(), r);
    return map;
  });

  protected rsiForSymbol(symbol: string): number | null {
    return this.rsiMap().get(symbol.toUpperCase())?.rsi ?? null;
  }

  protected channelForSymbol(symbol: string): RsiScanResult | null {
    return this.rsiMap().get(symbol.toUpperCase()) ?? null;
  }

  protected channelLabel(symbol: string): string {
    const state = this.channelForSymbol(symbol)?.channelState;
    return state === 'THIRD_TOUCH_APPROACHING'
      ? '3rd Rail Approaching'
      : state === 'THIRD_TOUCH_TEST'
        ? '3rd Rail Test'
        : state === 'LOWER_RAIL_APPROACHING'
          ? 'Lower Rail Approaching'
          : state === 'LOWER_RAIL_RETEST'
            ? 'Lower Rail Retest'
            : state === 'REVERSAL_DEVELOPING'
              ? 'Reversal Developing'
              : state === 'BOUNCE_CONFIRMED'
                ? 'Bounce Confirmed'
                : state === 'CHANNEL_BROKEN'
                  ? 'Channel Broken'
                  : '';
  }

  protected channelTooltip(symbol: string): string {
    const channel = this.channelForSymbol(symbol);
    if (!channel || !this.channelLabel(symbol)) return '';
    const touches = channel.channelTouchDetails
      .map(
        (touch) =>
          `#${touch.touchNumber}  ${touch.touchDate.slice(0, 10)}\nRail: ${touch.railPrice.toFixed(2)}\nLow: ${touch.actualLow.toFixed(2)}\nBounce: +${touch.bounceATR.toFixed(2)} ATR`,
      )
      .join('\n\n');
    const interaction =
      channel.priorConfirmedLowerTouches === 2
        ? '3rd Touch'
        : `${channel.priorConfirmedLowerTouches + 1}th Touch`;
    return `RISING CHANNEL\n\nCURRENT STRUCTURE\nState: ${this.channelLabel(symbol)}\nInteraction: ${interaction}\nQuality: ${channel.channelQuality}/100\nEOD Close: ${channel.currentPrice.toFixed(2)}\nLower Rail: ${channel.lowerRailToday.toFixed(2)}\nDistance: ${channel.distanceToLowerRailPercent.toFixed(2)}%\nDistance ATR: ${channel.distanceToLowerRailATR.toFixed(2)}\n\nTOUCH HISTORY\nConfirmed Touches: ${channel.priorConfirmedLowerTouches}\n${touches}\n\nGAP\nNearest Open Gap Above: ${channel.nearestOpenGapAbove?.toFixed(2) ?? '—'}`;
  }

  protected channelSortValue(symbol: string): number {
    const state = this.channelForSymbol(symbol)?.channelState;
    return (
      {
        NONE: 0,
        CHANNEL_ACTIVE: 0,
        THIRD_TOUCH_APPROACHING: 1,
        THIRD_TOUCH_TEST: 2,
        LOWER_RAIL_APPROACHING: 1,
        LOWER_RAIL_RETEST: 2,
        REVERSAL_DEVELOPING: 3,
        BOUNCE_CONFIRMED: 4,
        CHANNEL_BROKEN: 5,
      }[state ?? 'NONE'] ?? 0
    );
  }

  protected priceStructureForSymbol(
    symbol: string,
    summary?: WatchlistSummary,
  ): PriceStructureResult | null {
    const key = symbol.toUpperCase();
    return (
      summary?.priceStructure ??
      this.watchlist.items().find((item) => item.item.symbol.toUpperCase() === key)
        ?.priceStructure ??
      this.rsiMap().get(key)?.priceStructure ??
      null
    );
  }

  protected priceStructureLabel(symbol: string, summary?: WatchlistSummary): string {
    const structure = this.priceStructureForSymbol(symbol, summary);
    return formatPriceStructureLabel(this.priceStructureForSymbol(symbol, summary), (value) =>
      this.demoMode.maskValue(value),
    );
  }

  protected priceStructureTooltip(symbol: string, summary?: WatchlistSummary): string {
    return formatPriceStructureTooltip(this.priceStructureForSymbol(symbol, summary), (value) =>
      this.demoMode.maskValue(value),
    );
  }

  protected priceStructureSortValue(symbol: string, summary?: WatchlistSummary): number {
    const structure = this.priceStructureForSymbol(symbol, summary);
    return priceStructureSortRank(this.priceStructureForSymbol(symbol, summary));
  }

  private formatPriceStructureState(state: string): string {
    return state
      .split('_')
      .map((part) => part.charAt(0) + part.slice(1).toLowerCase())
      .join(' ');
  }

  protected decisionForSymbol(symbol: string, role: string | null): PageDecision | null {
    const r = this.rsiMap().get(symbol.toUpperCase());
    if (!r) return null;
    const vs = this.vsMap().get(symbol.toUpperCase());
    const bs = this.buyScoreForSymbol(symbol);
    const ctx: WatchlistValueContext = {
      buyScore: bs?.score ?? null,
      valueTrapWarning: vs?.actionTrigger === 'ValueTrapWarning',
      valueScore: vs?.score ?? null,
    };
    return this.engine.translateForWatchlist(r, role, ctx);
  }

  protected valueDataForSymbol(symbol: string): {
    technical: string;
    score: number;
    status: string;
    tooltip: string;
    technicalState: string;
  } | null {
    const vs = this.vsMap().get(symbol.toUpperCase());
    if (!vs) return null;
    const techLabels: Record<string, string> = {
      DeepValueReversal: 'Deep Value Reversal',
      OverboughtMomentum: 'Overbought Momentum',
      OverboughtPullback: 'Overbought Pullback',
      SidewaysConsolidation: 'Sideways Consolidation',
      MeanReversion: 'Mean Reversion',
      HighVolumeExhaustion: 'High-Volume Exhaustion',
    };
    const techTooltips: Record<string, string> = {
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
    const actionLabels: Record<string, string> = {
      AccumulateYield: 'Accumulate Yield',
      AccumulateValue: 'Accumulate Value',
      BuyLimitAlert: 'Buy Limit Alert',
      HoldRideTrend: 'Hold / Ride Trend',
      ValueTrapWarning: 'Value Trap Warning',
      Observe: 'Observe',
    };
    return {
      technical: techLabels[vs.technicalState] ?? vs.technicalState,
      tooltip: techTooltips[vs.technicalState] ?? vs.technicalState,
      score: vs.score,
      status: actionLabels[vs.actionTrigger] ?? vs.actionTrigger,
      technicalState: vs.technicalState,
    };
  }

  protected techStateClass(technicalState: string): string {
    const m: Record<string, string> = {
      DeepValueReversal: 'state-reversal',
      OverboughtMomentum: 'state-overbought',
      OverboughtPullback: 'state-pullback',
      SidewaysConsolidation: 'state-sideways',
      MeanReversion: 'state-mean',
      HighVolumeExhaustion: 'state-exhaustion',
    };
    return m[technicalState] ?? 'state-neutral';
  }

  protected valueScoreTooltip(score: number): string {
    if (score >= 8)
      return `High Conviction (${score.toFixed(1)}/10): Strong fundamental value signals.`;
    if (score >= 5)
      return `Fair Value (${score.toFixed(1)}/10): Moderate value signals — not yet high conviction.`;
    return `Value Trap Warning (${score.toFixed(1)}/10): Weak value signals. May look cheap for a reason.`;
  }

  protected valueStatusTooltip(status: string): string {
    const t: Record<string, string> = {
      'Accumulate Yield':
        'Strong dividend yield + value signals. Consider adding to income position.',
      'Accumulate Value':
        'Value signals confirmed with constructive technicals. Good entry opportunity.',
      'Buy Limit Alert': 'Near value threshold. Set a limit order — do not chase price higher.',
      'Hold / Ride Trend':
        'Position performing well. Continue holding with trailing stop discipline.',
      'Value Trap Warning':
        'Looks cheap but may be a value trap. Fundamentals deteriorating — avoid.',
      Observe: 'No actionable signal yet. Monitor for improving signals before entering.',
    };
    return t[status] ?? status;
  }

  protected valueScoreClass(score: number): string {
    if (score >= 8) return 'vs-high';
    if (score >= 5) return 'vs-fair';
    return 'vs-trap';
  }

  protected valueStatusClass(status: string): string {
    if (status.includes('Trap')) return 'action-trap';
    if (status.includes('Accumulate')) return 'action-buy';
    if (status.includes('Hold')) return 'action-hold';
    if (status.includes('Buy Limit')) return 'action-limit';
    return 'action-observe';
  }

  protected probClass(prob: string): string {
    if (prob === 'High') return 'prob-high';
    if (prob === 'Medium') return 'prob-medium';
    return 'prob-low';
  }

  protected gapStatusClass(status: GapStatus): string {
    if (status === 'Gap Up - Strong') return 'gap-strong';
    if (status === 'Gap Up - Weak') return 'gap-weak';
    if (status === 'Gap Up - Failed') return 'gap-failed';
    return '';
  }

  protected gapStatusIcon(status: GapStatus): string {
    if (status === 'Gap Up - Strong') return 'trending_up';
    if (status === 'Gap Up - Weak') return 'trending_flat';
    if (status === 'Gap Up - Failed') return 'trending_down';
    return 'remove';
  }

  protected analystForSymbol(
    symbol: string,
  ): { price: number | null; upside: number | null } | null {
    const r = this.rsiMap().get(symbol.toUpperCase());
    if (!r) return null;
    return { price: r.analystTargetPrice ?? null, upside: r.analystTargetUpside ?? null };
  }

  protected changeForSymbol(
    symbol: string,
  ): { change: number | null; changePct: number | null } | null {
    const r = this.rsiMap().get(symbol.toUpperCase());
    if (!r) return null;
    return { change: r.change ?? null, changePct: r.changePercent ?? null };
  }

  /**
   * Computes BUY Score (0–5) for a symbol from the RSI scan result.
   * Each of 5 checks contributes 1 point:
   *  1. Close > EMA9
   *  2. RSI14 > RSI9EMA (only when signal available)
   *  3. MACD Histogram Improving (macdHistDelta > 0)
   *  4. CloseLocation >= 0.50
   *  5. VolumeRatio20 >= 1.0
   */
  protected buyScoreForSymbol(symbol: string): {
    score: number;
    tooltip: string;
    available: boolean;
  } | null {
    const r = this.rsiMap().get(symbol.toUpperCase());
    if (!r) return null;

    const close = r.currentPrice;
    const ema9 = r.ema9Price ?? 0;
    const rsi = r.rsi;
    const rsiSig = r.rsiSignal ?? rsi;
    const rsiSigAvail = r.rsiSignalAvailable;
    const macdImproving = r.macdHistDelta > 0;

    // CloseLocation: where close sits in today's high-low range
    const dayH = r.dayHigh > 0 ? r.dayHigh : close;
    const dayL = r.dayLow > 0 ? r.dayLow : close;
    const range = dayH - dayL;
    const closeLocation = range > 0 ? (close - dayL) / range : 0.5;

    const vol = r.volumeRatio ?? 0;

    const c1 = ema9 > 0 && close > ema9;
    const c2 = rsiSigAvail ? rsi > rsiSig : false;
    const c3 = macdImproving;
    const c4 = closeLocation >= 0.5;
    const c5 = vol >= 1.0;

    const score = [c1, c2, c3, c4, c5].filter(Boolean).length;

    const ck = (v: boolean) => (v ? '✅' : '❌');
    const tooltip = [
      `${ck(c1)} Close > EMA9`,
      `${ck(c2)} RSI14 > RSI9EMA${rsiSigAvail ? '' : ' (unavailable)'}`,
      `${ck(c3)} MACD Histogram Improving`,
      `${ck(c4)} CloseLocation >= 0.50`,
      `${ck(c5)} VolumeRatio20 >= 1.0`,
    ].join('\n');

    return { score, tooltip, available: true };
  }

  protected maStatusForSymbol(symbol: string): 'STRONG BUY' | null {
    const r = this.rsiMap().get(symbol.toUpperCase());
    if (!r) return null;
    const price = r.currentPrice;
    const ma10 = r.ema10Price;
    const ma20 = r.ema20Price;
    const ma50 = r.sma50Price;
    if (!ma10 || !ma20 || !ma50 || !r.has200Dma || r.dma200Deviation === undefined) return null;
    const ma200 = price / (1 + r.dma200Deviation / 100);
    if (price > ma10 && ma10 > ma20 && ma20 > ma50 && ma50 > ma200) return 'STRONG BUY';
    return null;
  }

  protected fibForSymbol(symbol: string) {
    return this.rsiMap().get(symbol.toUpperCase()) ?? null;
  }

  protected fibZoneClass(zone: string): string {
    switch (zone) {
      case 'Value Zone':
        return 'fib-zone-value';
      case 'Key Fib Support':
        return 'fib-zone-key';
      case 'Shallow Pullback':
        return 'fib-zone-shallow';
      case 'Normal Pullback':
        return 'fib-zone-normal';
      case 'Deep Pullback':
        return 'fib-zone-deep';
      case 'Trend Damage':
        return 'fib-zone-damage';
      default:
        return '';
    }
  }

  protected fibStatusClass(status: string): string {
    switch (status) {
      case 'Reclaimed 61.8':
        return 'fib-status-reclaimed';
      case 'Testing 61.8':
        return 'fib-status-testing';
      case 'Above 61.8':
        return 'fib-status-above';
      case 'Below 61.8':
        return 'fib-status-below';
      case 'Below 78.6':
        return 'fib-status-damage';
      default:
        return '';
    }
  }

  protected readonly displayedColumns = inject(GridColumnService).getColumnKeys('watchlist');

  protected readonly filteredSorted = computed<WatchlistSummary[]>(() => {
    const filter = this.filterText().trim().toLowerCase();
    const filterTrendSetup = this.filterTrendSetup();
    const filterFinalAction = this.filterFinalAction();
    const filterFavorites = this.filterFavorites();
    const tier = this.tierFilter();
    let items = this.watchlist.items();

    if (tier !== 'All') {
      items = items.filter((w) => (w.item.watchlistTier ?? 'Strategic') === tier);
    }

    if (filterFavorites) {
      items = items.filter((w) => w.item.isFavorite);
    }

    if (filter) {
      items = items.filter(
        (w) =>
          w.item.symbol.toLowerCase().includes(filter) ||
          (w.quote?.companyName ?? '').toLowerCase().includes(filter) ||
          (w.quote?.sector ?? '').toLowerCase().includes(filter),
      );
    }

    if (filterTrendSetup) {
      items = items.filter(
        (w) =>
          (this.decisionForSymbol(w.item.symbol, w.item.role)?.trendSetup ?? '') ===
          filterTrendSetup,
      );
    }

    if (filterFinalAction) {
      items = items.filter(
        (w) =>
          (this.decisionForSymbol(w.item.symbol, w.item.role)?.finalAction ?? '') ===
          filterFinalAction,
      );
    }

    const col = this.sortCol();
    const dir = this.sortDir() === 'asc' ? 1 : -1;

    return [...items].sort((a, b) => {
      let av: string | number;
      let bv: string | number;
      switch (col) {
        case 'symbol':
          av = a.item.symbol;
          bv = b.item.symbol;
          break;
        case 'company':
          av = a.quote?.companyName ?? '';
          bv = b.quote?.companyName ?? '';
          break;
        case 'role':
          av = a.item.role ?? 'Strategic';
          bv = b.item.role ?? 'Strategic';
          break;
        case 'earningsDate':
          av = a.item.earningsDate ?? '';
          bv = b.item.earningsDate ?? '';
          break;
        case 'price':
          av = a.quote?.currentPrice ?? 0;
          bv = b.quote?.currentPrice ?? 0;
          break;
        case 'change':
          av = a.quote?.change ?? 0;
          bv = b.quote?.change ?? 0;
          break;
        case 'changePct':
          av = a.quote?.changePercent ?? 0;
          bv = b.quote?.changePercent ?? 0;
          break;
        case 'sector':
          av = a.quote?.sector ?? '';
          bv = b.quote?.sector ?? '';
          break;
        case 'rsi':
          av = this.rsiForSymbol(a.item.symbol) ?? -1;
          bv = this.rsiForSymbol(b.item.symbol) ?? -1;
          break;
        case 'buyScore':
          av = this.buyScoreForSymbol(a.item.symbol)?.score ?? -1;
          bv = this.buyScoreForSymbol(b.item.symbol)?.score ?? -1;
          break;
        case 'trendSetup':
          av = this.decisionForSymbol(a.item.symbol, a.item.role)?.trendSetup ?? '';
          bv = this.decisionForSymbol(b.item.symbol, b.item.role)?.trendSetup ?? '';
          break;
        case 'momentumShift':
          av = this.decisionForSymbol(a.item.symbol, a.item.role)?.momentumShift ?? '';
          bv = this.decisionForSymbol(b.item.symbol, b.item.role)?.momentumShift ?? '';
          break;
        case 'channel':
          av = this.channelSortValue(a.item.symbol);
          bv = this.channelSortValue(b.item.symbol);
          break;
        case 'priceStructure':
          av = this.priceStructureSortValue(a.item.symbol, a);
          bv = this.priceStructureSortValue(b.item.symbol, b);
          break;
        case 'finalAction':
          av = this.decisionForSymbol(a.item.symbol, a.item.role)?.finalAction ?? '';
          bv = this.decisionForSymbol(b.item.symbol, b.item.role)?.finalAction ?? '';
          break;
        case 'technical':
          av = this.valueDataForSymbol(a.item.symbol)?.technical ?? '';
          bv = this.valueDataForSymbol(b.item.symbol)?.technical ?? '';
          break;
        case 'valueScore':
          av = this.valueDataForSymbol(a.item.symbol)?.score ?? -1;
          bv = this.valueDataForSymbol(b.item.symbol)?.score ?? -1;
          break;
        case 'valueStatus':
          av = this.valueDataForSymbol(a.item.symbol)?.status ?? '';
          bv = this.valueDataForSymbol(b.item.symbol)?.status ?? '';
          break;
        case 'reversalP': {
          const order: Record<string, number> = { High: 3, Medium: 2, Low: 1 };
          av =
            order[this.rsiMap().get(a.item.symbol.toUpperCase())?.reversalProbability ?? ''] ?? 0;
          bv =
            order[this.rsiMap().get(b.item.symbol.toUpperCase())?.reversalProbability ?? ''] ?? 0;
          break;
        }
        case 'maStatus':
          av = this.maStatusForSymbol(a.item.symbol) === 'STRONG BUY' ? 1 : 0;
          bv = this.maStatusForSymbol(b.item.symbol) === 'STRONG BUY' ? 1 : 0;
          break;
        case 'fib38_2':
          av = this.fibForSymbol(a.item.symbol)?.fib38_2 ?? 0;
          bv = this.fibForSymbol(b.item.symbol)?.fib38_2 ?? 0;
          break;
        case 'fib50':
          av = this.fibForSymbol(a.item.symbol)?.fib50 ?? 0;
          bv = this.fibForSymbol(b.item.symbol)?.fib50 ?? 0;
          break;
        case 'fib61_8':
          av = this.fibForSymbol(a.item.symbol)?.fib61_8 ?? 0;
          bv = this.fibForSymbol(b.item.symbol)?.fib61_8 ?? 0;
          break;
        case 'fib78_6':
          av = this.fibForSymbol(a.item.symbol)?.fib78_6 ?? 0;
          bv = this.fibForSymbol(b.item.symbol)?.fib78_6 ?? 0;
          break;
        case 'fibZone':
          av = this.fibForSymbol(a.item.symbol)?.fibZone ?? '';
          bv = this.fibForSymbol(b.item.symbol)?.fibZone ?? '';
          break;
        case 'fibStatus':
          av = this.fibForSymbol(a.item.symbol)?.fibStatus ?? '';
          bv = this.fibForSymbol(b.item.symbol)?.fibStatus ?? '';
          break;
        case 'fibDist':
          av = this.fibForSymbol(a.item.symbol)?.distanceToFib61_8Pct ?? 0;
          bv = this.fibForSymbol(b.item.symbol)?.distanceToFib61_8Pct ?? 0;
          break;
        default:
          av = a.item.symbol;
          bv = b.item.symbol;
      }
      if (typeof av === 'number' && typeof bv === 'number') return (av - bv) * dir;
      return String(av).localeCompare(String(bv)) * dir;
    });
  });

  protected roleClass(role: string): string {
    switch (role) {
      case 'Core':
        return 'role-core';
      case 'Strategic':
        return 'role-strategic';
      case 'Strategic-Income':
        return 'role-strategic-income';
      case 'Swing':
        return 'role-swing';
      case 'Speculative':
        return 'role-speculative';
      case 'Options':
        return 'role-options';
      default:
        return 'role-strategic';
    }
  }

  protected readonly trendSetupOptions = computed<string[]>(() => {
    const set = new Set<string>();
    for (const w of this.watchlist.items()) {
      const ts = this.decisionForSymbol(w.item.symbol, w.item.role)?.trendSetup;
      if (ts) set.add(ts);
    }
    return [...set].sort();
  });

  protected readonly finalActionOptions = computed<string[]>(() => {
    const set = new Set<string>();
    for (const w of this.watchlist.items()) {
      const fa = this.decisionForSymbol(w.item.symbol, w.item.role)?.finalAction;
      if (fa) set.add(fa);
    }
    return [...set].sort();
  });

  setSort(col: SortColumn): void {
    if (this.sortCol() === col) {
      this.sortDir.update((d) => (d === 'asc' ? 'desc' : 'asc'));
    } else {
      this.sortCol.set(col);
      this.sortDir.set('asc');
    }
  }

  onMatSortChange(sort: Sort): void {
    if (!sort.active || sort.direction === '') return;
    this.sortCol.set(sort.active as SortColumn);
    this.sortDir.set(sort.direction as SortDir);
  }

  openAddDialog(): void {
    this.dialog
      .open(AddWatchlistDialogComponent, { width: '420px', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((result: AddWatchlistDialogResult | null) => {
        if (result) this.watchlist.addItem(result.symbol, result.role);
      });
  }

  refresh(): void {
    this.appRefresh.refreshAll();
    const symbols = this.watchlist.items().map((w) => w.item.symbol);
    if (symbols.length > 0) this.watchlistRsi.triggerRefresh(symbols);
  }

  remove(w: WatchlistSummary): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Remove Symbol',
          message: `Remove ${w.item.symbol} from your watchlist?`,
          confirmLabel: 'Remove',
          danger: true,
        },
        width: '360px',
      })
      .afterClosed()
      .subscribe((confirmed: boolean) => {
        if (confirmed) this.watchlist.deleteItem(w.item.id, w.item.symbol);
      });
  }

  updateRole(w: WatchlistSummary, role: string): void {
    this.watchlist.updateRole(w.item.id, role);
  }

  protected readonly activeTierCount = computed(
    () =>
      this.watchlist.items().filter((w) => (w.item.watchlistTier ?? 'Strategic') === 'Active')
        .length,
  );

  updateTier(w: WatchlistSummary, tier: string): void {
    this.watchlist.updateTier(w.item.id, tier);
  }

  protected readonly earningsRefreshing = signal(false);

  refreshEarningsFromYahoo(): void {
    this.earningsRefreshing.set(true);
    this.api.refreshWatchlistEarnings().subscribe({
      next: (r) => {
        this.earningsRefreshing.set(false);
        this.watchlist.refresh();
        const snack = (this as any).snackBar;
        if (snack)
          snack.open(`Updated earnings dates for ${r.refreshed} of ${r.total} symbols.`, 'OK', {
            duration: 5000,
          });
      },
      error: () => this.earningsRefreshing.set(false),
    });
  }

  updateEarningsDate(w: WatchlistSummary, value: string): void {
    this.watchlist.updateEarningsDate(w.item.id, value || null);
  }

  exportToExcel(): void {
    const today = new Date().toISOString().slice(0, 10);
    const data = this.filteredSorted().map((w) => {
      const dec = this.decisionForSymbol(w.item.symbol, w.item.role);
      const r = this.rsiMap().get(w.item.symbol.toUpperCase());

      // CloseLocation: where the close sits within the daily range (0–1)
      let closeLocation: number | string = '';
      if (r && r.dayHigh > 0 && r.dayLow >= 0) {
        const range = r.dayHigh - r.dayLow;
        closeLocation = range > 0 ? +((r.currentPrice - r.dayLow) / range).toFixed(4) : 0.5;
      }

      const prevMacdHist = r ? +(r.macdHistogram - r.macdHistDelta).toFixed(4) : '';

      return {
        Symbol: w.item.symbol,
        'Earnings Date': w.item.earningsDate ?? '',
        Company: w.quote?.companyName ?? '',
        Role: w.item.role ?? 'Strategic',
        Price: w.quote?.currentPrice ?? '',
        Change: w.quote?.change ?? '',
        'Change %': w.quote?.changePercent != null ? +w.quote.changePercent.toFixed(2) : '',
        Sector: w.quote?.sector ?? '',
        Industry: w.quote?.industry ?? '',
        'RSI (14)': this.rsiForSymbol(w.item.symbol) ?? '',
        'Trend Setup': dec?.trendSetup ?? '',
        'Momentum Shift': dec?.momentumShift ?? '',
        'Price Structure': this.priceStructureLabel(w.item.symbol, w),
        'Base Action': dec?.baseAction ?? '',
        'Final Action': dec?.finalAction ?? '',
        'Hover Note': dec?.hoverDescription ?? '',
        EMA9: r?.ema9Price ?? '',
        EMA10: r?.ema10Price ?? '',
        EMA20: r?.ema20Price ?? '',
        SMA20: r?.sma20Price ?? '',
        SMA50: r?.sma50Price ?? '',
        RSI9EMA: r?.rsiSignal ?? '',
        VolumeRatio20: r?.volumeRatio ?? '',
        CloseLocation: closeLocation,
        TopHalfClose: r
          ? r.dayHigh > 0 && r.dayLow >= 0 && r.dayHigh - r.dayLow > 0
            ? (r.currentPrice - r.dayLow) / (r.dayHigh - r.dayLow) >= 0.5
            : false
          : '',
        BottomHalfClose: r
          ? r.dayHigh > 0 && r.dayLow >= 0 && r.dayHigh - r.dayLow > 0
            ? (r.currentPrice - r.dayLow) / (r.dayHigh - r.dayLow) < 0.5
            : false
          : '',
        MACDHistogram: r?.macdHistogram ?? '',
        PrevMACDHistogram: prevMacdHist,
        Technical: this.valueDataForSymbol(w.item.symbol)?.technical ?? '',
        'Buy Score': this.buyScoreForSymbol(w.item.symbol)?.score ?? '',
        'Value Score': this.valueDataForSymbol(w.item.symbol)?.score ?? '',
      };
    });
    const ws = XLSX.utils.json_to_sheet(data);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Watchlist');
    XLSX.writeFile(wb, `watchlist-${today}.xlsx`);
  }

  backupWatchlist(): void {
    this.api.backupWatchlist().subscribe({
      next: (items) => {
        const backup = {
          exportedAt: new Date().toISOString(),
          type: 'watchlist',
          items,
        };
        const blob = new Blob([JSON.stringify(backup, null, 2)], {
          type: 'application/json;charset=utf-8;',
        });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `watchlist-backup-${new Date().toISOString().slice(0, 10)}.json`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => console.error('[Watchlist] Backup failed'),
    });
  }

  onRestoreFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    input.value = '';

    const reader = new FileReader();
    reader.onload = (e) => {
      try {
        const backup = JSON.parse(e.target?.result as string);
        if (backup.type !== 'watchlist') {
          console.error('[Watchlist] Invalid backup type:', backup.type);
          return;
        }
        const confirmed = window.confirm(
          `This will REPLACE all ${this.watchlist.count()} watchlist items with the backup from ${backup.exportedAt?.slice(0, 10) ?? 'unknown date'} (${backup.items?.length ?? 0} items). Continue?`,
        );
        if (!confirmed) return;

        this.api.restoreWatchlist({ items: backup.items ?? [] }).subscribe({
          next: () => {
            this.watchlist.refresh();
          },
          error: () => console.error('[Watchlist] Restore failed'),
        });
      } catch {
        console.error('[Watchlist] Invalid backup file');
      }
    };
    reader.readAsText(file);
  }

  toggleFavorite(w: WatchlistSummary): void {
    this.watchlist.updateFavorite(w.item.id, !w.item.isFavorite);
  }

  openNotes(w: WatchlistSummary): void {
    this.dialog
      .open(TransactionNotesDialogComponent, {
        data: { symbol: w.item.symbol, notes: w.item.notes } satisfies TransactionNotesDialogData,
        width: '480px',
      })
      .afterClosed()
      .subscribe((result: TransactionNotesDialogResult | undefined) => {
        if (result === undefined) return;
        this.watchlist.updateNotes(w.item.id, result.notes ?? '');
      });
  }

  // ── Column resize ───────────────────────────────────────────────────────────
  private static readonly COL_WIDTHS_KEY = 'wl_col_widths_v1';

  protected readonly colWidths = signal<Map<string, number>>(
    WatchlistPageComponent.loadColWidths(),
  );

  private static loadColWidths(): Map<string, number> {
    try {
      const raw = localStorage.getItem(WatchlistPageComponent.COL_WIDTHS_KEY);
      if (raw) return new Map<string, number>(JSON.parse(raw));
    } catch {
      /* ignore */
    }
    return new Map();
  }

  /** Returns width as CSS string for use in colgroup <col> elements */
  protected colWidthStyle(col: string): string {
    const w = this.colWidths().get(col);
    return w ? `${w}px` : '';
  }

  protected startResize(event: MouseEvent, col: string): void {
    event.preventDefault();
    event.stopPropagation();
    const th = (event.target as HTMLElement).closest('th') as HTMLElement;
    const startX = event.clientX;
    const startWidth = th.offsetWidth;

    const onMove = (e: MouseEvent) => {
      const newWidth = Math.max(50, startWidth + (e.clientX - startX));
      this.colWidths.update((m) => {
        const copy = new Map(m);
        copy.set(col, newWidth);
        return copy;
      });
    };

    const onUp = () => {
      document.removeEventListener('mousemove', onMove);
      document.removeEventListener('mouseup', onUp);
      try {
        localStorage.setItem(
          WatchlistPageComponent.COL_WIDTHS_KEY,
          JSON.stringify([...this.colWidths()]),
        );
      } catch {
        /* ignore */
      }
    };

    document.addEventListener('mousemove', onMove);
    document.addEventListener('mouseup', onUp);
  }

  resetColWidths(): void {
    this.colWidths.set(new Map());
    try {
      localStorage.removeItem(WatchlistPageComponent.COL_WIDTHS_KEY);
    } catch {
      /* ignore */
    }
  }
}
