import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';

export interface CloseLotPickerLot {
  id: number;
  openDate: string | null;
  shares: number;
}

export interface CloseLotPickerData {
  symbol: string;
  account: string;
  lots: CloseLotPickerLot[];
}

/** Compact chooser for which OPEN lot of a multi-lot Swing position to close. Resolves the lot id or nothing. */
@Component({
  selector: 'app-close-lot-picker-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, DecimalPipe, MatButtonModule, MatDialogModule],
  templateUrl: './close-lot-picker-dialog.component.html',
  styleUrl: './close-lot-picker-dialog.component.scss',
})
export class CloseLotPickerDialogComponent {
  protected readonly data: CloseLotPickerData = inject(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<CloseLotPickerDialogComponent, number>);

  protected choose(id: number): void {
    this.ref.close(id);
  }
}
