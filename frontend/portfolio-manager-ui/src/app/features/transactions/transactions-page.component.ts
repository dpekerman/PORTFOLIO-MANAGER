import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
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
import * as XLSX from 'xlsx';
import {
  OptionAnalysis,
  OptionItem,
  PortfolioItem,
  PortfolioSummary,
} from '../../core/models/portfolio.models';
import { AuthStore } from '../../core/store/auth.store';
import { DashboardStore } from '../../core/store/dashboard.store';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { GridColumnStore } from '../../core/store/grid-column.store';
import { OptionStore } from '../../core/store/option.store';
import { PortfolioApiService } from '../../core/services/portfolio-api.service';
import { PortfolioStore } from '../../core/store/portfolio.store';
import { currencyCodeForTradingSymbol } from '../../core/technical-display';
import { GridColumnButtonComponent } from '../../shared/column-config-dialog/grid-column-btn.component';
import { MobileTableControlsComponent } from '../../shared/responsive-table/mobile-table-controls.component';
import { ResponsiveTableDirective } from '../../shared/responsive-table/responsive-table.directive';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../shared/confirm-dialog/confirm-dialog.component';
import {
  EditOptionDialogComponent,
  EditOptionDialogData,
} from '../portfolio/edit-option-dialog/edit-option-dialog.component';
import {
  EditPositionDialogComponent,
  EditPositionDialogData,
  EditPositionDialogResult,
} from '../portfolio/edit-position-dialog/edit-position-dialog.component';
import {
  TransactionNotesDialogComponent,
  TransactionNotesDialogData,
  TransactionNotesDialogResult,
} from './transaction-notes-dialog/transaction-notes-dialog.component';

import {
  ExportCell,
  TX_NONE,
  TX_OPTION_EXPORT_COLUMNS,
  TX_STOCK_EXPORT_COLUMNS,
  TxExportColumn,
  TxFilterState,
  TxTotalsRow,
  TxTypeFilter,
  dateRangeInvalid,
  activeFilterSummary,
  closeFilterApplies,
  compareSortValues,
  computeTxTotals,
  dateOnly,
  daysSinceClose,
  distinctValues,
  isFutureClose,
  matchesTx,
  msUntilTorontoMidnight,
  torontoDateString,
  txRowsToCsv,
  txSheet,
} from './transactions-logic';

type SortDir = 'asc' | 'desc';

type StockTxCol =
  | 'tx_type'
  | 'tx_account'
  | 'tx_symbol'
  | 'tx_company'
  | 'tx_shares'
  | 'tx_avg_cost'
  | 'tx_open_date'
  | 'tx_close_date'
  | 'tx_closing_price'
  | 'tx_gain_loss'
  | 'tx_gain_pct'
  | 'tx_last_price'
  | 'tx_price_diff'
  | 'tx_diff_dollar'
  | 'tx_trans_date'
  | 'tx_decision_source'
  | 'tx_decision_source_closed'
  | 'tx_role'
  | 'tx_industry'
  | 'tx_age'
  | 'tx_since_close'
  | 'tx_actions';

type OptionTxCol =
  | 'otx_type'
  | 'otx_account'
  | 'otx_ticker'
  | 'otx_position'
  | 'otx_expiry'
  | 'otx_strike'
  | 'otx_premium'
  | 'otx_contracts'
  | 'otx_open_date'
  | 'otx_close_date'
  | 'otx_closing_price'
  | 'otx_gain_loss'
  | 'otx_gain_pct'
  | 'otx_mkt_value'
  | 'otx_decision_source'
  | 'otx_age'
  | 'otx_since_close'
  | 'otx_actions';

import { AppRefreshButtonComponent } from '../../shared/app-refresh-button/app-refresh-button.component';

