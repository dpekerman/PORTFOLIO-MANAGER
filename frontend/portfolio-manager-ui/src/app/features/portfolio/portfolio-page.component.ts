import { NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PortfolioStateService } from '../../core/services/portfolio-state.service';
import { ScannerStateService } from '../../core/services/scanner-state.service';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog.component';
import { AddManualDialogComponent } from '../add-manual-dialog/add-manual-dialog.component';
import { AddStockDialogComponent } from '../add-stock-dialog/add-stock-dialog.component';
import { PortfolioSummaryBarComponent } from '../portfolio-summary-bar/portfolio-summary-bar.component';
import { StockCardComponent } from '../stock-card/stock-card.component';
import { ImportStocksDialogComponent } from './import-stocks-dialog/import-stocks-dialog.component';

@Component({
  selector: 'app-portfolio-page',
  templateUrl: './portfolio-page.component.html',
  styleUrl: './portfolio-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    NgClass,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    StockCardComponent,
    PortfolioSummaryBarComponent,
  ],
})
export class PortfolioPageComponent {
  protected readonly portfolio = inject(PortfolioStateService);
  protected readonly scanner = inject(ScannerStateService);
  private readonly dialog = inject(MatDialog);

  openAddStockDialog(): void {
    this.dialog.open(AddStockDialogComponent, { width: '480px', maxWidth: '95vw' });
  }

  openAddManualDialog(): void {
    this.dialog.open(AddManualDialogComponent, { width: '480px', maxWidth: '95vw' });
  }

  openImportDialog(): void {
    this.dialog
      .open(ImportStocksDialogComponent, { width: '640px', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((imported) => {
        if (imported) this.scanner.refresh(true);
      });
  }

  confirmBulkDelete(): void {
    const count = this.portfolio.selectedCount();
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Remove Positions',
          message: `Remove all ${count} selected positions? This cannot be undone.`,
          confirmLabel: `Remove ${count}`,
          danger: true,
        },
        width: '400px',
      })
      .afterClosed()
      .subscribe((confirmed: boolean) => {
        if (confirmed) this.portfolio.deleteSelectedAsync();
      });
  }

  refresh(): void {
    this.portfolio.refresh();
  }
}
