import { CurrencyPipe, DecimalPipe, PercentPipe, SlicePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { EmptyState, PageHeader } from '@ui';
import { OptionItem } from '../../core/models/portfolio.models';
import { CashStateService } from '../../core/services/cash-state.service';
import { DemoModeService } from '../../core/services/demo-mode.service';
import { OptionStateService } from '../../core/services/option-state.service';
import { PortfolioApiService } from '../../core/services/portfolio-api.service';
import { PortfolioBetaStateService } from '../../core/services/portfolio-beta-state.service';
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
    MatSortModule,
    MatTableModule,
    MatTooltipModule,
    RouterLink,
    EmptyState,
    PageHeader,
    SectorExpositionComponent,
  ],
})
export class AllocationPageComponent {
  protected readonly portfolio = inject(PortfolioStateService);
  protected readonly demoMode = inject(DemoModeService);
  protected readonly cashState = inject(CashStateService);
  protected readonly optionState = inject(OptionStateService);
  private readonly api = inject(PortfolioApiService);
  private readonly snackBar = inject(MatSnackBar);
  protected readonly betaState = inject(PortfolioBetaStateService);

  protected readonly showBetaDetail = signal(false);
  protected readonly betaSortCol = signal<'symbol' | 'weightPct' | 'beta'>('symbol');
  protected readonly betaSortDir = signal<'asc' | 'desc'>('asc');
  protected readonly betaColumns = ['symbol', 'weightPct', 'beta', 'actions'] as const;

  protected readonly sortedBetaContributors = computed(() => {
    const contributors = this.betaState.result()?.topContributors ?? [];
    const col = this.betaSortCol();
    const dir = this.betaSortDir() === 'asc' ? 1 : -1;
    return [...contributors].sort((a, b) => {
      let av: string | number;
      let bv: string | number;
      if (col === 'symbol') {
        av = a.symbol;
        bv = b.symbol;
      } else if (col === 'weightPct') {
        av = a.weightPct;
        bv = b.weightPct;
      } else {
        av = this.betaForSymbol(a.symbol) ?? 0;
        bv = this.betaForSymbol(b.symbol) ?? 0;
      }
      if (typeof av === 'number' && typeof bv === 'number') return (av - bv) * dir;
      return String(av).localeCompare(String(bv)) * dir;
    });
  });

  onBetaSortChange(sort: Sort): void {
    if (!sort.active || sort.direction === '') return;
    this.betaSortCol.set(sort.active as 'symbol' | 'weightPct' | 'beta');
    this.betaSortDir.set(sort.direction as 'asc' | 'desc');
  }

  constructor() {
    // Load portfolio beta on page init (non-blocking)
    this.betaState.load();
  }

  toggleBetaDetail(): void {
    this.showBetaDetail.update((v) => !v);
  }

  protected betaForSymbol(symbol: string): number | null {
    return this.betaState.betaForSymbol(symbol);
  }

  protected isBetaOverridden(symbol: string): boolean {
    return this.betaState.betaOverrides()[symbol.toUpperCase()] !== undefined;
  }

  protected dv(v: number): number {
    return this.demoMode.isDemoMode() && this.demoMode.demoStyle() === 'fake'
      ? this.demoMode.maskValue(v)
      : v;
  }

  protected dvp(v: number): number {
    return this.demoMode.isDemoMode() && this.demoMode.demoStyle() === 'fake'
      ? this.demoMode.maskPercent(v)
      : v;
  }

  protected updateBeta(symbol: string, value: string): void {
    const num = parseFloat(value);
    this.betaState.setOverride(symbol, isNaN(num) ? null : num);
  }

  protected clearBetaOverride(symbol: string): void {
    this.betaState.setOverride(symbol, null);
  }

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

  protected readonly optionTransactionCount = computed(() =>
    this.optionGroups().reduce((sum, g) => sum + g.items.length, 0),
  );

  protected readonly cashExpanded = signal(false);
  protected readonly optionsExpanded = signal(false);
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

  backupAllocationData(): void {
    this.api.backupCash().subscribe({
      next: (cashItems) => {
        this.api.backupOptions().subscribe({
          next: (optionItems) => {
            const backup = {
              exportedAt: new Date().toISOString(),
              type: 'allocation',
              cash: cashItems,
              options: optionItems,
            };
            const blob = new Blob([JSON.stringify(backup, null, 2)], {
              type: 'application/json;charset=utf-8;',
            });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `allocation-backup-${new Date().toISOString().slice(0, 10)}.json`;
            a.click();
            URL.revokeObjectURL(url);
            this.snackBar.open('Allocation backup downloaded', 'Dismiss', { duration: 3000 });
          },
        });
      },
    });
  }

  onRestoreFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    input.value = '';

    const reader = new FileReader();
    reader.onload = (e) => {
      try {
        const backup = JSON.parse(e.target?.result as string);
        if (backup.type !== 'allocation') {
          this.snackBar.open('Invalid backup file: expected allocation backup', 'Dismiss', {
            duration: 4000,
          });
          return;
        }
        const confirmed = window.confirm(
          `This will REPLACE all current cash (${this.cashState.items().length} items) and options data with the backup from ${backup.exportedAt?.slice(0, 10) ?? 'unknown date'}. Continue?`,
        );
        if (!confirmed) return;

        this.api.restoreCash({ items: backup.cash ?? [] }).subscribe({
          next: () => {
            this.cashState.refresh();
            this.api.restoreOptions({ items: backup.options ?? [] }).subscribe({
              next: () => {
                this.optionState.refresh();
                this.snackBar.open('Allocation data restored successfully', 'Dismiss', {
                  duration: 4000,
                });
              },
              error: () =>
                this.snackBar.open('Failed to restore options data', 'Dismiss', {
                  duration: 4000,
                }),
            });
          },
          error: () =>
            this.snackBar.open('Failed to restore cash data', 'Dismiss', { duration: 4000 }),
        });
      } catch {
        this.snackBar.open('Invalid JSON backup file', 'Dismiss', { duration: 4000 });
      }
    };
    reader.readAsText(file);
  }
}
