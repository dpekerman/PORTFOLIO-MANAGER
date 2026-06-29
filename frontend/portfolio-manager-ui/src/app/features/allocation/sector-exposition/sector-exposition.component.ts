import { DecimalPipe, NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RsiScanResult } from '../../../core/models/portfolio.models';
import { DemoModeService } from '../../../core/services/demo-mode.service';
import { PortfolioApiService } from '../../../core/services/portfolio-api.service';
import { PortfolioStateService } from '../../../core/services/portfolio-state.service';
import { ScannerStateService } from '../../../core/services/scanner-state.service';

interface PositionRow {
  symbol: string;
  companyName: string;
  marketValue: number;
  pct: number;
  lastPrice: number | null;
  gainLoss: number | null;
  gainLossPct: number | null;
  changePct: number | null;
  rsi: number | null;
  momentumShift: string | null;
  momentumAction: string | null;
  momentumActionTooltip: string | null;
}

interface IndustryRow {
  industry: string;
  marketValue: number;
  pct: number;
  gainLoss: number;
  gainLossPct: number;
  positions: PositionRow[];
}

interface SectorRow {
  sector: string;
  marketValue: number;
  pct: number;
  gainLoss: number;
  gainLossPct: number;
  industries: IndustryRow[];
  expanded: boolean;
}

@Component({
  selector: 'app-sector-exposition',
  templateUrl: './sector-exposition.component.html',
  styleUrl: './sector-exposition.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    DecimalPipe,
    NgClass,
  ],
})
export class SectorExpositionComponent {
  private readonly portfolio = inject(PortfolioStateService);
  private readonly api = inject(PortfolioApiService);
  private readonly scanner = inject(ScannerStateService);
  private readonly snackBar = inject(MatSnackBar);
  protected readonly demoMode = inject(DemoModeService);

  protected readonly refreshing = signal(false);
  protected readonly expandedSectors = signal<Set<string>>(new Set());
  protected readonly expandedIndustries = signal<Set<string>>(new Set());

  /** Scanner result map (oversold + overbought) keyed by SYMBOL.toUpperCase(). */
  protected readonly scannerMap = computed<Map<string, RsiScanResult>>(() => {
    const map = new Map<string, RsiScanResult>();
    for (const r of [...this.scanner.oversold(), ...this.scanner.overbought()])
      map.set(r.symbol.toUpperCase(), r);
    return map;
  });

  /** RSI map built from scanner state (covers oversold/overbought signals) */
  protected readonly rsiMap = computed<Map<string, number>>(() => {
    const map = new Map<string, number>();
    for (const [sym, r] of this.scannerMap()) map.set(sym, r.rsi);
    return map;
  });

  /** Compute momentum shift label for a scanner result using the same 3-rule engine. */
  private momentumShiftFor(r: RsiScanResult): string {
    const rsi = r.rsi;
    const sig = r.rsiSignal ?? rsi;
    const price = r.currentPrice;
    const ema9 = r.ema9Price ?? 0;
    const sma20 = r.sma20Price ?? 0;
    const vol = r.volumeRatio ?? 1;

    if (rsi > 65) {
      if (r.status === 'Confirmed') return 'Active SELL Trigger';
      if (r.rsiSignalAvailable && rsi <= sig) return 'Bearish Shift';
      return 'Warning';
    }
    if (rsi < 30) {
      if (r.status === 'Confirmed') return 'Active BUY Trigger';
      if (r.rsiSignalAvailable && rsi >= sig) return 'Bullish Shift';
      return 'Warning';
    }
    if (ema9 > 0 && price > ema9 && rsi >= 50 && rsi <= 65) return 'Uptrend';
    if (sma20 > 0 && Math.abs(price - sma20) / sma20 <= 0.02 && rsi >= 40 && rsi <= 50 && vol > 1.2)
      return 'Consolidation';
    if (ema9 > 0 && price < ema9 && rsi < 40) return 'Breakdown';
    if (rsi >= 55) return 'Uptrend';
    if (rsi >= 45) return 'Neutral';
    return 'Downtrend';
  }

