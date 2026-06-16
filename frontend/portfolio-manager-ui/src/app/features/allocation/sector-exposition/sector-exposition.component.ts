import { DecimalPipe, NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
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

  protected readonly refreshing = signal(false);
  protected readonly expandedSectors = signal<Set<string>>(new Set());
  protected readonly expandedIndustries = signal<Set<string>>(new Set());

  /** RSI map built from scanner state (covers oversold/overbought signals) */
  protected readonly rsiMap = computed<Map<string, number>>(() => {
    const map = new Map<string, number>();
    for (const r of [...this.scanner.oversold(), ...this.scanner.overbought()])
      map.set(r.symbol.toUpperCase(), r.rsi);
    return map;
  });

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
    const summaries = this.portfolio.summaries();
    const totalValue = this.portfolio.totalValue();
    if (totalValue === 0) return [];

    const rsiMap = this.rsiMap();

    // sector → industry → positions map
    const sectorMap = new Map<string, Map<string, PositionRow[]>>();

    for (const s of summaries) {
      const isManual = s.item.isManual;
      const price = s.quote?.currentPrice ?? s.item.averageCostBasis;
      const mv = isManual
        ? (s.item.manualMarketValue ?? s.item.averageCostBasis * s.item.shares)
        : price * s.item.shares;
      const cost = s.item.averageCostBasis * s.item.shares;
      const gainLoss = mv - cost;
      const gainLossPct = cost > 0 ? (gainLoss / cost) * 100 : null;

      // If the user manually overrode the sector, always prefer the stored value
      const sector =
        s.item.sectorIsOverridden && s.item.sector
          ? s.item.sector
          : s.quote?.sector || s.item.sector || 'Unclassified';
      const industry = s.item.industry || s.quote?.industry || '';

      if (!sectorMap.has(sector)) sectorMap.set(sector, new Map());
      const industryMap = sectorMap.get(sector)!;

      const industryKey = industry || sector; // use sector name when industry is blank
      if (!industryMap.has(industryKey)) industryMap.set(industryKey, []);
      industryMap.get(industryKey)!.push({
        symbol: s.item.symbol,
        companyName: s.item.companyName,
        marketValue: mv,
        pct: (mv / totalValue) * 100,
        lastPrice: isManual ? null : (s.quote?.currentPrice ?? null),
        gainLoss,
        gainLossPct,
        changePct: isManual ? null : (s.quote?.changePercent ?? null),
        rsi: rsiMap.get(s.item.symbol.toUpperCase()) ?? null,
      });
    }

    return [...sectorMap.entries()]
      .map(([sector, industryMap]) => {
        const industries: IndustryRow[] = [...industryMap.entries()]
          .map(([industry, positions]) => {
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
              positions: [...positions].sort((a, b) => b.marketValue - a.marketValue),
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
