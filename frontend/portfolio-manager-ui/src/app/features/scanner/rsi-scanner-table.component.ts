import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import * as XLSX from 'xlsx';
import { LogicMode, RsiScanResult, ScanType } from '../../core/models/portfolio.models';
import {
  DecisionEngineService,
  GapStatus,
  PageDecision,
} from '../../core/services/decision-engine.service';
import { GridColumnService } from '../../core/services/grid-column.service';
import { GridColumnButtonComponent } from '../../shared/column-config-dialog/grid-column-btn.component';

@Component({
  selector: 'app-rsi-scanner-table',
  templateUrl: './rsi-scanner-table.component.html',
  styleUrl: './rsi-scanner-table.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatChipsModule,
    MatProgressBarModule,
    MatSortModule,
    RouterLink,
    DecimalPipe,
    CurrencyPipe,
    GridColumnButtonComponent,
  ],
})
export class RsiScannerTableComponent {
  readonly results = input.required<RsiScanResult[]>();
  readonly scanType = input.required<ScanType>();
  readonly logicMode = input<LogicMode>('Legacy');
  readonly loading = input(false);
  readonly portfolioSymbols = input<ReadonlySet<string>>(new Set());
  readonly watchlistSymbols = input<ReadonlySet<string>>(new Set());
  readonly showHistory = input(true);

  protected readonly sortCol = signal<string>('momentumShift');
  protected readonly sortDir = signal<'asc' | 'desc'>('asc');

  /** Sort priority for TrendShift: Bull/Bear Turns first, then Stabilizing, then Still Falling/Rising, then Waiting. */
  private trendShiftPriority(shift: string): number {
    if (shift.includes('Bull Turn') || shift.includes('Bear Turn')) return 0;
    if (shift.includes('Stabilizing')) return 1;
    if (shift.includes('Still')) return 2;
    return 3; // Waiting / empty
  }

  protected readonly sortedResults = computed(() => {
    const col = this.sortCol();
    const dir = this.sortDir() === 'asc' ? 1 : -1;
    return [...this.results()].sort((a, b) => {
      let av: number | string;
      let bv: number | string;
      switch (col) {
        case 'momentumShift':
          av = this.trendShiftPriority(a.trendShift ?? '');
          bv = this.trendShiftPriority(b.trendShift ?? '');
          if (av !== bv) return (av - bv) * dir;
          return a.symbol.localeCompare(b.symbol);
        case 'symbol':
          av = a.symbol;
          bv = b.symbol;
          break;
        case 'rsi':
          av = a.rsi;
          bv = b.rsi;
          break;
        case 'rsiDelta1D':
          av = a.rsiDelta1D ?? 0;
          bv = b.rsiDelta1D ?? 0;
          break;
        case 'price':
          av = a.currentPrice;
          bv = b.currentPrice;
          break;
        case 'probability':
          av = a.reversalProbability === 'High' ? 2 : a.reversalProbability === 'Medium' ? 1 : 0;
          bv = b.reversalProbability === 'High' ? 2 : b.reversalProbability === 'Medium' ? 1 : 0;
          break;
        case 'analystUpside':
          av = a.analystTargetUpside ?? 0;
          bv = b.analystTargetUpside ?? 0;
          break;
        default:
          return 0;
      }
      if (typeof av === 'number' && typeof bv === 'number') return (av - bv) * dir;
      return String(av).localeCompare(String(bv)) * dir;
    });
  });

  protected onSortChange(sort: Sort): void {
    this.sortCol.set(sort.active || 'momentumShift');
    this.sortDir.set((sort.direction as 'asc' | 'desc') || 'asc');
  }

  private readonly engine = inject(DecisionEngineService);
  private readonly _serviceColumns = inject(GridColumnService).getColumnKeys('scanner');