@Component({
  selector: 'app-transactions-page',
  templateUrl: './transactions-page.component.html',
  styleUrl: './transactions-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    AppRefreshButtonComponent,
    CurrencyPipe,
    DatePipe,
    DecimalPipe,
    FormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatSortModule,
    MatTableModule,
    MatTooltipModule,
    GridColumnButtonComponent,
    MobileTableControlsComponent,
    ResponsiveTableDirective,
  ],
})
export class TransactionsPageComponent {
  protected readonly portfolio = inject(PortfolioStore);
  protected readonly optionState = inject(OptionStore);
  protected readonly demoMode = inject(DemoModeStore);
  protected readonly authState = inject(AuthStore);
  protected readonly dashboardState = inject(DashboardStore);
  private readonly api = inject(PortfolioApiService);

  protected readonly activeTab = signal<'transactions' | 'analytics'>('transactions');

  protected onTabChange(tab: 'transactions' | 'analytics'): void {
    this.activeTab.set(tab);
    if (tab === 'analytics') this.dashboardState.loadDecisionPerformance();
  }

  /** Returns masked value when fake mode is on, original value otherwise. */
  protected dv(v: number): number {
    return this.demoMode.isDemoMode() && this.demoMode.demoStyle() === 'fake'
      ? this.demoMode.maskValue(v)
      : v;
  }
  private readonly dialog = inject(MatDialog);

  // ── Section collapse ────────────────────────────────────────────────────────
  protected readonly stocksExpanded = signal(true);
  protected readonly optionsExpanded = signal(true);

  // ── Type filter (ALL | OPEN | CLOSE) ────────────────────────────────────────
  protected readonly txTypeFilter = signal<TxTypeFilter>('ALL');

  // ── Extra filters ───────────────────────────────────────────────────────────
  protected readonly filterDecisionSource = signal('');
  protected readonly filterRole = signal('');
  protected readonly filterSourceClosed = signal('');
  protected readonly filterAccount = signal('');
  protected readonly filterDateFrom = signal('');
  protected readonly filterDateTo = signal('');
  protected readonly TX_NONE = TX_NONE;

  /** Toronto calendar date; refreshed at midnight so SINCE CLOSE stays correct on a long-lived page. */
  protected readonly todayToronto = signal(torontoDateString());

  constructor() {
    let timer: ReturnType<typeof setTimeout> | undefined;
    const arm = () => {
      // Capped so a DST shift can never delay the refresh by more than the cap.
      timer = setTimeout(
        () => {
          this.refreshToday();
          arm();
        },
        Math.min(msUntilTorontoMidnight(), 30 * 60_000),
      );
    };
    const onVisible = () => {
      if (document.visibilityState === 'visible') this.refreshToday();
    };
    arm();
    document.addEventListener('visibilitychange', onVisible);
    inject(DestroyRef).onDestroy(() => {
      clearTimeout(timer);
      document.removeEventListener('visibilitychange', onVisible);
    });
  }

  private refreshToday(): void {
    const today = torontoDateString();
    if (today !== this.todayToronto()) this.todayToronto.set(today);
  }

  /** Active filters; the close filter is ignored on OPEN rather than silently hiding them. */
  protected readonly filterState = computed<TxFilterState>(() => ({
    tab: this.txTypeFilter(),
    role: this.filterRole(),
    source: this.filterDecisionSource(),
    sourceClosed: this.filterSourceClosed(),
    account: this.filterAccount(),
    dateFrom: this.filterDateFrom(),
    dateTo: this.filterDateTo(),
  }));

  protected readonly closeFilterEnabled = computed(() => closeFilterApplies(this.txTypeFilter()));

  protected readonly dateRangeInvalid = computed(() => dateRangeInvalid(this.filterState()));

  protected readonly hasActiveFilters = computed(
    () =>
      !!(
        this.filterDecisionSource() ||
        this.filterRole() ||
        this.filterSourceClosed() ||
        this.filterAccount() ||
        this.filterDateFrom() ||
        this.filterDateTo()
      ),
  );

  private readonly stockTxSource = computed(() =>
    this.portfolio.summaries().filter((s) => !s.item.isManual),
  );

  protected readonly roleOptions = computed(() =>
    distinctValues(this.stockTxSource().map((s) => s.item.holdingRole)),
  );

  protected readonly hasRoleless = computed(() =>
    this.stockTxSource().some((s) => !(s.item.holdingRole ?? '').trim()),
  );

