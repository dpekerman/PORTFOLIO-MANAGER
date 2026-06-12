import { DatePipe, NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PortfolioStateService } from '../../core/services/portfolio-state.service';
import { ScannerStateService } from '../../core/services/scanner-state.service';
import { ThemeService } from '../../core/services/theme.service';
import { WatchlistStateService } from '../../core/services/watchlist-state.service';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog.component';
import { AddManualDialogComponent } from '../add-manual-dialog/add-manual-dialog.component';
import { AddStockDialogComponent } from '../add-stock-dialog/add-stock-dialog.component';
import { ImportStocksDialogComponent } from '../import-stocks-dialog/import-stocks-dialog.component';
import { MarketHeaderComponent } from '../market-header/market-header.component';
import { PortfolioSummaryBarComponent } from '../portfolio-summary-bar/portfolio-summary-bar.component';
import { RsiScannerTableComponent } from '../rsi-scanner/rsi-scanner-table.component';
import { SectorExpositionComponent } from '../sector-exposition/sector-exposition.component';
import { StockCardComponent } from '../stock-card/stock-card.component';
import { AddWatchlistDialogComponent } from '../watchlist/add-watchlist-dialog.component';
import { WatchlistCardComponent } from '../watchlist/watchlist-card.component';

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatTooltipModule,
    MatTabsModule,
    MatDividerModule,
    MatInputModule,
    MatFormFieldModule,
    FormsModule,
    DatePipe,
    NgClass,
    StockCardComponent,
    PortfolioSummaryBarComponent,
    RsiScannerTableComponent,
    MarketHeaderComponent,
    WatchlistCardComponent,
    SectorExpositionComponent,
  ],
})
export class DashboardComponent {
  protected readonly portfolio = inject(PortfolioStateService);
  protected readonly scanner = inject(ScannerStateService);
  protected readonly theme = inject(ThemeService);
  protected readonly watchlist = inject(WatchlistStateService);
  private readonly dialog = inject(MatDialog);

  protected readonly activeTab = signal(0);

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

  addToWatchlist(): void {
    this.dialog
      .open(AddWatchlistDialogComponent, { width: '420px', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((symbol: string | null) => {
        if (symbol) this.watchlist.addItem(symbol);
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

  refreshAll(): void {
    this.portfolio.refresh();
    this.scanner.refresh(true);
    this.watchlist.refresh();
  }

  refreshPortfolio(): void {
    this.portfolio.refresh();
  }

  refreshScanner(): void {
    this.scanner.refresh(true);
  }
}
