import { CurrencyPipe, DecimalPipe, NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { CashStateService } from '../../../core/services/cash-state.service';
import { DemoModeService } from '../../../core/services/demo-mode.service';
import { OptionStateService } from '../../../core/services/option-state.service';
import { PortfolioApiService } from '../../../core/services/portfolio-api.service';
import { PortfolioStateService } from '../../../core/services/portfolio-state.service';

@Component({
  selector: 'app-portfolio-summary-bar',
  templateUrl: './portfolio-summary-bar.component.html',
  styleUrl: './portfolio-summary-bar.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatCardModule, MatIconModule, CurrencyPipe, DecimalPipe, NgClass, MatTooltipModule],
})
export class PortfolioSummaryBarComponent {
  protected readonly stockState = inject(PortfolioStateService);
  protected readonly cashState = inject(CashStateService);
  protected readonly optionState = inject(OptionStateService);
  protected readonly demoMode = inject(DemoModeService);
  private readonly api = inject(PortfolioApiService);

  /** Total portfolio value: stocks + cash + option market value */
  protected readonly totalValue = computed(
    () =>
      this.stockState.totalValue() +
      this.cashState.totalCash() +
      this.optionState.totalMarketValue(),
  );

  protected readonly totalPositions = computed(
    () =>
      this.stockState.summaries().length +
      this.cashState.items().length +
      this.optionState.items().length,
  );

  /** Previous day stored portfolio value (from DB). Loaded once on init. */
  protected readonly previousDayValue = signal<number | null>(null);
  protected readonly oneDayChangeLoading = signal(true);

  /** 1 Day Change = current value − previous day stored value */
  protected readonly oneDayChange = computed<number | null>(() => {
    const prev = this.previousDayValue();
    if (prev === null) return null;
    return this.totalValue() - prev;
  });

  constructor() {
    this.api.getPortfolioValueHistory(2).subscribe({
      next: (history) => {
        if (history.length >= 2) {
          // history is ordered by RecordedAt desc: [0] = today or latest, [1] = day before
          this.previousDayValue.set(history[1].totalValue);
        }
        this.oneDayChangeLoading.set(false);
      },
      error: () => this.oneDayChangeLoading.set(false),
    });
  }
}
