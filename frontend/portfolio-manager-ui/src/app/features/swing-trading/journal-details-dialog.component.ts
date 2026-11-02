import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { JournalRow } from '../../core/models/swing-trading.models';
import { DemoModeStore } from '../../core/store/demo-mode.store';

/** Read-only look at one journal row: the lots behind it, its thesis and notes, and the stop/target change history. */
@Component({
  selector: 'app-journal-details-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, DecimalPipe, MatButtonModule, MatDialogModule, MatIconModule],
  templateUrl: './journal-details-dialog.component.html',
  styleUrl: './journal-details-dialog.component.scss',
})
export class JournalDetailsDialogComponent {
  protected readonly row = inject<{ row: JournalRow }>(MAT_DIALOG_DATA).row;
  protected readonly demo = inject(DemoModeStore);

  protected value(value: number | null): number | null {
    return value === null ? null : this.demo.maskValue(value);
  }

  protected pnl(): number | null {
    return this.row.realizedPnl === null ? null : this.demo.maskValue(this.row.realizedPnl);
  }
}