  /** Effective displayed columns: service order/visibility, minus signalHistory when showHistory=false. */
  protected readonly displayedColumns = computed(() => {
    const cols = this._serviceColumns();
    if (!this.showHistory()) {
      return cols.filter((c) => c !== 'signalHistory');
    }
    return cols;
  });

  protected readonly isOversold = computed(() => this.scanType() === 'Oversold');
  protected readonly isNeutral = computed(() => this.scanType() === 'Neutral');
  protected readonly isEnhanced = computed(() => this.logicMode() === 'Enhanced');

  protected trackingStatus(symbol: string): 'Portfolio' | 'Watchlist' | 'Market' {
    const s = symbol.toLowerCase();
    if (this.portfolioSymbols().has(s)) return 'Portfolio';
    if (this.watchlistSymbols().has(s)) return 'Watchlist';
    return 'Market';
  }

  protected trackingClass(status: 'Portfolio' | 'Watchlist' | 'Market'): string {
    if (status === 'Portfolio') return 'track-portfolio';
    if (status === 'Watchlist') return 'track-watchlist';
    return 'track-market';
  }

  protected trackingIcon(status: 'Portfolio' | 'Watchlist' | 'Market'): string {
    if (status === 'Portfolio') return 'account_balance_wallet';
    if (status === 'Watchlist') return 'visibility';
    return 'public';
  }

  protected analystUpsideClass(upside: number): string {
    if (upside > 10) return 'upside-pos';
    if (upside < -5) return 'upside-neg';
    return 'upside-neutral';
  }

  protected rsiBarColor(rsi: number, type: ScanType): string {
    if (type === 'Oversold') return rsi < 25 ? '#d32f2f' : '#f57c00';
    if (type === 'Overbought') return rsi > 80 ? '#d32f2f' : '#f57c00';
    return '#757575';
  }

  protected rsiBarValue(rsi: number, type: ScanType): number {
    if (type === 'Oversold') return (rsi / 30) * 100;
    if (type === 'Overbought') return ((rsi - 75) / 25) * 100;
    return rsi;
  }

  protected macdIcon(crossover: string): string {
    if (crossover === 'Bullish') return 'trending_up';
    if (crossover === 'Bearish') return 'trending_down';
    return 'trending_flat';
  }

  protected macdClass(crossover: string): string {
    if (crossover === 'Bullish') return 'ind-bull';
    if (crossover === 'Bearish') return 'ind-bear';
    return 'ind-neutral';
  }

  protected histSlopeIcon(slope: string): string {
    if (slope === 'Rising') return 'trending_up';
    if (slope === 'Falling') return 'trending_down';
    return 'trending_flat';
  }

  protected histSlopeClass(slope: string, scanType: ScanType): string {
    if (scanType === 'Oversold' && slope === 'Rising') return 'ind-bull';
    if (scanType === 'Overbought' && slope === 'Falling') return 'ind-bear';
    if (slope !== 'Neutral') return 'ind-warn';
    return 'ind-neutral';
  }

  protected histSlopeTooltip(row: RsiScanResult): string {
    const base = `MACD Histogram Momentum | Hist=${row.macdHistogram.toFixed(4)} | Delta=${row.macdHistDelta >= 0 ? '+' : ''}${row.macdHistDelta.toFixed(4)} | Slope: ${row.macdHistSlope}`;
    if (row.macdHistogram < 0 && row.macdHistSlope === 'Rising') {
      return base + ' - Negative bars shrinking, momentum shift BEFORE crossover';
    }
    if (row.macdHistogram > 0 && row.macdHistSlope === 'Falling') {
      return base + ' - Positive bars shrinking, distribution BEFORE crossover';
    }
    return base;
  }

  protected volSignalClass(sig: string): string {
    if (sig === 'Validated') return 'ind-bull';
    if (sig === 'Elevated') return 'ind-elevated';
    if (sig === 'Low-Volume Trap') return 'ind-warn';
    return 'ind-neutral';
  }

