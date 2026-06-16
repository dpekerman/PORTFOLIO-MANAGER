import { CurrencyPipe, DecimalPipe, NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { LogicMode, RsiScanResult, ScanType } from '../../core/models/portfolio.models';

@Component({
  selector: 'app-rsi-scanner-table',
  templateUrl: './rsi-scanner-table.component.html',
  styleUrl: './rsi-scanner-table.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatTableModule,
    MatIconModule,
    MatTooltipModule,
    MatChipsModule,
    MatProgressBarModule,
    DecimalPipe,
    CurrencyPipe,
    NgClass,
  ],
})
export class RsiScannerTableComponent {
  readonly results = input.required<RsiScanResult[]>();
  readonly scanType = input.required<ScanType>();
  readonly logicMode = input<LogicMode>('Legacy');
  readonly loading = input(false);
  /** Set of symbols currently in the portfolio (lowercase for lookup). */
  readonly portfolioSymbols = input<ReadonlySet<string>>(new Set());
  /** Set of symbols currently on the watchlist (lowercase for lookup). */
  readonly watchlistSymbols = input<ReadonlySet<string>>(new Set());

  protected readonly displayedColumns = [
    'tracking',
    'symbol',
    'rsi',
    'rsiSignal',
    'price',
    'change',
    'analystUpside',
    'indicators',
    'status',
    'trigger',
  ];

  protected readonly isOversold = computed(() => this.scanType() === 'Oversold');
  protected readonly isNeutral = computed(() => this.scanType() === 'Neutral');
  protected readonly isEnhanced = computed(() => this.logicMode() === 'Enhanced');

  /** Returns "Portfolio" | "Watchlist" | "Market" for a given symbol. */
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
    const base = `MACD Histogram Momentum · Hist=${row.macdHistogram.toFixed(4)} · Δ=${row.macdHistDelta >= 0 ? '+' : ''}${row.macdHistDelta.toFixed(4)} · Slope: ${row.macdHistSlope}`;
    if (row.macdHistogram < 0 && row.macdHistSlope === 'Rising') {
      return base + ' ↑ Negative bars shrinking → momentum shift BEFORE crossover';
    }
    if (row.macdHistogram > 0 && row.macdHistSlope === 'Falling') {
      return base + ' ↓ Positive bars shrinking → distribution BEFORE crossover';
    }
    return base;
  }

  protected volSignalClass(sig: string): string {
    if (sig === 'Validated') return 'ind-bull';
    if (sig === 'Low-Volume Trap') return 'ind-warn';
    return 'ind-neutral';
  }

  protected volSignalIcon(sig: string): string {
    if (sig === 'Validated') return 'volume_up';
    if (sig === 'Low-Volume Trap') return 'volume_off';
    return 'volume_mute';
  }

  protected dmaTooltip(row: RsiScanResult): string {
    const d50 = row.dma50Deviation.toFixed(1);
    const sign = row.dma50Deviation >= 0 ? '+' : '';
    if (!row.has200Dma) return `50 DMA: ${sign}${d50}% · 200 DMA: N/A`;
    const d200 = row.dma200Deviation.toFixed(1);
    const s200 = row.dma200Deviation >= 0 ? '+' : '';
    return `50 DMA: ${sign}${d50}%  |  200 DMA: ${s200}${d200}%`;
  }

  protected probClass(prob: string): string {
    if (prob === 'High') return 'prob-high';
    if (prob === 'Medium') return 'prob-medium';
    return 'prob-low';
  }
}
