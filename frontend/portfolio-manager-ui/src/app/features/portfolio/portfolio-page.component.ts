import { CurrencyPipe, DatePipe, DecimalPipe, NgClass } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  CashItem,
  OptionAnalysis,
  PortfolioSummary,
  RsiScanResult,
  StockQuote,
} from '../../core/models/portfolio.models';
import { CashStateService } from '../../core/services/cash-state.service';
import {
  DecisionEngineService,
  PortfolioItemContext,
} from '../../core/services/decision-engine.service';
import { DemoModeService } from '../../core/services/demo-mode.service';
import { OptionStateService } from '../../core/services/option-state.service';
import { PortfolioApiService } from '../../core/services/portfolio-api.service';
import { PortfolioStateService } from '../../core/services/portfolio-state.service';
import { ScannerStateService } from '../../core/services/scanner-state.service';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog.component';
import { StockCardSkeletonComponent } from '../../shared/skeleton/stock-card-skeleton.component';
import { AddCashDialogComponent } from './add-cash-dialog/add-cash-dialog.component';
import { AddManualDialogComponent } from './add-manual-dialog/add-manual-dialog.component';
import { AddOptionDialogComponent } from './add-option-dialog/add-option-dialog.component';
import { AddStockDialogComponent } from './add-stock-dialog/add-stock-dialog.component';
import {
  EditCashDialogComponent,
  EditCashDialogData,
} from './edit-cash-dialog/edit-cash-dialog.component';
import {
  EditOptionDialogComponent,
  EditOptionDialogData,
} from './edit-option-dialog/edit-option-dialog.component';
import {
  EditPositionDialogComponent,
  EditPositionDialogResult,
} from './edit-position-dialog/edit-position-dialog.component';
import { ImportStocksDialogComponent } from './import-stocks-dialog/import-stocks-dialog.component';
import { PortfolioSummaryBarComponent } from './portfolio-summary-bar/portfolio-summary-bar.component';
import { StockCardComponent } from './stock-card/stock-card.component';

type SortField =
  | 'default'
  | 'marketValue'
  | 'gainLoss'
  | 'gainLossPct'
  | 'sector'
  | 'industry'
  | 'symbol';
type SortDir = 'asc' | 'desc';
type ViewMode = 'card' | 'grid';

interface AggregatePortfolioRow {
  kind: 'aggregate';
  symbol: string;
  company: string;
  rowCount: number;
  totalShares: number;
  weightedAvgCost: number;
  totalCost: number;
  totalMarketValue: number;
  totalGainLoss: number;
  totalGainLossPct: number;
  totalDayGain: number;
  accountsList: string[];
  sector: string;
  industry: string;
  quote: StockQuote | null;
  holdingRole: string | null;
}

type GridRow = PortfolioSummary | AggregatePortfolioRow;

type GridSortCol =
  | 'symbol'
  | 'company'
  | 'accountType'
  | 'sector'
  | 'industry'
  | 'shares'
  | 'avgCost'
  | 'price'
  | 'marketValue'
  | 'gainLoss'
  | 'gainLossPct'
  | 'rsi'
  | 'changePct'
  | 'dayGain'
  | 'holdingRole'
  | 'trendSetup'
  | 'momentumShift'
  | 'finalAction';

type OptionSortCol =
  | 'opt_ticker'
  | 'opt_type'
  | 'opt_expiry'
  | 'opt_strike'
  | 'opt_premium'
  | 'opt_contracts'
  | 'opt_stockPrice'
  | 'opt_dte'
  | 'opt_cost'
  | 'opt_mv'
  | 'opt_gl'
  | 'opt_glp'
  | 'opt_state'
  | 'opt_action';

