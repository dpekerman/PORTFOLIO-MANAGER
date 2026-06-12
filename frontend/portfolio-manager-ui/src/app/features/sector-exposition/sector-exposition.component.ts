import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PortfolioApiService } from '../../core/services/portfolio-api.service';
import { PortfolioStateService } from '../../core/services/portfolio-state.service';

interface PositionRow {
  symbol: string;
  companyName: string;
  marketValue: number;
  pct: number;
}

interface IndustryRow {
  industry: string;
  marketValue: number;
  pct: number;
  positions: PositionRow[];
}

interface SectorRow {
  sector: string;
  marketValue: number;
  pct: number;
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
  ],
})
export class SectorExpositionComponent {
  private readonly portfolio = inject(PortfolioStateService);
  private readonly api = inject(PortfolioApiService);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly refreshing = signal(false);
  protected readonly expandedSector = signal<string | null>(null);
  protected readonly expandedIndustry = signal<string | null>(null);

  protected readonly sectors = computed<SectorRow[]>(() => {
    const summaries = this.portfolio.summaries();
    const totalValue = this.portfolio.totalValue();
    if (totalValue === 0) return [];

    // sector → industry → positions map
    const sectorMap = new Map<string, Map<string, PositionRow[]>>();

    for (const s of summaries) {
      const price = s.quote?.currentPrice ?? s.item.averageCostBasis;
      const mv = price * s.item.shares;
      const sector = s.quote?.sector || s.item.sector || 'Unclassified';
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
      });
    }

    return [...sectorMap.entries()]
      .map(([sector, industryMap]) => {
        const industries: IndustryRow[] = [...industryMap.entries()]
          .map(([industry, positions]) => {
            const mv = positions.reduce((sum, p) => sum + p.marketValue, 0);
            return {
              industry,
              marketValue: mv,
              pct: (mv / totalValue) * 100,
              positions: [...positions].sort((a, b) => b.marketValue - a.marketValue),
            };
          })
          .sort((a, b) => b.marketValue - a.marketValue);

        const sectorMv = industries.reduce((sum, i) => sum + i.marketValue, 0);
        return {
          sector,
          marketValue: sectorMv,
          pct: (sectorMv / totalValue) * 100,
          industries,
          expanded: false,
        };
      })
      .sort((a, b) => b.marketValue - a.marketValue);
  });

  toggleSector(sector: string): void {
    this.expandedSector.update((cur) => (cur === sector ? null : sector));
    this.expandedIndustry.set(null);
  }

  toggleIndustry(key: string): void {
    this.expandedIndustry.update((cur) => (cur === key ? null : key));
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
