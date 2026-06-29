import { CurrencyPipe, DecimalPipe, PercentPipe, SlicePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { OptionItem } from '../../core/models/portfolio.models';
import { CashStateService } from '../../core/services/cash-state.service';
import { DemoModeService } from '../../core/services/demo-mode.service';
import { OptionStateService } from '../../core/services/option-state.service';
import { PortfolioStateService } from '../../core/services/portfolio-state.service';
import { SectorExpositionComponent } from './sector-exposition/sector-exposition.component';

interface OptionTickerGroup {
  ticker: string;
  totalValue: number;
  pct: number;
  items: Array<OptionItem & { marketValue: number; pct: number }>;
  expanded: boolean;
}

@Component({
  selector: 'app-allocation-page',
  templateUrl: './allocation-page.component.html',
  styleUrl: './allocation-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CurrencyPipe,
    DecimalPipe,
    PercentPipe,
    SlicePipe,
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
  protected readonly cashState = inject(CashStateService);
  protected readonly optionState = inject(OptionStateService);

  protected readonly isPositive = computed(() => this.portfolio.totalGainLoss() >= 0);
  protected readonly returnPct = computed(() => this.portfolio.displayTotalGainLossPct() / 100);

  protected readonly grandTotal = computed(
    () =>
      this.portfolio.totalValue() +
      this.cashState.totalCash() +
      this.optionState.totalMarketValue(),
  );

  /** Combined cost basis: stocks + options + cash (deployed capital) */
  protected readonly grandTotalCost = computed(
    () => this.portfolio.totalCost() + this.optionState.totalCost() + this.cashState.totalCash(),
  );

  /** Combined gain/loss across all asset classes */
  protected readonly grandTotalGainLoss = computed(() => this.grandTotal() - this.grandTotalCost());

  protected readonly grandTotalGainLossIsPositive = computed(() => this.grandTotalGainLoss() >= 0);

  protected readonly grandTotalGainLossPct = computed(() => {
    const cost = this.grandTotalCost();
    return cost > 0 ? (this.grandTotalGainLoss() / cost) * 100 : 0;
  });

  /** Options unrealized gain/loss */
  protected readonly optionsGainLoss = computed(
    () => this.optionState.totalMarketValue() - this.optionState.totalCost(),
  );
  protected readonly optionsGainLossIsPositive = computed(() => this.optionsGainLoss() >= 0);
  protected readonly optionsGainLossPct = computed(() => {
    const cost = this.optionState.totalCost();
    return cost > 0 ? (this.optionsGainLoss() / cost) * 100 : 0;
  });

  protected readonly cashPct = computed(() => {
    const gt = this.grandTotal();
    return gt > 0 ? this.cashState.totalCash() / gt : 0;
  });

  protected readonly optionsTotalValue = computed(() => this.optionState.totalMarketValue());

  protected readonly optionsPct = computed(() => {
    const gt = this.grandTotal();
    return gt > 0 ? this.optionsTotalValue() / gt : 0;
  });

  /** Stocks-only % of grand total */
  protected readonly stocksPct = computed(() => {
    const gt = this.grandTotal();
    return gt > 0 ? this.portfolio.totalValue() / gt : 0;
  });

  protected readonly cashItemsWithPct = computed(() => {
    const gt = this.grandTotal();
    return this.cashState.items().map((c) => ({
      ...c,
      pct: gt > 0 ? c.amount / gt : 0,
    }));
  });

  protected readonly optionGroups = computed<OptionTickerGroup[]>(() => {
    const gt = this.grandTotal();
    const openItems = this.optionState.analyses().filter((a) => a.item.transactionType !== 'CLOSE');

    const tickerMap = new Map<string, Array<OptionItem & { marketValue: number; pct: number }>>();
    for (const a of openItems) {
      const ticker = a.item.underlyingTicker;
      if (!tickerMap.has(ticker)) tickerMap.set(ticker, []);
      tickerMap.get(ticker)!.push({
        ...a.item,
        marketValue: a.marketValue,
        pct: gt > 0 ? a.marketValue / gt : 0,
      });
    }

    return [...tickerMap.entries()]
      .map(([ticker, items]) => {
        const totalValue = items.reduce((s, i) => s + i.marketValue, 0);
        return {
          ticker,
          totalValue,
          pct: gt > 0 ? totalValue / gt : 0,
          items,
          expanded: false,
        };
      })
      .sort((a, b) => b.totalValue - a.totalValue);
  });

  protected readonly cashExpanded = signal(true);
  protected readonly optionsExpanded = signal(true);
  protected readonly expandedOptionTickers = signal<Set<string>>(new Set());

  toggleOptionTicker(ticker: string): void {
    this.expandedOptionTickers.update((s) => {
      const copy = new Set(s);
      copy.has(ticker) ? copy.delete(ticker) : copy.add(ticker);
      return copy;
    });
  }

  exportCsv(): void {
    const totalValue = this.portfolio.totalValue();
    const summaries = this.portfolio.summaries().filter((s) => s.item.transactionType !== 'CLOSE');

    const rows: string[][] = [
      [
        'Type',
        'Sector',
        'Industry',
        'Symbol / Description',
        'Company',
        'Market Value',
        'Portfolio %',
      ],
    ];

    for (const s of summaries) {
      const price = s.quote?.currentPrice ?? s.item.averageCostBasis;
      const marketValue = s.item.isManual
        ? (s.item.manualMarketValue ?? s.item.averageCostBasis)
        : price * s.item.shares;
      const pct = totalValue > 0 ? ((marketValue / totalValue) * 100).toFixed(2) : '0';
      rows.push([
        'Stock',
        s.item.sector ?? 'Unknown',
        s.item.industry ?? 'Unknown',
        s.item.symbol,
        s.item.companyName,
        marketValue.toFixed(2),
        pct,
      ]);
    }

    const gt = this.grandTotal();
    for (const c of this.cashState.items()) {
      const pct = gt > 0 ? ((c.amount / gt) * 100).toFixed(2) : '0';
      rows.push(['Cash', '', '', c.description, '', c.amount.toFixed(2), pct]);
    }

    for (const a of this.optionState.analyses().filter((a) => a.item.transactionType !== 'CLOSE')) {
      const pct = gt > 0 ? ((a.marketValue / gt) * 100).toFixed(2) : '0';
      const desc = `${a.item.underlyingTicker} ${a.item.positionType} $${a.item.strike} exp ${a.item.expirationDate}`;
      rows.push(['Option', '', '', a.item.underlyingTicker, desc, a.marketValue.toFixed(2), pct]);
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