@Component({
  selector: 'app-portfolio-page',
  templateUrl: './portfolio-page.component.html',
  styleUrl: './portfolio-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    NgClass,
    FormsModule,
    CurrencyPipe,
    DatePipe,
    DecimalPipe,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatSortModule,
    MatTableModule,
    MatTooltipModule,
    StockCardComponent,
    PortfolioSummaryBarComponent,
    StockCardSkeletonComponent,
  ],
})
export class PortfolioPageComponent {
  protected readonly portfolio = inject(PortfolioStateService);
  protected readonly cashState = inject(CashStateService);
  protected readonly optionState = inject(OptionStateService);
  protected readonly scanner = inject(ScannerStateService);
  protected readonly demoMode = inject(DemoModeService);
  private readonly api = inject(PortfolioApiService);
  private readonly dialog = inject(MatDialog);
  protected readonly engine = inject(DecisionEngineService);

  /** Ghost cards displayed while portfolio loads for the first time */
  protected readonly skeletonItems = Array.from({ length: 9 }, (_, i) => i);

  // ── Section collapse state ──────────────────────────────────────────────────
  protected readonly stocksExpanded = signal(true);
  protected readonly cashExpanded = signal(true);
  protected readonly optionsExpanded = signal(true);

  protected readonly sortField = signal<SortField>('marketValue');
  protected readonly sortDir = signal<SortDir>('desc');

  protected readonly viewMode = signal<ViewMode>('grid');

  protected readonly filterTicker = signal('');
  protected readonly filterSector = signal('');
  protected readonly filterIndustry = signal('');
  protected readonly filterRole = signal('');
  protected readonly filterMomentumShift = signal('');

  protected readonly gridSortCol = signal<GridSortCol>('marketValue');
  protected readonly gridSortDir = signal<SortDir>('desc');

  protected readonly gridDisplayedColumns: string[] = [
    'symbol',
    'company',
    'accountType',
    'sector',
    'industry',
    'shares',
    'avgCost',
    'price',
    'analystTarget',
    'changePct',
    'dayGain',
    'marketValue',
    'gainLoss',
    'gainLossPct',
    'rsi',
    'holdingRole',
    'trendSetup',
    'momentumShift',
    'finalAction',
    'actions',
  ];

  // Track which multi-account groups are collapsed (default: all expanded)
  protected readonly collapsedSymbols = signal<Set<string>>(new Set<string>());

  // ── Option grid sort ────────────────────────────────────────────────────
  protected readonly optionSortCol = signal<OptionSortCol>('opt_ticker');
  protected readonly optionSortDir = signal<SortDir>('asc');

  protected readonly optionDisplayedColumns: string[] = [
    'opt_ticker',
    'opt_type',
    'opt_expiry',
    'opt_strike',
    'opt_premium',
    'opt_contracts',
    'opt_stockPrice',
    'opt_dte',
    'opt_cost',
    'opt_mv',
    'opt_gl',
    'opt_glp',
    'opt_state',
    'opt_action',
    'opt_actions',
  ];

  protected readonly totalDayGain = computed<number>(() =>
    this.portfolio.summaries().reduce((sum, s) => {
      if (s.item.isManual) return sum;
      return sum + s.item.shares * (s.quote?.change ?? 0);
    }, 0),
  );

  protected readonly sortedOptionAnalyses = computed(() => {
    const col = this.optionSortCol();
    const dir = this.optionSortDir() === 'asc' ? 1 : -1;
    return [...this.optionState.analyses()]
      .filter((a) => a.item.transactionType !== 'CLOSE')
      .sort((a, b) => {
        const av = this.optionSortValue(a, col);
        const bv = this.optionSortValue(b, col);
        if (typeof av === 'number' && typeof bv === 'number') return (av - bv) * dir;
        return String(av).localeCompare(String(bv)) * dir;
      });
  });

  private optionSortValue(a: OptionAnalysis, col: OptionSortCol): number | string {
    switch (col) {
      case 'opt_ticker':
        return a.item.underlyingTicker;
      case 'opt_type':
        return a.item.positionType;
      case 'opt_expiry':
        return a.item.expirationDate;
      case 'opt_strike':
        return a.item.strike;
      case 'opt_premium':
        return a.item.premium;
      case 'opt_contracts':
        return a.item.numberOfContracts;
      case 'opt_stockPrice':
        return a.stockPrice ?? 0;
      case 'opt_dte':
        return a.dte;
      case 'opt_cost':
        return a.cost;
      case 'opt_mv':
        return a.marketValue;
      case 'opt_gl':
        return a.gainLoss;
      case 'opt_glp':
        return a.gainLossPct;
      case 'opt_state':
        return a.optionState;
      case 'opt_action':
        return a.action;
      default:
        return 0;
    }
  }

