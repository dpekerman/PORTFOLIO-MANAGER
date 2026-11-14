import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { firstValueFrom } from 'rxjs';
import { SwingTradeRow } from '../models/swing-trading.models';
import { PortfolioItem } from '../models/portfolio.models';
import { PortfolioStore } from '../store/portfolio.store';
import {
  EditPositionDialogComponent,
  EditPositionDialogData,
  EditPositionDialogResult,
} from '../../features/portfolio/edit-position-dialog/edit-position-dialog.component';
import { CloseLotPickerDialogComponent } from '../../features/swing-trading/close-lot-picker-dialog.component';
import {
  CLOSE_POSITION_DEFAULT_SOURCE,
  eligibleCloseLots,
  eligibleCloseLotsForSymbol,
} from '../../features/swing-trading/swing-trading-logic';

/**
 * Close Position shortcut for Swing Trading. It only opens the existing Edit Position dialog on one real OPEN lot;
 * the CLOSE itself is the existing `PortfolioStore.updateItem` -> `PortfolioService.UpdateAsync` path, so partial
 * closes, trade-episode and broker-stop handling are unchanged. Nothing is recorded until the user submits.
 */
@Injectable({ providedIn: 'root' })
export class SwingClosePositionService {
  private readonly dialog = inject(MatDialog);
  private readonly portfolio = inject(PortfolioStore);
  private readonly snackBar = inject(MatSnackBar);
  private busy = false;

  hasEligibleLot(row: SwingTradeRow): boolean {
    return this.swingLots(row).length > 0;
  }

  /** Signal Tracker: true when this exact security has at least one currently open, closable lot in any account. */
  hasEligibleLotForSymbol(symbol: string): boolean {
    return this.symbolLots(symbol).length > 0;
  }

  async open(row: SwingTradeRow): Promise<void> {
    await this.run(row.symbol, row.account, () => this.swingLots(row), 'No open Swing lot is available to close for this position.');
  }

  /** Signal Tracker entry point: the same picker, Edit Position dialog and update path, over current holdings. */
  async openForSymbol(symbol: string): Promise<void> {
    await this.run(symbol, null, () => this.symbolLots(symbol), 'No open lot is available to close for this security.', null);
  }

  private swingLots(row: SwingTradeRow): PortfolioItem[] {
    return eligibleCloseLots(row, this.portfolio.summaries().map((s) => s.item));
  }

  private symbolLots(symbol: string): PortfolioItem[] {
    return eligibleCloseLotsForSymbol(symbol, this.portfolio.summaries().map((s) => s.item));
  }

  private async run(
    symbol: string,
    account: string | null,
    currentLots: () => PortfolioItem[],
    emptyMessage: string,
    defaultSource: string | null = CLOSE_POSITION_DEFAULT_SOURCE,
  ): Promise<void> {
    if (this.busy) return;
    const lots = currentLots();
    if (lots.length === 0) {
      this.snackBar.open(emptyMessage, 'Close', { duration: 4000 });
      return;
    }
    // More than one lot (or account) is never resolved silently: the user must pick one.
    let lot: PortfolioItem = lots[0];
    if (lots.length > 1) {
      const multiAccount = new Set(lots.map((l) => l.accountType ?? '')).size > 1;
      const id = await firstValueFrom(
        this.dialog
          .open<CloseLotPickerDialogComponent, unknown, number>(CloseLotPickerDialogComponent, {
            width: '420px',
            maxWidth: '95vw',
            data: {
              symbol,
              account: multiAccount ? null : account ?? lots[0].accountType ?? null,
              lots: lots.map((l) => ({
                id: l.id,
                openDate: l.openDate ?? null,
                shares: l.shares,
                account: multiAccount ? l.accountType ?? null : null,
              })),
            },
          })
          .afterClosed(),
        { defaultValue: undefined },
      );
      if (id === undefined) return;
      lot = lots.find((l) => l.id === id) ?? lot;
    }
    this.openCloseDialog(currentLots, lot, defaultSource);
  }

  private openCloseDialog(currentLots: () => PortfolioItem[], lot: PortfolioItem, defaultSource: string | null): void {
    this.dialog.open(EditPositionDialogComponent, {
      data: {
        item: lot,
        closePosition: { defaultDecisionSourceClosed: defaultSource },
        submit: (result: EditPositionDialogResult) => this.submit(currentLots, lot, result),
      } satisfies EditPositionDialogData,
      width: '620px',
      maxWidth: '95vw',
      maxHeight: '95vh',
    });
  }

  private submit(currentLots: () => PortfolioItem[], lot: PortfolioItem, result: EditPositionDialogResult): Promise<string | null> {
    if (this.busy) return Promise.resolve('A close is already being saved.');
    // Re-confirm identity and eligibility against the latest data immediately before saving.
    const current = currentLots().find((l) => l.id === lot.id);
    if (!current || current.shares !== lot.shares) {
      return Promise.resolve('This lot changed since the dialog opened. Close it and reopen Close Position.');
    }
    this.busy = true;
    return new Promise<string | null>((resolve) =>
      this.portfolio.updateItem(
        lot.id,
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
        {
          success: () => {
            this.busy = false;
            resolve(null);
          },
          failure: (message) => {
            this.busy = false;
            resolve(message);
          },
        },
      ),
    );
  }
}
