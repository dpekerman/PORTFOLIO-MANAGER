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
    'momentumShift',
    'momentumAction',
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

  // ── Momentum Shift Engine ──────────────────────────────────────────────────
  // Extreme RSI zones (rsi < 30 / rsi > 65) keep the existing rich signals.
  // Neutral zone (30–65) checks new price-action rules first, then RSI-only fallback.
  //
  // New rules (in priority order within neutral zone):
  //   Rule 1 — Uptrend:       Price > 9-EMA  AND  RSI 50–65
  //   Rule 2 — Consolidation: Price ≈ 20-SMA (±2%)  AND  RSI 40–50  AND  Vol > 1.2×
  //   Rule 3 — Breakdown:     Price < 9-EMA  AND  RSI < 40

  protected momentumShift(row: RsiScanResult): string {
    const rsi = row.rsi;
    const signal = row.rsiSignal ?? rsi;
    const price = row.currentPrice;
    const ema9 = row.ema9Price ?? 0;
    const sma20 = row.sma20Price ?? 0;
    const vol = row.volumeRatio ?? 1;

    // Extreme RSI zones — keep existing rich signal classification
    if (rsi > 65) {
      if (row.status === 'Confirmed') return 'Active SELL Trigger';
      if (row.rsiSignalAvailable && rsi <= signal) return 'Bearish Shift';
      return 'Warning';
    }
    if (rsi < 30) {
      if (row.status === 'Confirmed') return 'Active BUY Trigger';
      if (row.rsiSignalAvailable && rsi >= signal) return 'Bullish Shift';
      return 'Warning';
    }

    // Neutral zone: new price-action rules (priority over RSI-only fallback)
    if (ema9 > 0 && price > ema9 && rsi >= 50 && rsi <= 65) return 'Uptrend';
    if (sma20 > 0 && Math.abs(price - sma20) / sma20 <= 0.02 && rsi >= 40 && rsi <= 50 && vol > 1.2)
      return 'Consolidation';
    if (ema9 > 0 && price < ema9 && rsi < 40) return 'Breakdown';

    // RSI-only fallback
    if (rsi >= 55) return 'Uptrend';
    if (rsi >= 45) return 'Neutral';
    return 'Downtrend';
  }

  protected momentumShiftTooltip(row: RsiScanResult): string {
    const rsi = row.rsi;
    const signal = row.rsiSignal ?? rsi;
    const price = row.currentPrice;
    const ema9 = row.ema9Price ?? 0;
    const sma20 = row.sma20Price ?? 0;
    const vol = row.volumeRatio ?? 1;

    if (rsi > 65) {
      if (row.status === 'Confirmed') return 'Sellers have officially taken control of the day.';
      if (row.rsiSignalAvailable && rsi <= signal)
        return 'The buying frenzy is starting to run out of steam.';
      return 'The stock is surging upward rapidly and is heavily overbought.';
    }
    if (rsi < 30) {
      if (row.status === 'Confirmed') return 'Buyers have officially taken control of the day.';
      if (row.rsiSignalAvailable && rsi >= signal)
        return 'The selling speed has broken, but we need candle confirmation.';
      return 'The waterfall drop is still active. Do not try to catch the knife yet.';
    }

    if (ema9 > 0 && price > ema9 && rsi >= 50 && rsi <= 65)
      return 'Price above 9-EMA with healthy RSI — trend is intact, do nothing.';
    if (sma20 > 0 && Math.abs(price - sma20) / sma20 <= 0.02 && rsi >= 40 && rsi <= 50 && vol > 1.2)
      return 'Price holding near 20-SMA with elevated volume — institutional dip-buy confirmed.';
    if (ema9 > 0 && price < ema9 && rsi < 40)
      return 'Price broke below 9-EMA with RSI fading — defensive risk triggered.';

    if (rsi >= 55) return 'Gentle Uptrend. No exhaustion in sight. Let the trend run.';
    if (rsi >= 45) return 'Equilibrium Chop, keep hands off Options.';
    return 'Gentle Downtrend. Asset is gently bleeding lower due to a lack of buyers.';
  }

  protected momentumShiftClass(row: RsiScanResult): string {
    const shift = this.momentumShift(row);
    switch (shift) {
      case 'Active BUY Trigger':
        return 'ms-confirmed-buy';
      case 'Bullish Shift':
        return 'ms-bullish';
      case 'Active SELL Trigger':
        return 'ms-confirmed-sell';
      case 'Bearish Shift':
        return 'ms-bearish';
      case 'Warning':
        return 'ms-warning';
      case 'Uptrend':
        return 'ms-uptrend';
      case 'Consolidation':
        return 'ms-consolidation';
      case 'Breakdown':
        return 'ms-breakdown';
      case 'Downtrend':
        return 'ms-downtrend';
      default:
        return 'ms-neutral';
    }
  }

  protected momentumAction(row: RsiScanResult): string {
    const rsi = row.rsi;
    const signal = row.rsiSignal ?? rsi;
    const price = row.currentPrice;
    const ema9 = row.ema9Price ?? 0;
    const sma20 = row.sma20Price ?? 0;
    const vol = row.volumeRatio ?? 1;

    if (rsi > 65) {
      if (row.status === 'Confirmed') return 'CONFIRMED SELL SIGNAL';
      if (row.rsiSignalAvailable && rsi <= signal) return 'EARLY WARNING';
      return 'AVOID / WAIT';
    }
    if (rsi < 30) {
      if (row.status === 'Confirmed') return 'CONFIRMED BUY SIGNAL';
      if (row.rsiSignalAvailable && rsi >= signal) return 'EARLY WARNING';
      return 'AVOID / WAIT';
    }

    if (ema9 > 0 && price > ema9 && rsi >= 50 && rsi <= 65) return 'HOLD LONGS';
    if (sma20 > 0 && Math.abs(price - sma20) / sma20 <= 0.02 && rsi >= 40 && rsi <= 50 && vol > 1.2)
      return 'BUY / ACCUMULATE';
    if (ema9 > 0 && price < ema9 && rsi < 40) return 'REDUCE';

    if (rsi >= 55) return 'HOLD LONGS';
    if (rsi >= 45) return 'STAND BY / HANDS OFF';
    return 'STAND BY';
  }

  protected momentumActionTooltip(row: RsiScanResult): string {
    const rsi = row.rsi;
    const signal = row.rsiSignal ?? rsi;
    const price = row.currentPrice;
    const ema9 = row.ema9Price ?? 0;
    const sma20 = row.sma20Price ?? 0;
    const vol = row.volumeRatio ?? 1;

    if (rsi > 65) {
      if (row.status === 'Confirmed') return 'High-probability short or put entry.';
      if (row.rsiSignalAvailable && rsi <= signal)
        return 'Get ready to short or buy puts. The buying speed has broken.';
      return 'The stock is running hot and squeezing shorts. Do not stand in front of the train.';
    }
    if (rsi < 30) {
      if (row.status === 'Confirmed') return 'High-probability long entry.';
      if (row.rsiSignalAvailable && rsi >= signal)
        return 'Get ready to buy. The selling speed has broken, but we need candle confirmation.';
      return 'The waterfall drop is still active. Do not try to catch the knife yet.';
    }

    if (ema9 > 0 && price > ema9 && rsi >= 50 && rsi <= 65)
      return 'Do nothing — trend is healthy. Price above 9-EMA, let it run.';
    if (sma20 > 0 && Math.abs(price - sma20) / sma20 <= 0.02 && rsi >= 40 && rsi <= 50 && vol > 1.2)
      return 'Institutional dip-buy confirmed. Staged accumulation zone — build position gradually.';
    if (ema9 > 0 && price < ema9 && rsi < 40)
      return 'Defensive risk triggered — reduce or hedge exposure until price reclaims 9-EMA.';

    if (rsi >= 55) return 'Gentle Uptrend. No exhaustion in sight. Let the trend run.';
    if (rsi >= 45)
      return 'Sideways range. Avoid buying short-term options; time decay (Theta) will eat your contracts.';
    return 'Gentle Downtrend. Asset is gently bleeding lower due to a lack of buyers.';
  }

  protected momentumActionClass(row: RsiScanResult): string {
    const action = this.momentumAction(row);
    switch (action) {
      case 'CONFIRMED BUY SIGNAL':
        return 'ma-confirmed-buy';
      case 'CONFIRMED SELL SIGNAL':
        return 'ma-confirmed-sell';
      case 'EARLY WARNING':
        return 'ma-early-warning';
      case 'AVOID / WAIT':
        return 'ma-avoid';
      case 'HOLD LONGS':
        return 'ma-hold';
      case 'BUY / ACCUMULATE':
        return 'ma-accumulate';
      case 'REDUCE':
        return 'ma-reduce';
      default:
        return 'ma-standby';
    }
  }
}
