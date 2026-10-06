import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { firstValueFrom } from 'rxjs';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../shared/confirm-dialog/confirm-dialog.component';
import {
  LinkCashDialogComponent,
  LinkCashDialogData,
  LinkCashDialogResult,
  easternToday,
} from '../../shared/link-cash-dialog/link-cash-dialog.component';
import {
  CashItem,
  OptionItem,
  PortfolioItem,
  TradeLinkSourceType,
  UnlinkedTrade,
} from '../models/portfolio.models';
import { CashStateService } from './cash-state.service';
import { DashboardStateService } from './dashboard-state.service';
import { DemoModeService } from './demo-mode.service';
import { PortfolioApiService } from './portfolio-api.service';

/** Everything needed to offer (or correct) the cash row of one trade leg. */
interface TradeLegSpec {
  sourceType: TradeLinkSourceType;
  sourceItemId: number;
  heading: string;
  /** e.g. "500 sh" or "10 contracts". */
  quantity: string;
  /** Per-share / per-contract price; null when it is unknown (e.g. a close without a closing price). */
  price: number | null;
  /** Null when the price needed to compute it is missing. */
  amount: number | null;
  accountType: string | null;
  tradeDate: string | null;
  /** A leg that has just come into existence (new trade, or an open position becoming closed). */
  isNewLeg: boolean;
  /** A cash-relevant field (size, price, account) differs from before the edit. */
  costFieldsChanged: boolean;
  /** Pre-fill the cash row with the trade's own date instead of today (catching up an old trade). */
  useTradeDate?: boolean;
}

const OPTION_MULTIPLIER = 100;
const usd = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' });

/**
 * Offers the cash-ledger side of a stock/option trade after it is saved, so buys and sells move Cash the
 * same way they move Stocks/Options. Always a confirm step ("Skip" keeps today's manual workflow), and
 * never nags on edits that don't touch size, price or account.
 */