  protected volSignalIcon(sig: string): string {
    if (sig === 'Validated') return 'volume_up';
    if (sig === 'Elevated') return 'volume_down';
    if (sig === 'Low-Volume Trap') return 'volume_off';
    return 'volume_mute';
  }

  protected dmaTooltip(row: RsiScanResult): string {
    const d50 = row.dma50Deviation.toFixed(1);
    const sign = row.dma50Deviation >= 0 ? '+' : '';
    if (!row.has200Dma) return `50 DMA: ${sign}${d50}% | 200 DMA: N/A`;
    const d200 = row.dma200Deviation.toFixed(1);
    const s200 = row.dma200Deviation >= 0 ? '+' : '';
    return `50 DMA: ${sign}${d50}%  |  200 DMA: ${s200}${d200}%`;
  }

  // ── Trend Shift (day-over-day RSI momentum) ────────────────────────────────
  protected trendShiftClass(trendShift: string): string {
    if (trendShift.includes('Bull Turn') || trendShift.includes('Bear Turn')) return 'trend-bull';
    if (trendShift.includes('Still Falling') || trendShift.includes('Still Rising'))
      return 'trend-bear';
    if (trendShift.includes('Stabilizing')) return 'trend-neutral';
    return 'trend-waiting';
  }

  /** Display label combining TrendShift with Turn Strength suffix. */
  protected trendShiftDisplay(row: RsiScanResult): string {
    const shift = row.trendShift;
    const strength = row.turnStrength;
    if (!shift || shift === 'Waiting') return shift || 'Waiting';
    if (!strength || strength === 'Normal') return shift;
    return `${shift} — ${strength}`;
  }

  protected stageStatusClass(status: string): string {
    if (status === 'CONFIRMING') return 'stage-confirming';
    if (status === 'TRACKING') return 'stage-tracking';
    if (status === 'STAGED') return 'stage-staged';
    return '';
  }

  protected rsiDeltaIcon(delta: number | null): string {
    if (delta === null) return '→';
    if (delta > 0.05) return '↑';
    if (delta < -0.05) return '↓';
    return '→';
  }

  protected rsiDeltaClass(delta: number | null, scanType: ScanType): string {
    if (delta === null) return 'delta-neutral';
    if (scanType === 'Oversold') {
      return delta > 0.25 ? 'delta-positive' : delta < -0.25 ? 'delta-negative' : 'delta-neutral';
    }
    return delta < -0.25 ? 'delta-positive' : delta > 0.25 ? 'delta-negative' : 'delta-neutral';
  }

  protected sma200Label(row: RsiScanResult): string {
    if (!row.sma200 || row.sma200 <= 0) return '—';
    const diff = row.dma200Deviation;
    const sign = diff >= 0 ? '+' : '';
    const aboveBelow = diff >= 0 ? 'ABOVE' : 'BELOW';
    return `$${row.sma200.toFixed(2)} · ${sign}${diff.toFixed(1)}% ${aboveBelow}`;
  }

  protected sma200Class(row: RsiScanResult): string {
    if (!row.sma200 || row.sma200 <= 0) return '';
    return row.dma200Deviation >= 0 ? 'sma200-above' : 'sma200-below';
  }

  protected trendSetup200Class(setup: string): string {
    if (setup === 'Trend-Aligned') return 'setup-aligned';
    if (setup === 'Counter-Trend') return 'setup-counter';
    return '';
  }

  protected probClass(prob: string): string {
    if (prob === 'High') return 'prob-high';
    if (prob === 'Medium') return 'prob-medium';
    return 'prob-low';
  }

