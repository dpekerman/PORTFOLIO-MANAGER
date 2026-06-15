import { CurrencyPipe, PercentPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { DemoModeService } from '../../core/services/demo-mode.service';
import { PortfolioStateService } from '../../core/services/portfolio-state.service';
import { SectorExpositionComponent } from './sector-exposition/sector-exposition.component';

@Component({
  selector: 'app-allocation-page',
  templateUrl: './allocation-page.component.html',
  styleUrl: './allocation-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CurrencyPipe,
    PercentPipe,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatTooltipModule,
    RouterLink,
    SectorExpositionComponent,
  ],
})
export class AllocationPageComponent {
  protected readonly portfolio = inject(PortfolioStateService);
  protected readonly demoMode = inject(DemoModeService);

  protected readonly isPositive = computed(() => this.portfolio.totalGainLoss() >= 0);
  protected readonly returnPct = computed(() => this.portfolio.displayTotalGainLossPct() / 100);

  exportCsv(): void {
    const totalValue = this.portfolio.totalValue();
    const summaries = this.portfolio.summaries();

    const rows: string[][] = [
      ['Sector', 'Industry', 'Symbol', 'Company', 'Market Value', 'Portfolio %'],
    ];

    for (const s of summaries) {
      const price = s.quote?.currentPrice ?? s.item.averageCostBasis;
      const marketValue = s.item.isManual
        ? (s.item.manualMarketValue ?? s.item.averageCostBasis)
        : price * s.item.shares;
      const pct = totalValue > 0 ? ((marketValue / totalValue) * 100).toFixed(2) : '0';
      rows.push([
        s.item.sector ?? 'Unknown',
        s.item.industry ?? 'Unknown',
        s.item.symbol,
        s.item.companyName,
        marketValue.toFixed(2),
        pct,
      ]);
    }

    const csv = rows.map((r) => r.map((c) => `"${c.replace(/"/g, '""')}"`).join(',')).join('\r\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'allocation.csv';
    a.click();
    URL.revokeObjectURL(url);
  }
}
