import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTabChangeEvent, MatTabsModule } from '@angular/material/tabs';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { SwingTradingStore } from '../../core/store/swing-trading.store';
import { OpenTradesTabComponent } from './open-trades-tab.component';
import { RiskMonitorTabComponent } from './risk-monitor-tab.component';
import { TradeJournalTabComponent } from './trade-journal-tab.component';

/** Swing Trading workspace: Open Trades, Risk Monitor and Trade Journal over the shared Swing store. */
@Component({
  selector: 'app-swing-trading-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    MatButtonModule,
    MatIconModule,
    MatTabsModule,
    OpenTradesTabComponent,
    RiskMonitorTabComponent,
    TradeJournalTabComponent,
  ],
  templateUrl: './swing-trading-page.component.html',
  styleUrl: './swing-trading-page.component.scss',
})
export class SwingTradingPageComponent {
  protected readonly store = inject(SwingTradingStore);
  private readonly demo = inject(DemoModeStore);

  protected readonly tab = signal(0);
  protected readonly summary = this.store.summary;

  protected readonly totals = computed(() =>
    (this.summary()?.totals ?? []).map((t) => ({
      currency: t.currency,
      marketValue: this.demo.maskValue(t.marketValue),
      pnl: this.demo.maskValue(t.unrealizedPnl),
      pnlPct: this.demo.maskPercent(t.unrealizedPnlPct),
      positive: t.unrealizedPnl > 0,
      negative: t.unrealizedPnl < 0,
    })),
  );

  protected onTabChange(event: MatTabChangeEvent): void {
    this.tab.set(event.index);
    if (event.index === 2 && !this.store.journalLoaded()) this.store.loadJournal();
  }

  protected refresh(): void {
    this.store.refresh(this.tab() === 2);
  }
}
