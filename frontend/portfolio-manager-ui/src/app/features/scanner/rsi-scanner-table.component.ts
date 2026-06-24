import { CurrencyPipe, DecimalPipe, NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import * as XLSX from 'xlsx';
import { LogicMode, RsiScanResult, ScanType } from '../../core/models/portfolio.models';
import { DecisionEngineService, PageDecision } from '../../core/services/decision-engine.service';

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
  readonly portfolioSymbols = input<ReadonlySet<string>>(new Set());
  readonly watchlistSymbols = input<ReadonlySet<string>>(new Set());

  private readonly engine = inject(DecisionEngineService);

  protected readonly displayedColumns = [
    'tracking',
    'symbol',
    'rsi',
    'rsiSignal',
    'price',
    'change',
    'analystUpside',
    'probability',
    'trendSetup',
    'momentumShift',
    'baseAction',
    'status',
    'trigger',
  ];

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
    if (!row.has200Dma) return `50 DMA: ${sign}${d50}% | 200 DMA: N/A`;
    const d200 = row.dma200Deviation.toFixed(1);
    const s200 = row.dma200Deviation >= 0 ? '+' : '';
    return `50 DMA: ${sign}${d50}%  |  200 DMA: ${s200}${d200}%`;
  }

  protected probClass(prob: string): string {
    if (prob === 'High') return 'prob-high';
    if (prob === 'Medium') return 'prob-medium';
    return 'prob-low';
  }

  protected decision(row: RsiScanResult): PageDecision {
    return this.engine.translateForRsiScanner(row);
  }

  exportToExcel(): void {
    const today = new Date().toISOString().slice(0, 10);
    const scanLabel = this.scanType();
    const data = this.results().map((r) => {
      const dec = this.decision(r);
      return {
        Symbol: r.symbol,
        Price: r.currentPrice,
        'Change %': r.changePercent != null ? +r.changePercent.toFixed(2) : '',
        'RSI (14)': r.rsi != null ? +r.rsi.toFixed(2) : '',
        'RSI Signal': r.rsiSignal != null ? +r.rsiSignal.toFixed(2) : '',
        'Vol Ratio': r.volumeRatio != null ? +r.volumeRatio.toFixed(2) : '',
        EMA9: r.ema9Price ?? '',
        EMA10: r.ema10Price ?? '',
        EMA20: r.ema20Price ?? '',
        SMA20: r.sma20Price ?? '',
        SMA50: r.sma50Price ?? '',
        'MACD Hist': r.macdHistogram != null ? +r.macdHistogram.toFixed(4) : '',
        'MACD Delta': r.macdHistDelta != null ? +r.macdHistDelta.toFixed(4) : '',
        'Day High': r.dayHigh ?? '',
        'Day Low': r.dayLow ?? '',
        Status: r.status,
        'Scan Type': r.scanType,
        'Trend Setup': dec.trendSetup,
        'Momentum Shift': dec.momentumShift,
        'Base Action': dec.baseAction,
      };
    });
    const ws = XLSX.utils.json_to_sheet(data);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, scanLabel);
    XLSX.writeFile(wb, `rsi-scanner-${scanLabel.toLowerCase()}-${today}.xlsx`);
  }
}
