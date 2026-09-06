import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { CashStateService } from '../../core/services/cash-state.service';
import { PortfolioValueHistoryStateService } from '../../core/services/portfolio-value-history-state.service';
import { AdminActionsComponent } from './admin-actions/admin-actions.component';
import { CashLedgerTableComponent } from './cash-ledger-table/cash-ledger-table.component';
import { HistoryFilterBarComponent } from './history-filter-bar/history-filter-bar.component';
import { HistorySummaryCardsComponent } from './history-summary-cards/history-summary-cards.component';
import { SnapshotsTableComponent } from './snapshots-table/snapshots-table.component';

@Component({
  selector: 'app-portfolio-value-history-page',
  templateUrl: './portfolio-value-history-page.component.html',
  styleUrl: './portfolio-value-history-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatTabsModule,
    HistorySummaryCardsComponent,
    HistoryFilterBarComponent,
    SnapshotsTableComponent,
    CashLedgerTableComponent,
    AdminActionsComponent,
  ],
})
export class PortfolioValueHistoryPageComponent {
  protected readonly historyState = inject(PortfolioValueHistoryStateService);
  protected readonly cashState = inject(CashStateService);
}