  protected readonly accountOptions = computed(() =>
    distinctValues([
      ...this.stockTxSource().map((s) => s.item.accountType),
      ...this.optionState.analyses().map((a) => a.item.accountType),
    ]),
  );

  protected readonly sourceOptions = computed(() =>
    distinctValues([
      ...this.stockTxSource().map((s) => s.item.decisionSource),
      ...this.optionState.analyses().map((a) => a.item.decisionSource),
    ]),
  );

  protected readonly sourceClosedOptions = computed(() =>
    distinctValues([
      ...this.stockTxSource().map((s) => s.item.decisionSourceClosed),
      ...this.optionState.analyses().map((a) => a.item.decisionSourceClosed),
    ]),
  );

  protected resetFilters(): void {
    this.filterDecisionSource.set('');
    this.filterRole.set('');
    this.filterSourceClosed.set('');
    this.filterAccount.set('');
    this.filterDateFrom.set('');
    this.filterDateTo.set('');
  }

  // ── Stock transactions sort ─────────────────────────────────────────────────
  protected readonly stockSortCol = signal<StockTxCol>('tx_open_date');
  protected readonly stockSortDir = signal<SortDir>('desc');

  // ── Option transactions sort ────────────────────────────────────────────────
  protected readonly optionSortCol = signal<OptionTxCol>('otx_open_date');
  protected readonly optionSortDir = signal<SortDir>('desc');

  /**
   * Stock table columns, projected for the current breakpoint.
   *
   * Desktop and tablet get exactly the saved layout; a handset gets the primary columns only,
   * because `appResponsiveTable` renders the table as cards there. The MatSort instance is handed
   * to `<app-mobile-table-controls>` through the `#stockSort` template reference, since the header
   * row that normally offers sorting is hidden in card mode.
   */
  protected readonly stockColumns =
    inject(GridColumnStore).getResponsiveColumnKeys('transactions-stocks');

  protected readonly optionColumns = inject(GridColumnStore).getColumnKeys('transactions-options');

  protected readonly sortedStockTransactions = computed(() => {
    const col = this.stockSortCol();
    const dir = this.stockSortDir() === 'asc' ? 1 : -1;
    const state = this.filterState();
    return this.stockTxSource()
      .filter((s) =>
        matchesTx(
          {
            type: s.item.transactionType,
            openDate: s.item.openDate,
            closeDate: s.item.closeDate,
            account: s.item.accountType,
            role: s.item.holdingRole,
            source: s.item.decisionSource,
            sourceClosed: s.item.decisionSourceClosed,
          },
          state,
        ),
      )
      .sort((a, b) =>
        compareSortValues(this.stockSortValue(a, col), this.stockSortValue(b, col), dir),
      );
  });

  protected readonly sortedOptionTransactions = computed(() => {
    const col = this.optionSortCol();
    const dir = this.optionSortDir() === 'asc' ? 1 : -1;
    const state = this.filterState();
    return [...this.optionState.analyses()]
      .filter((a) =>
        matchesTx(
          {
            type: a.item.transactionType,
            openDate: a.item.openDate,
            closeDate: a.item.closeDate,
            account: a.item.accountType,
            source: a.item.decisionSource,
            sourceClosed: a.item.decisionSourceClosed,
          },
          state,
          false, // options carry no role, so the Role filter does not apply to them
        ),
      )
      .sort((a, b) =>
        compareSortValues(this.optionTxSortValue(a, col), this.optionTxSortValue(b, col), dir),
      );
  });

  /** Gain/Loss for a stock row: (closingPrice - avgCost) * shares */
  protected stockGainLoss(s: PortfolioSummary): number | null {
    const cp = s.item.closingPrice;
    if (cp == null) return null;
    return (cp - s.item.averageCostBasis) * s.item.shares;
  }

  /**
   * Age in whole days for a stock transaction.
   * OPEN: today − openDate. CLOSE: closeDate − openDate.
   */
  protected stockTransactionAge(item: PortfolioItem): number | null {
    if (!item.openDate) return null;
    const open = new Date(item.openDate).getTime();
    const end =
      item.transactionType === 'CLOSE' && item.closeDate
        ? new Date(item.closeDate).getTime()
        : Date.now();
    return Math.floor((end - open) / (1000 * 60 * 60 * 24));
  }