  onOptionSortChange(sort: Sort): void {
    if (!sort.active || sort.direction === '') return;
    this.optionSortCol.set(sort.active as OptionSortCol);
    this.optionSortDir.set(sort.direction as SortDir);
  }

  /**
   * Full RsiScanResult map for all portfolio symbols (keyed by UPPER symbol).
   * Covers ALL symbols, not just oversold/overbought extremes.
   */
  protected readonly portfolioRsiResultMap = signal<Map<string, RsiScanResult>>(new Map());

  constructor() {
    // Whenever portfolio summaries change, load RSI for ALL non-manual symbols.
    // We batch in groups of 50 (backend limit) and merge results.
    effect(() => {
      const symbols = this.portfolio
        .summaries()
        .filter((s) => !s.item.isManual)
        .map((s) => s.item.symbol);
      if (symbols.length === 0) return;

      // Split into batches of 50
      const batchSize = 50;
      const batches: string[][] = [];
      for (let i = 0; i < symbols.length; i += batchSize)
        batches.push(symbols.slice(i, i + batchSize));

      const merged = new Map<string, RsiScanResult>();
      let completed = 0;
      for (const batch of batches) {
        this.api.analyzeSymbols(batch, 30, 75, 'Enhanced').subscribe({
          next: (results) => {
            for (const r of results) merged.set(r.symbol.toUpperCase(), r);
            completed++;
            if (completed === batches.length) this.portfolioRsiResultMap.set(new Map(merged));
          },
          error: (err) => {
            console.warn('Portfolio RSI batch fetch failed', err);
            completed++;
            if (completed === batches.length) this.portfolioRsiResultMap.set(new Map(merged));
          },
        });
      }
    });
  }

  /** Full result map: symbol (upper) → RsiScanResult */
  protected readonly rsiMap = computed<Map<string, RsiScanResult>>(() => {
    const map = new Map<string, RsiScanResult>();
    for (const [sym, r] of this.portfolioRsiResultMap()) map.set(sym, r);
    for (const r of [...this.scanner.oversold(), ...this.scanner.overbought()])
      map.set(r.symbol.toUpperCase(), r);
    return map;
  });

  protected rsiForSymbol(symbol: string): number | null {
    return this.rsiMap().get(symbol.toUpperCase())?.rsi ?? null;
  }

  protected readonly roles = ['Core', 'Strategic', 'Swing', 'Speculative', 'Options'] as const;

  protected roleClass(role: string | null | undefined): string {
    switch (role) {
      case 'Core':
        return 'role-core';
      case 'Strategic':
        return 'role-strategic';
      case 'Swing':
        return 'role-swing';
      case 'Speculative':
        return 'role-speculative';
      case 'Options':
        return 'role-options';
      default:
        return 'role-strategic';
    }
  }

  updateHoldingRole(row: PortfolioSummary, role: string): void {
    this.portfolio.updateHoldingRole(row.item.id, role);
  }

  updateAggHoldingRole(agg: AggregatePortfolioRow, role: string): void {
    // Update all individual positions that belong to this grouped symbol
    const items = this.portfolio
      .summaries()
      .filter((s) => s.item.symbol === agg.symbol && s.item.transactionType !== 'CLOSE');
    for (const item of items) {
      this.portfolio.updateHoldingRole(item.item.id, role);
    }
  }

  protected analystForSymbol(symbol: string): { price: number; upside: number } | null {
    const r = this.rsiMap().get(symbol.toUpperCase());
    if (!r || !r.analystTargetPrice) return null;
    return { price: r.analystTargetPrice, upside: r.analystTargetUpside };
  }

