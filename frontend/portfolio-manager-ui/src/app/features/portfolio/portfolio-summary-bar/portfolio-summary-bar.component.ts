import { CurrencyPipe, DecimalPipe, NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { CashStateService } from '../../../core/services/cash-state.service';
import { OptionStateService } from '../../../core/services/option-state.service';
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

  /** Total portfolio value: stocks + cash + option market value */
  protected readonly totalValue = computed(
    () =>
      this.stockState.totalValue() +
      this.cashState.totalCash() +
      this.optionState.totalMarketValue(),
  );

  /** Total cost: stocks cost + cash (cost = amount) + options cost */
  protected readonly totalCost = computed(
    () => this.stockState.totalCost() + this.cashState.totalCash() + this.optionState.totalCost(),
  );

  protected readonly totalGainLoss = computed(() => this.totalValue() - this.totalCost());

  protected readonly totalGainLossPct = computed(() => {
    const cost = this.totalCost();
    return cost === 0 ? 0 : (this.totalGainLoss() / cost) * 100;
  });

  protected readonly isPositive = computed(() => this.totalGainLoss() >= 0);

  protected readonly totalPositions = computed(
    () =>
      this.stockState.summaries().length +
      this.cashState.items().length +
      this.optionState.items().length,
  );
}