  /**
   * Age in whole days for an option transaction.
   * OPEN: today − openDate. CLOSE: closeDate − openDate.
   */
  protected optionTransactionAge(item: OptionItem): number | null {
    if (!item.openDate) return null;
    const open = new Date(item.openDate).getTime();
    const end =
      item.transactionType === 'CLOSE' && item.closeDate
        ? new Date(item.closeDate).getTime()
        : Date.now();
    return Math.floor((end - open) / (1000 * 60 * 60 * 24));
  }

  /** Calendar days since the position was closed (Toronto date); null when not applicable. */
  protected sinceClose(item: PortfolioItem | OptionItem): number | null {
    return daysSinceClose(item.transactionType, item.closeDate, this.todayToronto());
  }

  protected futureClose(item: PortfolioItem | OptionItem): boolean {
    return isFutureClose(item.transactionType, item.closeDate, this.todayToronto());
  }

  /** Gain% for a stock row: gainLoss / (avgCost * shares) */
  protected stockGainPct(s: PortfolioSummary): number | null {
    const gl = this.stockGainLoss(s);
    if (gl == null) return null;
    const cost = s.item.averageCostBasis * s.item.shares;
    return cost === 0 ? 0 : (gl / cost) * 100;
  }

  /** Current MKT Value: shares * last price */
  protected stockMktValue(s: PortfolioSummary): number {
    const price = s.quote?.currentPrice ?? s.item.averageCostBasis;
    return s.item.shares * price;
  }

  /** Last price (current market price) */
  protected stockLastPrice(s: PortfolioSummary): number | null {
    return s.quote?.currentPrice ?? null;
  }

  protected stockCurrency(symbol: string): string {
    return currencyCodeForTradingSymbol(symbol);
  }

  /**
   * Price Diff for a stock row:
   *   CLOSE: closingPrice - lastPrice
   *   OPEN:  lastPrice - avgCost
   */
  protected stockPriceDiff(s: PortfolioSummary): number | null {
    const lastPrice = s.quote?.currentPrice ?? null;
    if (lastPrice === null) return null;
    if (s.item.transactionType === 'CLOSE') {
      return s.item.closingPrice != null ? s.item.closingPrice - lastPrice : null;
    }
    // OPEN: last price vs average cost
    return lastPrice - s.item.averageCostBasis;
  }

  /** Diff $ = Price Diff * Shares */
  protected stockDiffDollar(s: PortfolioSummary): number | null {
    const pd = this.stockPriceDiff(s);
    if (pd === null) return null;
    return pd * s.item.shares;
  }

  /** Trans Date: CLOSE → closeDate, OPEN → openDate */
  protected stockTransDate(item: PortfolioItem): string | null {
    if (item.transactionType === 'CLOSE') return item.closeDate ?? null;
    if (item.transactionType === 'OPEN') return item.openDate ?? null;
    return item.openDate ?? null;
  }

  /** Industry stored on the transaction, falling back to the live quote's industry. */
  protected stockIndustry(s: PortfolioSummary): string {
    return s.item.industry || s.quote?.industry || '';
  }

  /** Export the filtered stock transactions (the same rows as the grid) to CSV. */
  exportStocksCsv(): void {
    this.downloadCsv(
      TX_STOCK_EXPORT_COLUMNS,
      this.sortedStockTransactions().map((s) => this.stockExportRow(s)),
      'transactions',
    );
  }

  /** Export the filtered option transactions to CSV. */
  exportOptionsCsv(): void {
    this.downloadCsv(
      TX_OPTION_EXPORT_COLUMNS,
      this.sortedOptionTransactions().map((a) => this.optionExportRow(a)),
      'option-transactions',
    );
  }