@Injectable({ providedIn: 'root' })
export class TradeCashLinkService {
  private readonly api = inject(PortfolioApiService);
  private readonly cashState = inject(CashStateService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly demoMode = inject(DemoModeService);
  private readonly dashboardState = inject(DashboardStateService);

  /** Offers the missing cash row for a trade leg that was saved without one (from the "Unlinked trades" list). */
  linkUnlinkedTrade(t: UnlinkedTrade): Promise<void> {
    return this.offer({
      sourceType: t.sourceType,
      sourceItemId: t.sourceItemId,
      heading: t.label,
      quantity: t.quantity,
      price: t.price,
      amount: t.amount,
      accountType: t.accountType,
      tradeDate: this.dateOnly(t.tradeDate),
      isNewLeg: true,
      costFieldsChanged: false,
      useTradeDate: true,
    });
  }

  /** A partial close splits the purchase cash row server-side; reload the ledger so it shows both rows. */
  onPartialClose(): void {
    this.cashState.refresh();
    this.dashboardState.refresh();
  }

  /** prev is null for a brand-new position, otherwise the row as it was before the edit. */
  offerForStock(prev: PortfolioItem | null, item: PortfolioItem): Promise<void> {
    if (item.isManual) return Promise.resolve();
    const isClose = item.transactionType === 'CLOSE';
    const price = isClose ? item.closingPrice : item.averageCostBasis;
    const changed =
      prev !== null &&
      (prev.shares !== item.shares ||
        prev.averageCostBasis !== item.averageCostBasis ||
        (prev.closingPrice ?? null) !== (item.closingPrice ?? null) ||
        (prev.accountType ?? null) !== (item.accountType ?? null));
    return this.offer({
      sourceType: isClose ? 'PortfolioClose' : 'PortfolioOpen',
      sourceItemId: item.id,
      heading: `${isClose ? 'Sell' : 'Buy'} ${item.symbol}`,
      quantity: `${item.shares} sh`,
      price: price ?? null,
      amount: price == null ? null : this.round2(item.shares * price),
      accountType: item.accountType ?? null,
      tradeDate: this.dateOnly(isClose ? item.closeDate : item.openDate),
      isNewLeg: prev === null || (isClose && prev.transactionType !== 'CLOSE'),
      costFieldsChanged: changed,
    });
  }

  offerForOption(prev: OptionItem | null, item: OptionItem): Promise<void> {
    const isClose = item.transactionType === 'CLOSE';
    const price = isClose ? item.closingPrice : item.premium;
    const changed =
      prev !== null &&
      (prev.numberOfContracts !== item.numberOfContracts ||
        prev.premium !== item.premium ||
        (prev.closingPrice ?? null) !== (item.closingPrice ?? null) ||
        (prev.accountType ?? null) !== (item.accountType ?? null));
    return this.offer({
      sourceType: isClose ? 'OptionClose' : 'OptionOpen',
      sourceItemId: item.id,
      heading: `${isClose ? 'Sell' : 'Buy'} ${item.underlyingTicker} ${item.positionType} $${item.strike}`,
      quantity: `${item.numberOfContracts} contract${item.numberOfContracts === 1 ? '' : 's'}`,
      price: price ?? null,
      amount:
        price == null ? null : this.round2(item.numberOfContracts * price * OPTION_MULTIPLIER),
      accountType: item.accountType ?? null,
      tradeDate: this.dateOnly(isClose ? item.closeDate : item.openDate),
      isNewLeg: prev === null || (isClose && prev.transactionType !== 'CLOSE'),
      costFieldsChanged: changed,
    });
  }

  /** After a position is deleted: offer to remove the cash rows that were linked to it. */
  offerRemoveForStock(id: number, label: string): Promise<void> {
    return this.offerRemove(['PortfolioOpen', 'PortfolioClose'], id, label);
  }

  offerRemoveForOption(id: number, label: string): Promise<void> {
    return this.offerRemove(['OptionOpen', 'OptionClose'], id, label);
  }

  private async offer(spec: TradeLegSpec): Promise<void> {
    // Nothing cash-relevant happened (e.g. an inline decision-source or market-price edit) — skip the lookup.
    if (!spec.isNewLeg && !spec.costFieldsChanged) return;
    try {
      const existing = await firstValueFrom(
        this.api.getLinkedCash(spec.sourceType, spec.sourceItemId),
      );

      if (existing) {
        if (!spec.costFieldsChanged || spec.amount === null || !this.mismatch(existing, spec))
          return;
        const result = await this.askUser(this.dialogData('update', spec, existing));
        if (!result) return;
        await this.cashState.updateItem(existing.id, {
          description: result.description,
          amount: result.amount,
          cashFlowType: existing.cashFlowType!,
          accountType: result.accountType,
          transactionDate: result.transactionDate,
        });
        this.dashboardState.refresh();
        return;
      }

      // Plain edits to a position that was never linked (all pre-existing ones) never prompt.
      if (!spec.isNewLeg) return;

      const result = await this.askUser(this.dialogData('create', spec, null));
      if (!result) return;
      await this.cashState.addLinked({
        sourceType: spec.sourceType,
        sourceItemId: spec.sourceItemId,
        amount: result.amount,
        description: result.description,
        accountType: result.accountType,
        transactionDate: result.transactionDate,
      });
      this.snackBar.open('Cash entry linked to trade', 'Dismiss', { duration: 3000 });
      this.dashboardState.refresh();
    } catch (err) {
      // Failed writes already showed their own snackbar; a failed lookup must never block the trade save.
      console.warn('Trade cash link skipped', err);
    }
  }

  private async offerRemove(
    types: TradeLinkSourceType[],
    id: number,
    label: string,
  ): Promise<void> {
    try {
      const found = await Promise.all(
        types.map((t) => firstValueFrom(this.api.getLinkedCash(t, id))),
      );
      const links = found.filter((c): c is CashItem => c !== null);
      if (links.length === 0) return;

      const lines = links
        .map(
          (c) => `${c.cashFlowType} ${this.money(c.amount)} on ${this.dateOnly(c.transactionDate)}`,
        )
        .join('; ');
      const data: ConfirmDialogData = {
        title: 'Remove linked cash?',
        message: `${label} had ${links.length === 1 ? 'a linked cash entry' : 'linked cash entries'} (${lines}). Remove ${
          links.length === 1 ? 'it' : 'them'
        } too so Cash stays accurate?`,
        confirmLabel: 'Remove cash',
        cancelLabel: 'Keep',
      };
      const confirmed = await firstValueFrom(
        this.dialog
          .open(ConfirmDialogComponent, { data, width: '460px', maxWidth: '95vw' })
          .afterClosed(),
      );
      if (confirmed) {
        await this.cashState.removeItems(links.map((c) => c.id));
        this.dashboardState.refresh();
      }
    } catch (err) {
      console.warn('Linked cash removal skipped', err);
    }
  }

  private dialogData(
    mode: 'create' | 'update',
    spec: TradeLegSpec,
    existing: CashItem | null,
  ): LinkCashDialogData {
    const direction = spec.sourceType.endsWith('Open') ? 'out' : 'in';
    const priceText = (fmt: (v: number) => string) => (spec.price === null ? '—' : fmt(spec.price));
    return {
      mode,
      direction,
      heading: spec.heading,
      detail: `${spec.quantity} @ ${priceText((v) => this.money(v))}`,
      amount: spec.amount,
      accountType: spec.accountType,
      tradeDate: spec.tradeDate,
      defaultDate: spec.useTradeDate ? spec.tradeDate : null,
      // Stored text: always the real figures, never the demo-masked ones.
      description:
        existing?.description ??
        `${spec.heading} — ${spec.quantity} @ ${priceText((v) => usd.format(v))}`,
      existing,
      duplicate: mode === 'create' ? this.findDuplicate(spec, direction) : null,
    };
  }

  private askUser(data: LinkCashDialogData): Promise<LinkCashDialogResult | undefined> {
    return firstValueFrom(
      this.dialog
        .open<
          LinkCashDialogComponent,
          LinkCashDialogData,
          LinkCashDialogResult
        >(LinkCashDialogComponent, { data, width: '480px', maxWidth: '95vw', maxHeight: '95vh', autoFocus: false })
        .afterClosed(),
    );
  }

  private mismatch(existing: CashItem, spec: TradeLegSpec): boolean {
    return (
      Math.abs(Math.abs(existing.amount) - spec.amount!) >= 0.01 ||
      (existing.accountType ?? null) !== spec.accountType
    );
  }

  /** A hand-entered ledger row that already records this trade (same direction/account/amount, ±1 day). */
  private findDuplicate(spec: TradeLegSpec, direction: 'out' | 'in'): CashItem | null {
    if (spec.amount === null) return null;
    const type = direction === 'out' ? 'TradePurchase' : 'TradeProceeds';
    const tolerance = Math.max(1, spec.amount * 0.01);
    const anchors = [spec.tradeDate, easternToday()].filter((d): d is string => !!d);
    return (
      this.cashState
        .items()
        .find(
          (c) =>
            !c.sourceType &&
            c.cashFlowType === type &&
            (c.accountType ?? null) === spec.accountType &&
            Math.abs(Math.abs(c.amount) - spec.amount!) <= tolerance &&
            anchors.some((a) => this.dayGap(c.transactionDate, a) <= 1),
        ) ?? null
    );
  }

  private dayGap(a: string | null | undefined, b: string): number {
    const day = this.dateOnly(a);
    if (!day) return Number.POSITIVE_INFINITY;
    const ms = Math.abs(
      new Date(`${day}T12:00:00Z`).getTime() - new Date(`${b}T12:00:00Z`).getTime(),
    );
    return ms / 86_400_000;
  }

  /** Currency text for on-screen messages, honouring demo mode (blur has no text form, so it hides the figure). */
  private money(v: number): string {
    if (!this.demoMode.isDemoMode()) return usd.format(v);
    return this.demoMode.demoStyle() === 'fake' ? usd.format(this.demoMode.maskValue(v)) : '••••';
  }

  private dateOnly(value: string | null | undefined): string | null {
    return value ? value.split('T')[0] : null;
  }

  private round2(n: number): number {
    return Math.round(n * 100) / 100;
  }
}
