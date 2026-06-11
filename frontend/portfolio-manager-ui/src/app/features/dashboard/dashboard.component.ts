import { DatePipe, NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PortfolioStateService } from '../../core/services/portfolio-state.service';
import { ScannerStateService } from '../../core/services/scanner-state.service';
import { AddStockDialogComponent } from '../add-stock-dialog/add-stock-dialog.component';
import { MarketHeaderComponent } from '../market-header/market-header.component';
import { PortfolioSummaryBarComponent } from '../portfolio-summary-bar/portfolio-summary-bar.component';
import { RsiScannerTableComponent } from '../rsi-scanner/rsi-scanner-table.component';
import { StockCardComponent } from '../stock-card/stock-card.component';

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
    DatePipe,
    NgClass,
    StockCardComponent,
    PortfolioSummaryBarComponent,
    RsiScannerTableComponent,
    MarketHeaderComponent,
  ],
})
export class DashboardComponent {
  protected readonly portfolio = inject(PortfolioStateService);
  protected readonly scanner = inject(ScannerStateService);
  private readonly dialog = inject(MatDialog);

  protected readonly activeTab = signal(0);

  openAddStockDialog(): void {
    this.dialog.open(AddStockDialogComponent, {
      width: '480px',
      maxWidth: '95vw',
    });
  }

  refreshAll(): void {
    this.portfolio.refresh();
    this.scanner.refresh();
  }
}
