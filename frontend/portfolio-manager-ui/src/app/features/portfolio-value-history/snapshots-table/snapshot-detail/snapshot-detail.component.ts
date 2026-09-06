import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { CashItem, PortfolioValueHistoryDto } from '../../../../core/models/portfolio.models';
import { DemoModeService } from '../../../../core/services/demo-mode.service';

@Component({
  selector: 'app-snapshot-detail',
  templateUrl: './snapshot-detail.component.html',
  styleUrl: './snapshot-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CurrencyPipe, DatePipe, MatIconModule],
})
export class SnapshotDetailComponent {
  protected readonly demoMode = inject(DemoModeService);

  readonly snapshot = input.required<PortfolioValueHistoryDto>();
  readonly cashActivity = input<CashItem[]>([]);

  protected readonly reasonText = computed(() => {
    const s = this.snapshot();
    switch (s.snapshotStatus) {
      case 'Original':
        return 'Original EOD snapshot — never recalculated.';
      case 'PendingReseal':
        return 'Same-day cash mutation — waiting for the 90-second quiet period before resealing.';
      case 'Resealed':
        return 'Same-day cash mutation.';
      case 'CashRecalculated':
        return 'Backdated cash correction.';
      default:
        return '';
    }
  });

  protected dv(value: number): number {
    return this.demoMode.maskValue(value);
  }
}
