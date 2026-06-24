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
  template: `
    <h2 mat-dialog-title>Notes — {{ data.symbol }}</h2>
    <mat-dialog-content>
      <mat-form-field appearance="outline" style="width:100%">
        <mat-label>Notes</mat-label>
        <textarea
          matInput
          [(ngModel)]="notes"
          rows="6"
          placeholder="Add a note about this transaction…"
          cdkFocusInitial
        ></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="cancel()">Cancel</button>
      @if (notes) {
        <button mat-stroked-button color="warn" (click)="clear()">Clear Note</button>
      }
      <button mat-flat-button color="primary" (click)="save()">Save</button>
    </mat-dialog-actions>
  `,
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
