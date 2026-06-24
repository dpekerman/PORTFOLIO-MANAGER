import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { OptionAnalysis, PortfolioSummary } from '../../core/models/portfolio.models';
import { DemoModeService } from '../../core/services/demo-mode.service';
import { OptionStateService } from '../../core/services/option-state.service';
import { PortfolioApiService } from '../../core/services/portfolio-api.service';
import { PortfolioStateService } from '../../core/services/portfolio-state.service';
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
  | 'tx_mkt_value'
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
  | 'otx_actions';

@Component({
  selector: 'app-transactions-page',
  templateUrl: './transactions-page.component.html',
  styleUrl: './transactions-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CurrencyPipe,
    DatePipe,
    DecimalPipe,
    MatButtonModule,
    MatIconModule,
    MatSortModule,
    MatTableModule,
    MatTooltipModule,
  ],
})
export class TransactionsPageComponent {
  protected readonly portfolio = inject(PortfolioStateService);
  protected readonly optionState = inject(OptionStateService);
  protected readonly demoMode = inject(DemoModeService);
  private readonly api = inject(PortfolioApiService);
  private readonly dialog = inject(MatDialog);

  // ── Section collapse ────────────────────────────────────────────────────────
  protected readonly stocksExpanded = signal(true);
  protected readonly optionsExpanded = signal(true);

  // ── Stock transactions sort ─────────────────────────────────────────────────
  protected readonly stockSortCol = signal<StockTxCol>('tx_open_date');
  protected readonly stockSortDir = signal<SortDir>('desc');

  // ── Option transactions sort ────────────────────────────────────────────────
  protected readonly optionSortCol = signal<OptionTxCol>('otx_open_date');
  protected readonly optionSortDir = signal<SortDir>('desc');

  protected readonly stockColumns: string[] = [
    'tx_type',
    'tx_account',
    'tx_symbol',
    'tx_company',
    'tx_shares',
    'tx_avg_cost',
    'tx_open_date',
    'tx_close_date',
    'tx_closing_price',
    'tx_gain_loss',
    'tx_gain_pct',
    'tx_mkt_value',
    'tx_actions',
  ];

  protected readonly optionColumns: string[] = [
    'otx_type',
    'otx_account',
    'otx_ticker',
    'otx_position',
    'otx_expiry',
    'otx_strike',
    'otx_premium',
    'otx_contracts',
    'otx_open_date',
    'otx_close_date',
    'otx_closing_price',
    'otx_gain_loss',
    'otx_gain_pct',
    'otx_mkt_value',
    'otx_actions',
  ];

  protected readonly sortedStockTransactions = computed(() => {
    const col = this.stockSortCol();
    const dir = this.stockSortDir() === 'asc' ? 1 : -1;
    return [...this.portfolio.summaries()]
      .filter((s) => !s.item.isManual && s.item.transactionType === 'CLOSE')
      .sort((a, b) => {
        const av = this.stockSortValue(a, col);
        const bv = this.stockSortValue(b, col);
        if (typeof av === 'number' && typeof bv === 'number') return (av - bv) * dir;
        return String(av ?? '').localeCompare(String(bv ?? '')) * dir;
      });
  });

  protected readonly sortedOptionTransactions = computed(() => {
    const col = this.optionSortCol();
    const dir = this.optionSortDir() === 'asc' ? 1 : -1;
    return [...this.optionState.analyses()]
      .filter((a) => a.item.transactionType === 'CLOSE')
      .sort((a, b) => {
        const av = this.optionTxSortValue(a, col);
        const bv = this.optionTxSortValue(b, col);
        if (typeof av === 'number' && typeof bv === 'number') return (av - bv) * dir;
        return String(av ?? '').localeCompare(String(bv ?? '')) * dir;
      });
  });

  /** Gain/Loss for a stock row: (closingPrice - avgCost) * shares */
  protected stockGainLoss(s: { item: any; quote: any }): number | null {
    const cp = s.item.closingPrice;
    if (cp == null) return null;
    return (cp - s.item.averageCostBasis) * s.item.shares;
  }

  /** Gain% for a stock row: gainLoss / (avgCost * shares) */
  protected stockGainPct(s: { item: any; quote: any }): number | null {
    const gl = this.stockGainLoss(s);
    if (gl == null) return null;
    const cost = s.item.averageCostBasis * s.item.shares;
    return cost === 0 ? 0 : (gl / cost) * 100;
  }

  /** Current MKT Value: shares * last price */
  protected stockMktValue(s: { item: any; quote: any }): number {
    const price = s.quote?.currentPrice ?? s.item.averageCostBasis;
    return s.item.shares * price;
  }

  /** Gain/Loss for an option row: (closingPrice - premium) * contracts * 100 */
  protected optionGainLoss(a: any): number | null {
    const cp = a.item.closingPrice;
    if (cp == null) return null;
    return (cp - a.item.premium) * a.item.numberOfContracts * 100;
  }

  /** Gain% for an option row */
  protected optionGainPct(a: any): number | null {
    const gl = this.optionGainLoss(a);
    if (gl == null) return null;
    const cost = a.item.premium * a.item.numberOfContracts * 100;
    return cost === 0 ? 0 : (gl / cost) * 100;
  }

  /** Current MKT Value for option: contracts * 100 * marketPrice */
  protected optionMktValue(a: any): number {
    return a.item.numberOfContracts * 100 * a.item.marketPrice;
  }

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

  private stockSortValue(s: { item: any; quote: any }, col: StockTxCol): number | string | null {
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
      case 'tx_mkt_value':
        return this.stockMktValue(s);
      default:
        return 0;
    }
  }

  private optionTxSortValue(a: any, col: OptionTxCol): number | string | null {
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
      default:
        return 0;
    }
  }

  // ── Stock Edit / Remove ────────────────────────────────────────────────────
  openEditStockTransaction(s: PortfolioSummary): void {
    this.dialog
      .open(EditPositionDialogComponent, {
        data: { item: s.item } satisfies EditPositionDialogData,
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
