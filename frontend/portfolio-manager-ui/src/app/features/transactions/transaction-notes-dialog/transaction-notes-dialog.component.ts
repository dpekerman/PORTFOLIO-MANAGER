import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export interface TransactionNotesDialogData {
  symbol: string;
  notes: string | null | undefined;
}

export interface TransactionNotesDialogResult {
  notes: string | null;
}

@Component({
  selector: 'app-transaction-notes-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule],
  templateUrl: './transaction-notes-dialog.component.html',
  styleUrl: './transaction-notes-dialog.component.scss',
})
export class TransactionNotesDialogComponent {
  protected readonly data = inject<TransactionNotesDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<TransactionNotesDialogComponent>);

  protected notes: string = this.data.notes ?? '';

  protected save(): void {
    this.dialogRef.close({
      notes: this.notes.trim() || null,
    } satisfies TransactionNotesDialogResult);
  }

  protected clear(): void {
    this.dialogRef.close({ notes: null } satisfies TransactionNotesDialogResult);
  }

  protected cancel(): void {
    this.dialogRef.close();
  }
}