  private momentumActionFor(r: RsiScanResult): string {
    const rsi = r.rsi;
    const sig = r.rsiSignal ?? rsi;
    const price = r.currentPrice;
    const ema9 = r.ema9Price ?? 0;
    const sma20 = r.sma20Price ?? 0;
    const vol = r.volumeRatio ?? 1;

    if (rsi > 65) {
      if (r.status === 'Confirmed') return 'CONFIRMED SELL';
      if (r.rsiSignalAvailable && rsi <= sig) return 'EARLY WARNING';
      return 'AVOID / WAIT';
    }
    if (rsi < 30) {
      if (r.status === 'Confirmed') return 'CONFIRMED BUY';
      if (r.rsiSignalAvailable && rsi >= sig) return 'EARLY WARNING';
      return 'AVOID / WAIT';
    }
    if (ema9 > 0 && price > ema9 && rsi >= 50 && rsi <= 65) return 'HOLD LONGS';
    if (sma20 > 0 && Math.abs(price - sma20) / sma20 <= 0.02 && rsi >= 40 && rsi <= 50 && vol > 1.2)
      return 'BUY / ACCUMULATE';
    if (ema9 > 0 && price < ema9 && rsi < 40) return 'REDUCE';
    if (rsi >= 55) return 'HOLD LONGS';
    if (rsi >= 45) return 'HANDS OFF';
    return 'STAND BY';
  }

  private momentumActionTooltipFor(r: RsiScanResult): string {
    const rsi = r.rsi;
    const price = r.currentPrice;
    const ema9 = r.ema9Price ?? 0;
    const sma20 = r.sma20Price ?? 0;
    const vol = r.volumeRatio ?? 1;
    if (ema9 > 0 && price > ema9 && rsi >= 50 && rsi <= 65)
      return 'Do nothing — trend is healthy. Price above 9-EMA.';
    if (sma20 > 0 && Math.abs(price - sma20) / sma20 <= 0.02 && rsi >= 40 && rsi <= 50 && vol > 1.2)
      return 'Institutional dip-buy confirmed. Staged accumulation zone.';
    if (ema9 > 0 && price < ema9 && rsi < 40)
      return 'Defensive risk triggered — reduce or hedge until price reclaims 9-EMA.';
    return this.momentumActionFor(r);
  }

