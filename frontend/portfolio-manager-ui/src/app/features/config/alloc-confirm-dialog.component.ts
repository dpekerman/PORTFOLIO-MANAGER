import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-alloc-confirm-dialog',
  templateUrl: './alloc-confirm-dialog.component.html',
  styleUrl: './alloc-confirm-dialog.component.scss',
  imports: [MatButtonModule, MatDialogModule, MatIconModule],
})
export class AllocConfirmDialogComponent {
  readonly data = inject<{
    title: string;
    message: string;
    confirmLabel?: string;
    confirmIcon?: string;
  }>(MAT_DIALOG_DATA);
}
