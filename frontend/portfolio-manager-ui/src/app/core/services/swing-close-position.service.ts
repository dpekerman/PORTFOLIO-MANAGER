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
    return eligibleCloseLots(row, this.portfolio.summaries().map((s) => s.item)).length > 0;
  }

  async open(row: SwingTradeRow): Promise<void> {
    if (this.busy) return;
    const lots = eligibleCloseLots(row, this.portfolio.summaries().map((s) => s.item));
    if (lots.length === 0) {
      this.snackBar.open('No open Swing lot is available to close for this position.', 'Close', { duration: 4000 });
      return;
    }
    let lot: PortfolioItem = lots[0];
    if (lots.length > 1) {
      const id = await firstValueFrom(
        this.dialog
          .open<CloseLotPickerDialogComponent, unknown, number>(CloseLotPickerDialogComponent, {
            width: '420px',
            maxWidth: '95vw',
            data: {
              symbol: row.symbol,
              account: row.account,
              lots: lots.map((l) => ({ id: l.id, openDate: l.openDate ?? null, shares: l.shares })),
            },
          })
          .afterClosed(),
        { defaultValue: undefined },
      );
      if (id === undefined) return;
      lot = lots.find((l) => l.id === id) ?? lot;
    }
    this.openCloseDialog(row, lot);
  }

  private openCloseDialog(row: SwingTradeRow, lot: PortfolioItem): void {
    this.dialog.open(EditPositionDialogComponent, {
      data: {
        item: lot,
        closePosition: { defaultDecisionSourceClosed: CLOSE_POSITION_DEFAULT_SOURCE },
        submit: (result: EditPositionDialogResult) => this.submit(row, lot, result),
      } satisfies EditPositionDialogData,
      width: '620px',
      maxWidth: '95vw',
      maxHeight: '95vh',
    });
  }

  private submit(row: SwingTradeRow, lot: PortfolioItem, result: EditPositionDialogResult): Promise<string | null> {
    if (this.busy) return Promise.resolve('A close is already being saved.');
    // Re-confirm identity and eligibility against the latest data immediately before saving.
    const current = eligibleCloseLots(row, this.portfolio.summaries().map((s) => s.item)).find(
      (l) => l.id === lot.id,
    );
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
