import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { PortfolioValueHistoryDto } from '../../../core/models/portfolio.models';
import { DemoModeService } from '../../../core/services/demo-mode.service';

@Component({
  selector: 'app-history-summary-cards',
  templateUrl: './history-summary-cards.component.html',
  styleUrl: './history-summary-cards.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatCardModule, MatIconModule, CurrencyPipe, DatePipe],
})
export class HistorySummaryCardsComponent {
  protected readonly demoMode = inject(DemoModeService);

  readonly latestSnapshot = input<PortfolioValueHistoryDto | null>(null);
  readonly loading = input(false);

  protected readonly displayTotalValue = computed(() =>
    this.demoMode.maskValue(this.latestSnapshot()?.totalValue ?? 0),
  );
  protected readonly displayCashValue = computed(() =>
    this.demoMode.maskValue(this.latestSnapshot()?.cashValue ?? 0),
  );
  protected readonly displayExternalFlow = computed(() =>
    this.demoMode.maskValue(this.latestSnapshot()?.externalCashFlow ?? 0),
  );

  /** Last resealed/recalculated time if this row was ever recomputed, else its original recorded time. */
  protected readonly lastSnapshotTime = computed(() => {
    const s = this.latestSnapshot();
    if (!s) return null;
    return s.lastRecalculatedAt ?? s.recordedAt;
  });

  protected readonly lastSnapshotLabel = computed(() =>
    this.latestSnapshot()?.lastRecalculatedAt ? 'Last Resealed' : 'Last Recorded',
  );
}