  /** CSS class for momentum shift badge in the portfolio ticker view. */
  protected momentumShiftClass(shift: string | null): string {
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

  isSectorExpanded(sector: string): boolean {
    return this.expandedSectors().has(sector);
  }

  isIndustryExpanded(key: string): boolean {
    return this.expandedIndustries().has(key);
  }

  expandAll(): void {
    const allSectors = new Set(this.sectors().map((s) => s.sector));
    const allIndustries = new Set(
      this.sectors().flatMap((s) =>
        s.industries.map((ind) => this.industryKey(s.sector, ind.industry)),
      ),
    );
    this.expandedSectors.set(allSectors);
    this.expandedIndustries.set(allIndustries);
  }

  collapseAll(): void {
    this.expandedSectors.set(new Set());
    this.expandedIndustries.set(new Set());
  }

  protected readonly sectors = computed<SectorRow[]>(() => {
    const summaries = this.portfolio.summaries().filter((s) => s.item.transactionType !== 'CLOSE');
    const totalValue = this.portfolio.totalValue();
    if (totalValue === 0) return [];

    const rsiMap = this.rsiMap();

    // sector → industry → symbol → aggregated position accumulators
    const sectorMap = new Map<
      string,
      Map<
        string,
        Map<
          string,
          {
            companyName: string;
            marketValue: number;
            cost: number;
            quote: (typeof summaries)[0]['quote'] | null;
          }
        >
      >
    >();

    for (const s of summaries) {
      const isManual = s.item.isManual;
      const price = s.quote?.currentPrice ?? s.item.averageCostBasis;
      const mv = isManual
        ? (s.item.manualMarketValue ?? s.item.averageCostBasis * s.item.shares)
        : price * s.item.shares;
      const cost = s.item.averageCostBasis * s.item.shares;

      const sector =
        s.item.sectorIsOverridden && s.item.sector
          ? s.item.sector
          : s.quote?.sector || s.item.sector || 'Unclassified';
      const industry = s.item.industry || s.quote?.industry || '';
      const industryKey = industry || sector;

      if (!sectorMap.has(sector)) sectorMap.set(sector, new Map());
      const industryMap = sectorMap.get(sector)!;
      if (!industryMap.has(industryKey)) industryMap.set(industryKey, new Map());
      const symbolMap = industryMap.get(industryKey)!;

      const sym = s.item.symbol;
      if (!symbolMap.has(sym)) {
        symbolMap.set(sym, {
          companyName: s.item.companyName,
          marketValue: 0,
          cost: 0,
          quote: s.quote,
        });
      }
      const agg = symbolMap.get(sym)!;
      agg.marketValue += mv;
      agg.cost += cost;
      if (!agg.quote && s.quote) agg.quote = s.quote;
    }

    return [...sectorMap.entries()]
      .map(([sector, industryMap]) => {
        const industries: IndustryRow[] = [...industryMap.entries()]
          .map(([industry, symbolMap]) => {
            const positions: PositionRow[] = [...symbolMap.entries()]
              .map(([sym, agg]) => {
                const gainLoss = agg.marketValue - agg.cost;
                const gainLossPct = agg.cost > 0 ? (gainLoss / agg.cost) * 100 : null;
                const r = this.scannerMap().get(sym.toUpperCase());
                return {
                  symbol: sym,
                  companyName: agg.companyName,
                  marketValue: agg.marketValue,
                  pct: (agg.marketValue / totalValue) * 100,
                  lastPrice: agg.quote?.currentPrice ?? null,
                  gainLoss,
                  gainLossPct,
                  changePct: agg.quote?.changePercent ?? null,
                  rsi: rsiMap.get(sym.toUpperCase()) ?? null,
                  momentumShift: r ? this.momentumShiftFor(r) : null,
                  momentumAction: r ? this.momentumActionFor(r) : null,
                  momentumActionTooltip: r ? this.momentumActionTooltipFor(r) : null,
                };
              })
              .sort((a, b) => b.marketValue - a.marketValue);

            const mv = positions.reduce((sum, p) => sum + p.marketValue, 0);
            const totalGl = positions.reduce((sum, p) => sum + (p.gainLoss ?? 0), 0);
            const totalCost = positions.reduce(
              (sum, p) => sum + p.marketValue - (p.gainLoss ?? 0),
              0,
            );
            return {
              industry,
              marketValue: mv,
              pct: (mv / totalValue) * 100,
              gainLoss: totalGl,
              gainLossPct: totalCost > 0 ? (totalGl / totalCost) * 100 : 0,
              positions,
            };
          })
          .sort((a, b) => b.marketValue - a.marketValue);

        const sectorMv = industries.reduce((sum, i) => sum + i.marketValue, 0);
        const sectorGl = industries.reduce((sum, i) => sum + i.gainLoss, 0);
        const sectorCost = sectorMv - sectorGl;
        return {
          sector,
          marketValue: sectorMv,
          pct: (sectorMv / totalValue) * 100,
          gainLoss: sectorGl,
          gainLossPct: sectorCost > 0 ? (sectorGl / sectorCost) * 100 : 0,
          industries,
          expanded: false,
        };
      })
      .sort((a, b) => b.marketValue - a.marketValue);
  });

  toggleSector(sector: string): void {
    this.expandedSectors.update((set) => {
      const next = new Set(set);
      if (next.has(sector)) next.delete(sector);
      else next.add(sector);
      return next;
    });
  }

  toggleIndustry(key: string): void {
    this.expandedIndustries.update((set) => {
      const next = new Set(set);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  industryKey(sector: string, industry: string): string {
    return `${sector}::${industry}`;
  }

  refreshSectors(): void {
    if (this.refreshing()) return;
    this.refreshing.set(true);
    this.api.refreshSectors().subscribe({
      next: ({ updated }) => {
        this.refreshing.set(false);
        this.snackBar.open(
          `Sector data updated for ${updated} position${updated !== 1 ? 's' : ''}.`,
          'OK',
          { duration: 4000 },
        );
        // Reload portfolio so new sector values flow into computed signals
        this.portfolio.refresh();
      },
      error: () => {
        this.refreshing.set(false);
        this.snackBar.open('Failed to refresh sector data.', 'Dismiss', { duration: 4000 });
      },
    });
  }
}
