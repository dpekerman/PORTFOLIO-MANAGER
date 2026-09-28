import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatNativeDateModule, MatOptionModule } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { SnapshotStatus } from '../../../core/models/portfolio.models';
import { PortfolioValueHistoryStateService } from '../../../core/services/portfolio-value-history-state.service';

@Component({
  selector: 'app-history-filter-bar',
  templateUrl: './history-filter-bar.component.html',
  styleUrl: './history-filter-bar.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatFormFieldModule,
    MatInputModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatSelectModule,
    MatOptionModule,
    MatButtonModule,
    MatIconModule,
    ReactiveFormsModule,
  ],
})
export class HistoryFilterBarComponent {
  protected readonly historyState = inject(PortfolioValueHistoryStateService);

  protected readonly statusOptions: { value: SnapshotStatus; label: string }[] = [
    { value: 'Original', label: 'Original' },
    { value: 'Resealed', label: 'Resealed' },
    { value: 'CashRecalculated', label: 'Cash Recalculated' },
    { value: 'PendingReseal', label: 'Pending Reseal' },
  ];

  protected readonly fromControl = new FormControl<Date | null>(null);
  protected readonly toControl = new FormControl<Date | null>(null);

  onFromChange(date: Date | null): void {
    this.historyState.setDateRange({
      from: date ? this.toIso(date) : null,
      to: this.historyState.dateRange().to,
    });
  }

  onToChange(date: Date | null): void {
    this.historyState.setDateRange({
      from: this.historyState.dateRange().from,
      to: date ? this.toIso(date) : null,
    });
  }

  onStatusChange(status: SnapshotStatus | null): void {
    this.historyState.setStatusFilter(status);
  }

  reset(): void {
    this.fromControl.setValue(null);
    this.toControl.setValue(null);
    this.historyState.resetFilters();
  }

  private toIso(date: Date): string {
    return date.toISOString().slice(0, 10);
  }
}
