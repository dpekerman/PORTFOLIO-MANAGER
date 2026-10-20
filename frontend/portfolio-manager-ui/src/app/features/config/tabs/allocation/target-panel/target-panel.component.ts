import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AllocConfirmDialogComponent } from '../../../alloc-confirm-dialog.component';

export interface TargetRow {
  id: number;
  label: string;
  targetPct: number;
}

export interface TargetRowEdit {
  /** null when a new row was added. */
  id: number | null;
  label: string;
  targetPct: number;
}

interface EditState {
  id: number | null;
  label: string;
  targetPct: number | null;
}

/**
 * Editable "label + percentage" list used for role targets, sector targets and position limits.
 * Presentational: edits are emitted, the parent owns the data and persistence.
 */
@Component({
  selector: 'app-target-panel',
  templateUrl: './target-panel.component.html',
  styleUrl: './target-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatTooltipModule,
  ],
})
export class TargetPanelComponent {
  private readonly dialog = inject(MatDialog);

  readonly heading = input.required<string>();
  /** Label column header and field label, e.g. "Role" or "Sector". */
  readonly labelName = input.required<string>();
  readonly pctLabel = input<string>('%');
  readonly rows = input.required<TargetRow[]>();
  /** Shows the TOTAL line (green at 100%); otherwise `noTotalHint` is shown. */
  readonly showTotal = input<boolean>(true);
  readonly noTotalHint = input<string>('');

  readonly saved = output<TargetRowEdit>();
  readonly deleted = output<number>();

  protected readonly editing = signal<EditState | null>(null);
  protected readonly total = computed(() => this.rows().reduce((s, r) => s + r.targetPct, 0));

  protected startAdd(): void {
    this.editing.set({ id: null, label: '', targetPct: null });
  }

  protected startEdit(row: TargetRow): void {
    this.editing.set({ id: row.id, label: row.label, targetPct: row.targetPct });
  }

  protected cancel(): void {
    this.editing.set(null);
  }

  protected setLabel(label: string): void {
    this.editing.update((e) => (e ? { ...e, label } : e));
  }

  protected setPct(value: number | string): void {
    this.editing.update((e) => (e ? { ...e, targetPct: +value } : e));
  }

  protected save(): void {
    const e = this.editing();
    if (!e || !e.label.trim() || !e.targetPct) return;
    this.saved.emit({ id: e.id, label: e.label.trim(), targetPct: e.targetPct });
    this.editing.set(null);
  }

  protected confirmDelete(id: number): void {
    this.dialog
      .open(AllocConfirmDialogComponent, {
        data: {
          title: 'Delete entry?',
          message: 'This entry will be removed when you save changes.',
        },
        width: '340px',
      })
      .afterClosed()
      .subscribe((confirmed: boolean) => {
        if (confirmed) this.deleted.emit(id);
      });
  }
}