  /** Export stocks, options and the applied filters/totals to a single .xlsx workbook. */
  exportExcel(): void {
    const book = XLSX.utils.book_new();
    const stockRows = this.sortedStockTransactions().map((s) => this.stockExportRow(s));
    XLSX.utils.book_append_sheet(
      book,
      txSheet(TX_STOCK_EXPORT_COLUMNS, stockRows),
      'Stocks',
    );
    const optionRows = this.sortedOptionTransactions().map((a) => this.optionExportRow(a));
    if (optionRows.length > 0) {
      XLSX.utils.book_append_sheet(
        book,
        txSheet(TX_OPTION_EXPORT_COLUMNS, optionRows),
        'Options',
      );
    }
    XLSX.utils.book_append_sheet(book, this.summarySheet(), 'Filters & Totals');
    XLSX.writeFile(book, `transactions-${this.todayToronto()}.xlsx`);
  }

  private summarySheet(): XLSX.WorkSheet {
    const aoa: ExportCell[][] = [['Transactions export'], ...activeFilterSummary(this.filterState())];
    const addTotals = (title: string, t: ReturnType<typeof computeTxTotals>) => {
      aoa.push([], [title], ['Records', t.count], ['Closed records', t.closedCount], ['Open records', t.openCount]);
      for (const c of t.closedGainLoss) aoa.push([`Realized Gain/Loss (${c.currency})`, c.amount]);
      for (const c of t.closedDiff) aoa.push([`Closed DIFF $ (${c.currency})`, c.amount]);
      for (const c of t.openDiff) aoa.push([`Open DIFF $ (${c.currency})`, c.amount]);
    };
    addTotals('Stocks', this.stockTotals());
    addTotals('Options', this.optionTotals());
    aoa.push([], ['Totals are per currency; CAD and USD are never added together.']);
    const sheet = XLSX.utils.aoa_to_sheet(aoa);
    sheet['!cols'] = [{ wch: 38 }, { wch: 22 }];
    return sheet;
  }

