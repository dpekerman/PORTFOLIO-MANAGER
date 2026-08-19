import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PortfolioValueHistoryDto } from '../../../core/models/portfolio.models';
import { CashStateService } from '../../../core/services/cash-state.service';
import { DemoModeService } from '../../../core/services/demo-mode.service';
import { OptionStateService } from '../../../core/services/option-state.service';
import { PortfolioApiService } from '../../../core/services/portfolio-api.service';
import { PortfolioBetaStateService } from '../../../core/services/portfolio-beta-state.service';
import { PortfolioStateService } from '../../../core/services/portfolio-state.service';

@Component({
  selector: 'app-portfolio-summary-bar',
  templateUrl: './portfolio-summary-bar.component.html',
  styleUrl: './portfolio-summary-bar.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatCardModule, MatIconModule, CurrencyPipe, DecimalPipe, MatTooltipModule],
})
export class PortfolioSummaryBarComponent {
  protected readonly stockState = inject(PortfolioStateService);
  protected readonly cashState = inject(CashStateService);
  protected readonly optionState = inject(OptionStateService);
  protected readonly demoMode = inject(DemoModeService);
  private readonly api = inject(PortfolioApiService);
  protected readonly betaState = inject(PortfolioBetaStateService);

  protected dv(v: number): number {
    return this.demoMode.isDemoMode() && this.demoMode.demoStyle() === 'fake'
      ? this.demoMode.maskValue(v)
      : v;
  }

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
    this.loadHistory();
  }

  private loadHistory(): void {
    this.api.getPortfolioValueHistory(2).subscribe({
      next: (history) => {
        if (history.length > 0) {
          const todayDate = new Date().toISOString().split('T')[0];
          const isFirstRecordToday = history[0].recordedDate === todayDate;

          if (isFirstRecordToday && history.length >= 2) {
            this.previousDayValue.set(history[1].totalValue);
            this.oneDayChangeLoading.set(false);
          } else if (!isFirstRecordToday) {
            // Check whether the most recent record is from yesterday or older
            const yesterday = new Date();
            yesterday.setDate(yesterday.getDate() - 1);
            // Skip back over weekends to find the last trading day
            while (yesterday.getDay() === 0 || yesterday.getDay() === 6)
              yesterday.setDate(yesterday.getDate() - 1);
            const lastTradingDay = yesterday.toISOString().split('T')[0];

            if (history[0].recordedDate < lastTradingDay) {
              // Gap detected — attempt silent backfill then reload
              this.api.backfillMissingHistory(14).subscribe({
                next: (filled) => {
                  if (filled.length > 0) this.loadHistory();
                  else this.setPreviousDay(history);
                },
                error: () => this.setPreviousDay(history),
              });
              return;
            }
            this.setPreviousDay(history);
          }
        }
        this.oneDayChangeLoading.set(false);
      },
      error: () => this.oneDayChangeLoading.set(false),
    });
  }

  private setPreviousDay(history: PortfolioValueHistoryDto[]): void {
    this.previousDayValue.set(history[0].totalValue);
    this.oneDayChangeLoading.set(false);
  }
}
