import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';

export interface ConfirmDialogData {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  danger?: boolean;
}

@Component({
  selector: 'app-confirm-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatDialogModule, MatButtonModule, MatIconModule],
  template: `
    <h2 mat-dialog-title class="confirm-title">
      <mat-icon [class.danger-icon]="data.danger">{{
        data.danger ? 'warning' : 'help_outline'
      }}</mat-icon>
      {{ data.title }}
    </h2>
    <mat-dialog-content>
      <p class="confirm-msg">{{ data.message }}</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-stroked-button [mat-dialog-close]="false">
        {{ data.cancelLabel ?? 'Cancel' }}
      </button>
      <button mat-flat-button [color]="data.danger ? 'warn' : 'primary'" [mat-dialog-close]="true">
        {{ data.confirmLabel ?? 'Confirm' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      .confirm-title {
        display: flex;
        align-items: center;
        gap: 8px;
        mat-icon {
          font-size: 22px;
          width: 22px;
          height: 22px;
          color: #9e9e9e;
        }
        mat-icon.danger-icon {
          color: #ff5252;
        }
      }
      .confirm-msg {
        margin: 0;
        color: var(--text-secondary);
        line-height: 1.6;
      }
      mat-dialog-actions {
        gap: 8px;
      }
    `,
  ],
})
export class ConfirmDialogComponent {
  protected readonly data = inject<ConfirmDialogData>(MAT_DIALOG_DATA);
  readonly dialogRef = inject(MatDialogRef<ConfirmDialogComponent>);
}