  private downloadCsv(
    columns: readonly TxExportColumn[],
    rows: ExportCell[][],
    name: string,
  ): void {
    const blob = new Blob([txRowsToCsv(columns, rows)], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${name}-${this.todayToronto()}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  private stockExportRow(s: PortfolioSummary): ExportCell[] {
    const i = s.item;
    return [
      i.transactionType ?? '',
      i.accountType ?? '',
      i.symbol,
      i.companyName,
      i.shares,
      i.averageCostBasis,
      dateOnly(i.openDate),
      dateOnly(i.closeDate),
      i.closingPrice ?? null,
      this.stockGainLoss(s),
      this.stockGainPct(s),
      this.stockLastPrice(s),
      this.stockPriceDiff(s),
      this.stockDiffDollar(s),
      dateOnly(this.stockTransDate(i)),
      i.decisionSource ?? '',
      i.decisionSourceClosed ?? '',
      i.holdingRole ?? '',
      this.stockIndustry(s),
      this.stockTransactionAge(i),
      this.sinceClose(i),
      this.stockCurrency(i.symbol),
    ];
  }

  private optionExportRow(a: OptionAnalysis): ExportCell[] {
    const i = a.item;
    return [
      i.transactionType ?? '',
      i.accountType ?? '',
      i.underlyingTicker,
      i.positionType,
      dateOnly(i.expirationDate),
      i.strike,
      i.premium,
      i.numberOfContracts,
      dateOnly(i.openDate),
      dateOnly(i.closeDate),
      i.closingPrice ?? null,
      this.optionGainLoss(a),
      this.optionGainPct(a),
      this.optionMktValue(a),
      i.decisionSource ?? '',
      i.decisionSourceClosed ?? '',
      this.optionTransactionAge(i),
      this.sinceClose(i),
      this.stockCurrency(i.underlyingTicker),
    ];
  }

  /** Gain/Loss for an option row: (closingPrice - premium) * contracts * 100 */
  protected optionGainLoss(a: OptionAnalysis): number | null {
    const cp = a.item.closingPrice;
    if (cp == null) return null;
    return (cp - a.item.premium) * a.item.numberOfContracts * 100;
  }

  /** Gain% for an option row */
  protected optionGainPct(a: OptionAnalysis): number | null {
    const gl = this.optionGainLoss(a);
    if (gl == null) return null;
    const cost = a.item.premium * a.item.numberOfContracts * 100;
    return cost === 0 ? 0 : (gl / cost) * 100;
  }

  /** Current MKT Value for option: contracts * 100 * marketPrice */
  protected optionMktValue(a: OptionAnalysis): number {
    return a.item.numberOfContracts * 100 * a.item.marketPrice;
  }

  // ── Totals: every filtered record (the grid is not paginated), per currency ─────────────
  protected readonly stockTotals = computed(() =>
    computeTxTotals(
      this.sortedStockTransactions().map(
        (s): TxTotalsRow => ({
          type: s.item.transactionType,
          currency: this.stockCurrency(s.item.symbol),
          gainLoss: this.stockGainLoss(s),
          diffDollar: this.stockDiffDollar(s),
        }),
      ),
    ),
  );

  protected readonly optionTotals = computed(() =>
    computeTxTotals(
      this.sortedOptionTransactions().map(
        (a): TxTotalsRow => ({
          type: a.item.transactionType,
          currency: this.stockCurrency(a.item.underlyingTicker),
          gainLoss: this.optionGainLoss(a),
          diffDollar: null, // DIFF $ is a stock-only metric
        }),
      ),
    ),
  );

  onStockSortChange(sort: Sort): void {
    if (!sort.active || sort.direction === '') return;
    this.stockSortCol.set(sort.active as StockTxCol);
    this.stockSortDir.set(sort.direction as SortDir);
  }

  onOptionSortChange(sort: Sort): void {
    if (!sort.active || sort.direction === '') return;
    this.optionSortCol.set(sort.active as OptionTxCol);
    this.optionSortDir.set(sort.direction as SortDir);
  }

  private stockSortValue(s: PortfolioSummary, col: StockTxCol): number | string | null {
    switch (col) {
      case 'tx_type':
        return s.item.transactionType ?? '';
      case 'tx_account':
        return s.item.accountType ?? '';
      case 'tx_symbol':
        return s.item.symbol;
      case 'tx_company':
        return s.item.companyName;
      case 'tx_shares':
        return s.item.shares;
      case 'tx_avg_cost':
        return s.item.averageCostBasis;
      case 'tx_open_date':
        return s.item.openDate ?? '';
      case 'tx_close_date':
        return s.item.closeDate ?? '';
      case 'tx_closing_price':
        return s.item.closingPrice ?? 0;
      case 'tx_gain_loss':
        return this.stockGainLoss(s) ?? 0;
      case 'tx_gain_pct':
        return this.stockGainPct(s) ?? 0;
      case 'tx_last_price':
        return s.quote?.currentPrice ?? 0;
      case 'tx_price_diff':
        return this.stockPriceDiff(s) ?? 0;
      case 'tx_diff_dollar':
        return this.stockDiffDollar(s) ?? 0;
      case 'tx_trans_date':
        return this.stockTransDate(s.item) ?? '';
      case 'tx_decision_source':
        return s.item.decisionSource ?? '';
      case 'tx_decision_source_closed':
        return s.item.decisionSourceClosed ?? '';
      case 'tx_role':
        return s.item.holdingRole ?? '';
      case 'tx_industry':
        return this.stockIndustry(s);
      case 'tx_age':
        return this.stockTransactionAge(s.item) ?? 0;
      case 'tx_since_close':
        return this.sinceClose(s.item);
      default:
        return 0;
    }
  }

  private optionTxSortValue(a: OptionAnalysis, col: OptionTxCol): number | string | null {
    switch (col) {
      case 'otx_type':
        return a.item.transactionType ?? '';
      case 'otx_account':
        return a.item.accountType ?? '';
      case 'otx_ticker':
        return a.item.underlyingTicker;
      case 'otx_position':
        return a.item.positionType;
      case 'otx_expiry':
        return a.item.expirationDate;
      case 'otx_strike':
        return a.item.strike;
      case 'otx_premium':
        return a.item.premium;
      case 'otx_contracts':
        return a.item.numberOfContracts;
      case 'otx_open_date':
        return a.item.openDate ?? '';
      case 'otx_close_date':
        return a.item.closeDate ?? '';
      case 'otx_closing_price':
        return a.item.closingPrice ?? 0;
      case 'otx_gain_loss':
        return this.optionGainLoss(a) ?? 0;
      case 'otx_gain_pct':
        return this.optionGainPct(a) ?? 0;
      case 'otx_mkt_value':
        return this.optionMktValue(a);
      case 'otx_decision_source':
        return a.item.decisionSource ?? '';
      case 'otx_age':
        return this.optionTransactionAge(a.item) ?? 0;
      case 'otx_since_close':
        return this.sinceClose(a.item);
      default:
        return 0;
    }
  }

  // ── Stock Edit / Remove ────────────────────────────────────────────────────
  openEditStockTransaction(s: PortfolioSummary): void {
    this.dialog.open(EditPositionDialogComponent, {
      data: {
        item: s.item,
        submit: (result: EditPositionDialogResult) =>
          new Promise<string | null>((resolve) =>
            this.portfolio.updateItem(
              s.item.id,
              {
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
                decisionSource: result.decisionSource,
                decisionSourceClosed: result.decisionSourceClosed,
                holdingRole: result.holdingRole,
              },
              { success: () => resolve(null), failure: (message) => resolve(message) },
            ),
          ),
      } satisfies EditPositionDialogData,
      width: '620px',
      maxWidth: '95vw',
      maxHeight: '95vh',
    });
  }

  confirmRemoveStockTransaction(s: PortfolioSummary): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Remove Transaction',
          message: `Permanently delete transaction for ${s.item.symbol} (${s.item.companyName})? This cannot be undone.`,
          confirmLabel: 'Delete',
          danger: true,
        } satisfies ConfirmDialogData,
        width: '400px',
      })
      .afterClosed()
      .subscribe((confirmed: boolean) => {
        if (confirmed) this.portfolio.deleteItem(s.item.id, s.item.symbol);
      });
  }

  // ── Option Edit / Remove ───────────────────────────────────────────────────
  openEditOptionTransaction(analysis: OptionAnalysis): void {
    this.dialog.open(EditOptionDialogComponent, {
      data: { item: analysis.item } satisfies EditOptionDialogData,
      width: '620px',
      maxWidth: '95vw',
      maxHeight: '95vh',
    });
  }

  confirmRemoveOptionTransaction(analysis: OptionAnalysis): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Remove Option Transaction',
          message: `Permanently delete option transaction for ${analysis.item.underlyingTicker} (${analysis.item.positionType})? This cannot be undone.`,
          confirmLabel: 'Delete',
          danger: true,
        } satisfies ConfirmDialogData,
        width: '400px',
      })
      .afterClosed()
      .subscribe((confirmed: boolean) => {
        if (confirmed) this.optionState.deleteItem(analysis.item.id);
      });
  }

  openStockNotes(s: PortfolioSummary): void {
    this.dialog
      .open(TransactionNotesDialogComponent, {
        data: { symbol: s.item.symbol, notes: s.item.notes } satisfies TransactionNotesDialogData,
        width: '480px',
      })
      .afterClosed()
      .subscribe((result: TransactionNotesDialogResult | undefined) => {
        if (result === undefined) return;
        this.api.updatePortfolioNotes(s.item.id, result.notes).subscribe();
        this.portfolio.patchItemNotes(s.item.id, result.notes);
      });
  }

  openOptionNotes(analysis: OptionAnalysis): void {
    this.dialog
      .open(TransactionNotesDialogComponent, {
        data: {
          symbol: analysis.item.underlyingTicker,
          notes: analysis.item.notes,
        } satisfies TransactionNotesDialogData,
        width: '480px',
      })
      .afterClosed()
      .subscribe((result: TransactionNotesDialogResult | undefined) => {
        if (result === undefined) return;
        this.api.updateOptionNotes(analysis.item.id, result.notes).subscribe();
        this.optionState.patchItemNotes(analysis.item.id, result.notes);
      });
  }
}