  protected decisionForPortfolio(
    symbol: string,
    holdingRole: string | null | undefined,
    item?: import('../../core/models/portfolio.models').PortfolioItem,
  ): import('../../core/services/decision-engine.service').PageDecision | null {
    const r = this.rsiMap().get(symbol.toUpperCase());
    if (!r) return null;

    let context: PortfolioItemContext | undefined;
    if (item) {
      const unrealizedGainPct =
        item.averageCostBasis && item.averageCostBasis > 0
          ? ((r.currentPrice - item.averageCostBasis) / item.averageCostBasis) * 100
          : null;
      const holdingDays = item.openDate
        ? Math.floor((Date.now() - new Date(item.openDate).getTime()) / (1000 * 60 * 60 * 24))
        : null;
      context = {
        accountType: item.accountType ?? null,
        unrealizedGainPct,
        holdingDays,
      };
    }

    return this.engine.translateForPortfolio(r, holdingRole ?? null, true, context);
  }

  protected readonly sortedSummaries = computed(() => {
    const list = [...this.portfolio.summaries()].filter((s) => s.item.transactionType !== 'CLOSE');
    const field = this.sortField();
    const dir = this.sortDir() === 'asc' ? 1 : -1;

    if (field === 'default') return list;

    return list.sort((a, b) => {
      const av = this.sortValue(a, field);
      const bv = this.sortValue(b, field);
      if (typeof av === 'number' && typeof bv === 'number') return (av - bv) * dir;
      return String(av).localeCompare(String(bv)) * dir;
    });
  });