  protected decision(row: RsiScanResult): PageDecision {
    return this.engine.translateForRsiScanner(row);
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

  protected fibZoneClass(zone: string): string {
    switch (zone) {
      case 'Value Zone':      return 'fib-zone-value';
      case 'Key Fib Support': return 'fib-zone-key';
      case 'Shallow Pullback':return 'fib-zone-shallow';
      case 'Normal Pullback': return 'fib-zone-normal';
      case 'Deep Pullback':   return 'fib-zone-deep';
      case 'Trend Damage':    return 'fib-zone-damage';
      default:                return '';
    }
  }

  protected fibStatusClass(status: string): string {
    switch (status) {
      case 'Reclaimed 61.8':  return 'fib-status-reclaimed';
      case 'Testing 61.8':    return 'fib-status-testing';
      case 'Above 61.8':      return 'fib-status-above';
      case 'Below 61.8':      return 'fib-status-below';
      case 'Below 78.6':      return 'fib-status-damage';
      default:                return '';
    }
  }

  exportToExcel(): void {
    const today = new Date().toISOString().slice(0, 10);
    const scanLabel = this.scanType();
    const data = this.results().map((r) => {
      const dec = this.decision(r);
      const ema9Confirmed =
        r.scanType === 'Oversold' ? r.currentPrice > r.ema9Price : r.currentPrice < r.ema9Price;
      const eodPriceConfirmed =
        r.dailyAtr > 0 &&
        (r.scanType === 'Oversold'
          ? r.currentPrice > r.openPrice && r.currentPrice >= r.dayHigh - 0.25 * r.dailyAtr
          : r.currentPrice < r.openPrice && r.currentPrice <= r.dayLow + 0.25 * r.dailyAtr);
      const promotionReady =
        r.rsiDelta1D !== null &&
        (r.trendShift.includes('Bull Turn') || r.trendShift.includes('Bear Turn')) &&
        r.volumeRatio >= 1.5 &&
        eodPriceConfirmed;
      return {
        Symbol: r.symbol,
        ScanType: r.scanType,
        BaseRsi: r.rsi != null ? +r.rsi.toFixed(2) : '',
        PreviousRsi: '',
        CurrentRsi: r.rsi != null ? +r.rsi.toFixed(2) : '',
        'RSI Delta1D': r.rsiDelta1D != null ? +r.rsiDelta1D.toFixed(2) : '',
        TrendShift: r.trendShift,
        TurnStrength: r.turnStrength,
        StageStatus: r.stageStatus,
        CurrentPrice: r.currentPrice,
        EMA9: r.ema9Price ?? '',
        Ema9Confirmed: ema9Confirmed,
        VolumeRatio: r.volumeRatio != null ? +r.volumeRatio.toFixed(2) : '',
        VolumeConfirmationPassed: r.volumeRatio >= 1.5,
        EodPriceConfirmationPassed: eodPriceConfirmed,
        TurnPassed:
          r.rsiDelta1D !== null &&
          (r.trendShift.includes('Bull Turn') || r.trendShift.includes('Bear Turn')),
        PromotionReady: promotionReady,
        BlockingReason: promotionReady
          ? 'None'
          : !(
                r.rsiDelta1D !== null &&
                (r.trendShift.includes('Bull Turn') || r.trendShift.includes('Bear Turn'))
              )
            ? r.rsiDelta1D === null
              ? 'Waiting for Day-2 RSI'
              : 'No Bull/Bear Turn'
            : r.volumeRatio < 1.5
              ? 'Low Volume'
              : !eodPriceConfirmed
                ? 'EOD Price Confirmation Failed'
                : 'None',
        SMA200: r.sma200 ?? '',
        TrendSetup: dec.trendSetup,
        StopLoss: r.dynamicStopLoss ?? '',
        StagedDate: '',
        LastEvaluatedDate: '',
        IsActiveWatch: r.isTracked,
        'Legacy Status': r.status,
        'Legacy Action': dec.baseAction,
      };
    });
    const ws = XLSX.utils.json_to_sheet(data);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, scanLabel);
    XLSX.writeFile(wb, `rsi-scanner-${scanLabel.toLowerCase()}-${today}.xlsx`);
  }
}
