import { CurrencyPipe, DecimalPipe, NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PortfolioSummary } from '../../../core/models/portfolio.models';
import { DemoModeService } from '../../../core/services/demo-mode.service';
import { PortfolioStateService } from '../../../core/services/portfolio-state.service';
import { ConfirmDialogComponent } from '../../../shared/confirm-dialog/confirm-dialog.component';
import {
  EditPositionDialogComponent,
  EditPositionDialogResult,
} from '../edit-position-dialog/edit-position-dialog.component';

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
  /** RSI(14) value from the scanner — null when this symbol is not in the scanner results. */
  readonly rsi = input<number | null>(null);
  protected readonly state = inject(PortfolioStateService);
  protected readonly demoMode = inject(DemoModeService);
  private readonly dialog = inject(MatDialog);

  protected readonly currentPrice = computed(() => {
    const item = this.summary().item;
    if (item.isManual) return item.manualMarketValue ?? item.averageCostBasis;
    return this.summary().quote?.currentPrice ?? item.averageCostBasis;
  });

  protected readonly marketValue = computed(() => this.currentPrice() * this.summary().item.shares);

  protected readonly costBasis = computed(
    () => this.summary().item.averageCostBasis * this.summary().item.shares,
  );

  protected readonly gainLoss = computed(() => this.marketValue() - this.costBasis());

  protected readonly gainLossPct = computed(() => {
    const cost = this.costBasis();
    return cost === 0 ? 0 : (this.gainLoss() / cost) * 100;
  });

  /** Overall portfolio P&L direction — drives card border colour */
  protected readonly isPositive = computed(() => this.gainLoss() >= 0);

  /** Today's daily change direction — drives the change row icon/colour */
  protected readonly isDailyPositive = computed(() => (this.summary().quote?.change ?? 0) >= 0);

  protected readonly isSelected = computed(() => this.state.isSelected(this.summary().item.id));

  toggleSelect(): void {
    this.state.toggleSelection(this.summary().item.id);
  }

  edit(): void {
    this.dialog
      .open(EditPositionDialogComponent, {
        data: { item: this.summary().item },
        width: '460px',
        maxWidth: '95vw',
      })
      .afterClosed()
      .subscribe((result: EditPositionDialogResult | undefined) => {
        if (result) {
          this.state.updateItem(this.summary().item.id, {
            companyName: result.companyName,
            shares: result.shares,
            averageCostBasis: result.averageCostBasis,
            sector: result.sector,
            industry: result.industry,
          });
        }
      });
  }

  remove(): void {
    const { symbol } = this.summary().item;
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Remove Position',
          message: `Remove ${symbol} from your portfolio? This cannot be undone.`,
          confirmLabel: 'Remove',
          danger: true,
        },
        width: '380px',
      })
      .afterClosed()
      .subscribe((confirmed: boolean) => {
        if (confirmed) {
          const { id } = this.summary().item;
          this.state.deleteItem(id, symbol);
        }
      });
  }
}