  // â”€â”€ Unique filter options â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  protected readonly uniqueSectors = computed<string[]>(() => {
    const set = new Set<string>();
    for (const s of this.portfolio.summaries()) {
      const sector = s.item.sector || s.quote?.sector;
      if (sector) set.add(sector);
    }
    return [...set].sort();
  });

  protected readonly uniqueIndustries = computed<string[]>(() => {
    const set = new Set<string>();
    for (const s of this.portfolio.summaries()) {
      const ind = s.item.industry || s.quote?.industry;
      if (ind) set.add(ind);
    }
    return [...set].sort();
  });

  protected readonly momentumShiftOptions = [
    'Active Buy Trigger',
    'Active Sell Trigger',
    'Warning',
    'Warning — Overbought Run',
    'Bullish Shift',
    'Bearish Shift',
    'Breakdown',
    'Consolidation / Dip-Buy',
    'Uptrend',
    'Neutral',
  ] as const;

  // â”€â”€ Grid filtered + sorted rows â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  protected readonly gridRows = computed<GridRow[]>(() => {
    const ticker = this.filterTicker().trim().toLowerCase();
    const sector = this.filterSector();
    const industry = this.filterIndustry();
    const filterRole = this.filterRole();
    const filterMomentum = this.filterMomentumShift();

    const rows = this.portfolio.summaries().filter((s) => {
      if (s.item.transactionType === 'CLOSE') return false;
      if (
        ticker &&
        !s.item.symbol.toLowerCase().includes(ticker) &&
        !s.item.companyName.toLowerCase().includes(ticker)
      )
        return false;
      if (sector && (s.item.sector || s.quote?.sector || '') !== sector) return false;
      if (industry && (s.item.industry || s.quote?.industry || '') !== industry) return false;
      if (filterRole && (s.item.holdingRole ?? 'Strategic') !== filterRole) return false;
      if (filterMomentum) {
        const ms =
          this.decisionForPortfolio(s.item.symbol, s.item.holdingRole, s.item)?.momentumShift ?? '';
        if (ms !== filterMomentum) return false;
      }
      return true;
    });

    const col = this.gridSortCol();
    const dir = this.gridSortDir() === 'asc' ? 1 : -1;

    const sorted = [...rows].sort((a, b) => {
      const av = this.gridSortValue(a, col);
      const bv = this.gridSortValue(b, col);
      if (typeof av === 'number' && typeof bv === 'number') return (av - bv) * dir;
      return String(av).localeCompare(String(bv)) * dir;
    });

    // Group by symbol – tickers with 2+ entries get a collapsible aggregate group header
    const groups = new Map<string, PortfolioSummary[]>();
    for (const s of sorted) {
      const sym = s.item.symbol;
      if (!groups.has(sym)) groups.set(sym, []);
      groups.get(sym)!.push(s);
    }

    const collapsed = this.collapsedSymbols();
    const result: GridRow[] = [];
    for (const [sym, group] of groups) {
      if (group.length === 1) {
        // Single-account ticker: show row directly
        result.push(group[0]);
      } else {
        // Multi-account ticker: show aggregate header first
        const totalShares = group.reduce((sum, s) => sum + s.item.shares, 0);
        const totalCost = group.reduce(
          (sum, s) => sum + s.item.averageCostBasis * s.item.shares,
          0,
        );
        const totalMv = group.reduce((sum, s) => {
          const p = s.quote?.currentPrice ?? s.item.averageCostBasis;
          return (
            sum +
            (s.item.isManual
              ? (s.item.manualMarketValue ?? s.item.averageCostBasis)
              : p * s.item.shares)
          );
        }, 0);
        const totalDayGain = group.reduce(
          (sum, s) => sum + (s.item.isManual ? 0 : s.item.shares * (s.quote?.change ?? 0)),
          0,
        );
        const accountsList = [
          ...new Set(group.map((s) => s.item.accountType).filter((a): a is string => !!a)),
        ];
        result.push({
          kind: 'aggregate',
          symbol: group[0].item.symbol,
          company: group[0].item.companyName,
          rowCount: group.length,
          totalShares,
          weightedAvgCost: totalShares > 0 ? totalCost / totalShares : 0,
          totalCost,
          totalMarketValue: totalMv,
          totalGainLoss: totalMv - totalCost,
          totalGainLossPct: totalCost > 0 ? ((totalMv - totalCost) / totalCost) * 100 : 0,
          totalDayGain,
          accountsList,
          sector: group[0].item.sector || group[0].quote?.sector || '',
          industry: group[0].item.industry || group[0].quote?.industry || '',
          quote: group[0].quote,
          holdingRole: group[0].item.holdingRole ?? null,
        } satisfies AggregatePortfolioRow);
        // Individual rows only shown when group is expanded
        if (!collapsed.has(sym)) {
          result.push(...group);
        }
      }
    }
    return result;
  });

  protected readonly gridItemCount = computed(
    () => this.gridRows().filter((r) => !('kind' in r)).length,
  );

  protected isItemRow = (_i: number, row: GridRow): row is PortfolioSummary => !('kind' in row);
  protected isAggRow = (_i: number, row: GridRow): row is AggregatePortfolioRow =>
    'kind' in row && (row as AggregatePortfolioRow).kind === 'aggregate';

  // Template cast helpers (Angular doesn't narrow union types in @if blocks)
  protected asItem = (row: GridRow) => row as PortfolioSummary;
  protected asAgg = (row: GridRow) => row as AggregatePortfolioRow;

  // Symbols that have a group header (used to style child rows with indentation)
  protected readonly groupedSymbols = computed<Set<string>>(() => {
    const set = new Set<string>();
    for (const r of this.gridRows()) {
      if ('kind' in r) set.add(r.symbol);
    }
    return set;
  });

  protected toggleGroup(symbol: string, event: Event): void {
    event.stopPropagation();
    this.collapsedSymbols.update((set) => {
      const next = new Set(set);
      if (next.has(symbol)) next.delete(symbol);
      else next.add(symbol);
      return next;
    });
  }

  private gridSortValue(s: PortfolioSummary, col: GridSortCol): number | string {
    const price = s.quote?.currentPrice ?? s.item.averageCostBasis;
    const mv = s.item.isManual
      ? (s.item.manualMarketValue ?? s.item.averageCostBasis)
      : price * s.item.shares;
    const cost = s.item.averageCostBasis * s.item.shares;
    const rsi = this.rsiForSymbol(s.item.symbol) ?? 0;
    switch (col) {
      case 'symbol':
        return s.item.symbol;
      case 'company':
        return s.item.companyName;
      case 'accountType':
        return s.item.accountType ?? '';
      case 'sector':
        return s.item.sector || s.quote?.sector || '';
      case 'industry':
        return s.item.industry || s.quote?.industry || '';
      case 'shares':
        return s.item.shares;
      case 'avgCost':
        return s.item.averageCostBasis;
      case 'price':
        return price;
      case 'marketValue':
        return mv;
      case 'gainLoss':
        return mv - cost;
      case 'gainLossPct':
        return cost > 0 ? ((mv - cost) / cost) * 100 : 0;
      case 'rsi':
        return rsi;
      case 'changePct':
        return s.quote?.changePercent ?? 0;
      case 'dayGain':
        return s.item.isManual ? 0 : s.item.shares * (s.quote?.change ?? 0);
      case 'trendSetup':
        return (
          this.decisionForPortfolio(s.item.symbol, s.item.holdingRole, s.item)?.trendSetup ?? ''
        );
      case 'momentumShift':
        return (
          this.decisionForPortfolio(s.item.symbol, s.item.holdingRole, s.item)?.momentumShift ?? ''
        );
      case 'finalAction':
        return (
          this.decisionForPortfolio(s.item.symbol, s.item.holdingRole, s.item)?.finalAction ?? ''
        );
      default:
        return 0;
    }
  }

  onGridSortChange(sort: Sort): void {
    if (!sort.active || sort.direction === '') return;
    this.gridSortCol.set(sort.active as GridSortCol);
    this.gridSortDir.set(sort.direction as SortDir);
  }

  clearGridFilters(): void {
    this.filterTicker.set('');
    this.filterSector.set('');
    this.filterIndustry.set('');
    this.filterRole.set('');
    this.filterMomentumShift.set('');
  }

  get hasActiveFilters(): boolean {
    return !!(
      this.filterTicker() ||
      this.filterSector() ||
      this.filterIndustry() ||
      this.filterRole() ||
      this.filterMomentumShift()
    );
  }

  private sortValue(s: PortfolioSummary, field: SortField): number | string {
    const price = s.quote?.currentPrice ?? s.item.averageCostBasis;
    const marketValue = s.item.isManual
      ? (s.item.manualMarketValue ?? s.item.averageCostBasis)
      : price * s.item.shares;
    const cost = s.item.averageCostBasis * s.item.shares;
    switch (field) {
      case 'marketValue':
        return marketValue;
      case 'gainLoss':
        return marketValue - cost;
      case 'gainLossPct':
        return cost > 0 ? ((marketValue - cost) / cost) * 100 : 0;
      case 'sector':
        return s.item.sector ?? '';
      case 'industry':
        return s.item.industry ?? '';
      case 'symbol':
        return s.item.symbol;
      default:
        return 0;
    }
  }

  toggleSortDir(): void {
    this.sortDir.update((d) => (d === 'asc' ? 'desc' : 'asc'));
  }

  setSortField(field: SortField): void {
    if (this.sortField() === field) {
      this.toggleSortDir();
    } else {
      this.sortField.set(field);
      this.sortDir.set('desc');
    }
  }

  exportCsv(): void {
    const grouped = this.groupedSymbols();
    const rows: string[][] = [
      [
        'Symbol',
        'Company',
        'Account Type',
        'Sector',
        'Industry',
        'Shares',
        'Avg Cost',
        'Current Price',
        'Market Value',
        'Gain/Loss',
        'Gain/Loss %',
        'Daily Change',
        'Daily Change %',
        'RSI (14)',
        'Role',
        'Trend Setup',
        'Momentum Shift',
        'Action',
      ],
    ];

    for (const row of this.gridRows()) {
      if (this.isAggRow(0, row)) {
        // Multi-account ticker: export one aggregated row listing all accounts
        const agg = row as AggregatePortfolioRow;
        const rsiVal = this.rsiForSymbol(agg.symbol);
        rows.push([
          agg.symbol,
          agg.company,
          agg.accountsList.join(', '),
          agg.sector,
          agg.industry,
          agg.totalShares.toString(),
          agg.weightedAvgCost.toFixed(2),
          (agg.quote?.currentPrice ?? 0).toFixed(2),
          agg.totalMarketValue.toFixed(2),
          agg.totalGainLoss.toFixed(2),
          agg.totalGainLossPct.toFixed(2),
          (agg.quote?.change ?? 0).toFixed(2),
          (agg.quote?.changePercent ?? 0).toFixed(2),
          rsiVal !== null ? rsiVal.toFixed(1) : '',
          '',
          this.decisionForPortfolio(agg.symbol, null)?.trendSetup ?? '',
          this.decisionForPortfolio(agg.symbol, null)?.momentumShift ?? '',
          this.decisionForPortfolio(agg.symbol, null)?.finalAction ?? '',
        ]);
      } else {
        // Skip individual child rows that belong to a multi-account group
        const s = row as PortfolioSummary;
        if (grouped.has(s.item.symbol)) continue;

        const price = s.quote?.currentPrice ?? s.item.averageCostBasis;
        const marketValue = s.item.isManual
          ? (s.item.manualMarketValue ?? s.item.averageCostBasis)
          : price * s.item.shares;
        const cost = s.item.averageCostBasis * s.item.shares;
        const gainLoss = marketValue - cost;
        const gainLossPct = cost > 0 ? ((gainLoss / cost) * 100).toFixed(2) : '0';
        const rsiVal = this.rsiForSymbol(s.item.symbol);
        rows.push([
          s.item.symbol,
          s.item.companyName,
          s.item.accountType ?? '',
          s.item.sector ?? s.quote?.sector ?? '',
          s.item.industry ?? s.quote?.industry ?? '',
          s.item.shares.toString(),
          s.item.averageCostBasis.toFixed(2),
          price.toFixed(2),
          marketValue.toFixed(2),
          gainLoss.toFixed(2),
          gainLossPct,
          (s.quote?.change ?? 0).toFixed(2),
          (s.quote?.changePercent ?? 0).toFixed(2),
          rsiVal !== null ? rsiVal.toFixed(1) : '',
          s.item.holdingRole ?? 'Strategic',
          this.decisionForPortfolio(s.item.symbol, s.item.holdingRole, s.item)?.trendSetup ?? '',
          this.decisionForPortfolio(s.item.symbol, s.item.holdingRole, s.item)?.momentumShift ?? '',
          this.decisionForPortfolio(s.item.symbol, s.item.holdingRole, s.item)?.finalAction ?? '',
        ]);
      }
    }
    this.downloadCsv(rows, 'portfolio.csv');
  }

  private downloadCsv(rows: string[][], filename: string): void {
    const csv = rows.map((r) => r.map((c) => `"${c.replace(/"/g, '""')}"`).join(',')).join('\r\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }

  openEditGridRow(s: PortfolioSummary): void {
    this.dialog
      .open(EditPositionDialogComponent, {
        data: { item: s.item },
        width: '620px',
        maxWidth: '95vw',
        maxHeight: '95vh',
      })
      .afterClosed()
      .subscribe((result: EditPositionDialogResult | undefined) => {
        if (!result) return;
        this.portfolio.updateItem(s.item.id, {
          companyName: result.companyName,
          shares: result.shares,
          averageCostBasis: result.averageCostBasis,
          sector: result.sector,
          industry: result.industry,
          overrideSector: result.overrideSector,
          transactionType: result.transactionType,
          accountType: result.accountType,
          openDate: result.openDate,
          closeDate: result.closeDate,
          closingPrice: result.closingPrice,
        });
      });
  }

  confirmDeleteGridRow(s: PortfolioSummary): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Remove Position',
          message: `Remove ${s.item.symbol} (${s.item.companyName}) from your portfolio? This cannot be undone.`,
          confirmLabel: 'Remove',
          danger: true,
        },
        width: '400px',
      })
      .afterClosed()
      .subscribe((confirmed: boolean) => {
        if (confirmed) this.portfolio.deleteItem(s.item.id, s.item.symbol);
      });
  }

  openAddStockDialog(): void {
    this.dialog.open(AddStockDialogComponent, {
      width: '620px',
      maxWidth: '95vw',
      maxHeight: '95vh',
    });
  }

  openAddManualDialog(): void {
    this.dialog.open(AddManualDialogComponent, { width: '480px', maxWidth: '95vw' });
  }

  openAddCashDialog(): void {
    this.dialog.open(AddCashDialogComponent, { width: '420px', maxWidth: '95vw' });
  }

  openAddOptionDialog(): void {
    this.dialog.open(AddOptionDialogComponent, {
      width: '620px',
      maxWidth: '95vw',
      maxHeight: '95vh',
    });
  }

  openEditCashDialog(item: CashItem): void {
    this.dialog.open(EditCashDialogComponent, {
      data: { item } satisfies EditCashDialogData,
      width: '420px',
      maxWidth: '95vw',
    });
  }

  openEditOptionDialog(analysis: OptionAnalysis): void {
    this.dialog.open(EditOptionDialogComponent, {
      data: { item: analysis.item } satisfies EditOptionDialogData,
      width: '620px',
      maxWidth: '95vw',
      maxHeight: '95vh',
    });
  }

  confirmDeleteCash(item: CashItem): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Remove Cash Position',
          message: `Remove "${item.description}" ($${item.amount.toFixed(2)}) from your portfolio?`,
          confirmLabel: 'Remove',
          danger: true,
        },
        width: '400px',
      })
      .afterClosed()
      .subscribe((confirmed: boolean) => {
        if (confirmed) this.cashState.deleteItem(item.id);
      });
  }

  confirmDeleteOption(analysis: OptionAnalysis): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Remove Option Position',
          message: `Remove ${analysis.item.underlyingTicker} ${analysis.item.positionType} $${analysis.item.strike} exp ${analysis.item.expirationDate.split('T')[0]}?`,
          confirmLabel: 'Remove',
          danger: true,
        },
        width: '400px',
      })
      .afterClosed()
      .subscribe((confirmed: boolean) => {
        if (confirmed) this.optionState.deleteItem(analysis.item.id);
      });
  }

  // ── Option state helpers ──────────────────────────────────────────────────
  optionStateClass(state: string): string {
    switch (state) {
      case 'FREE_TRADE_MILESTONE':
        return 'os-free-trade';
      case 'INTRINSIC_CRACKED':
        return 'os-cut';
      case 'TEMPORARILY_BROKEN':
        return 'os-cut';
      case 'TARGET_ACHIEVED':
        return 'os-target';
      case 'VELOCITY_INVERSION':
        return 'os-momentum';
      case 'TREND_REVERSED':
        return 'os-cut';
      case 'VOLATILITY_EXPANSION':
        return 'os-target';
      case 'MONITOR':
        return 'os-monitor';
      default:
        return 'os-monitor';
    }
  }

  actionClass(action: string): string {
    if (action.startsWith('🟥')) return 'oa-exit';
    if (action.startsWith('🟩')) return 'oa-profit';
    if (action.startsWith('🟨')) return 'oa-caution';
    return 'oa-monitor';
  }

  openImportDialog(): void {
    this.dialog
      .open(ImportStocksDialogComponent, { width: '640px', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((imported) => {
        if (imported) this.scanner.refresh(true);
      });
  }

  confirmBulkDelete(): void {
    const count = this.portfolio.selectedCount();
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Remove Positions',
          message: `Remove all ${count} selected positions? This cannot be undone.`,
          confirmLabel: `Remove ${count}`,
          danger: true,
        },
        width: '400px',
      })
      .afterClosed()
      .subscribe((confirmed: boolean) => {
        if (confirmed) this.portfolio.deleteSelectedAsync();
      });
  }

  refresh(): void {
    this.portfolio.refresh();
  }
}
