import { CurrencyPipe, DecimalPipe, NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PortfolioSummary } from '../../core/models/portfolio.models';
import { PortfolioStateService } from '../../core/services/portfolio-state.service';

@Component({
  selector: 'app-stock-card',
  templateUrl: './stock-card.component.html',
  styleUrl: './stock-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatChipsModule,
    MatTooltipModule,
    CurrencyPipe,
    DecimalPipe,
    NgClass,
  ],
})
export class StockCardComponent {
  readonly summary = input.required<PortfolioSummary>();
  private readonly state = inject(PortfolioStateService);

  protected readonly currentPrice = computed(
    () => this.summary().quote?.currentPrice ?? this.summary().item.averageCostBasis,
  );

  protected readonly marketValue = computed(() => this.currentPrice() * this.summary().item.shares);

  protected readonly costBasis = computed(
    () => this.summary().item.averageCostBasis * this.summary().item.shares,
  );

  protected readonly gainLoss = computed(() => this.marketValue() - this.costBasis());

  protected readonly gainLossPct = computed(() => {
    const cost = this.costBasis();
    return cost === 0 ? 0 : (this.gainLoss() / cost) * 100;
  });

  protected readonly isPositive = computed(() => this.gainLoss() >= 0);

  remove(): void {
    const { id, symbol } = this.summary().item;
    this.state.deleteItem(id, symbol);
  }
}
