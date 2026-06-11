import { CurrencyPipe, DecimalPipe, NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RsiScanResult, ScanType } from '../../core/models/portfolio.models';

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
  readonly loading = input(false);

  protected readonly displayedColumns = [
    'symbol',
    'rsi',
    'price',
    'change',
    'indicators',
    'probability',
    'status',
    'trigger',
  ];

  protected readonly isOversold = computed(() => this.scanType() === 'Oversold');

  protected rsiBarColor(rsi: number, type: ScanType): string {
    if (type === 'Oversold') return rsi < 25 ? '#d32f2f' : '#f57c00';
    return rsi > 80 ? '#d32f2f' : '#f57c00';
  }

  protected rsiBarValue(rsi: number, type: ScanType): number {
    if (type === 'Oversold') return (rsi / 30) * 100;
    return ((rsi - 75) / 25) * 100;
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
