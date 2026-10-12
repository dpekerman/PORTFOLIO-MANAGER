import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, signal, untracked } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { UnlinkedTrade } from '../../../core/models/portfolio.models';
import { AuthStore } from '../../../core/store/auth.store';
import { CashStateService } from '../../../core/services/cash-state.service';
import { DemoModeStore } from '../../../core/store/demo-mode.store';
import { TradeCashLinkService } from '../../../core/services/trade-cash-link.service';

@Component({
  selector: 'app-unlinked-trades',
  templateUrl: './unlinked-trades.component.html',
  styleUrl: './unlinked-trades.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, MatTooltipModule, CurrencyPipe, DatePipe],
})
export class UnlinkedTradesComponent {
  protected readonly cashState = inject(CashStateService);
  protected readonly authState = inject(AuthStore);
  protected readonly demoMode = inject(DemoModeStore);
  private readonly tradeCashLink = inject(TradeCashLinkService);
  protected readonly expanded = signal<ReadonlySet<string>>(new Set());

  protected key(t: UnlinkedTrade): string {
    return t.sourceType + t.sourceItemId;
  }

  protected toggle(t: UnlinkedTrade): void {
    const k = this.key(t);
    this.expanded.update((s) => {
      const next = new Set(s);
      if (!next.delete(k)) next.add(k);
      return next;
    });
  }

  constructor() {
    // Any change to the cash ledger (a link was added/removed, a row edited) can change what is unlinked.
    effect(() => {
      this.cashState.items();
      if (this.cashState.loading()) return;
      untracked(() => this.cashState.loadUnlinkedTrades());
    });
  }

  protected dv(value: number): number {
    return this.demoMode.maskValue(value);
  }

  protected isBuy(t: UnlinkedTrade): boolean {
    return t.sourceType.endsWith('Open');
  }

  protected link(t: UnlinkedTrade): void {
    void this.tradeCashLink.linkUnlinkedTrade(t);
  }

  protected reload(): void {
    this.cashState.loadUnlinkedTrades();
  }
}
