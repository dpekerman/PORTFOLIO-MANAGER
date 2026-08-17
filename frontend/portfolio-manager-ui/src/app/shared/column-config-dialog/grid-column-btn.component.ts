import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { GRID_REGISTRY, GridColumnService } from '../../core/services/grid-column.service';
import {
  ColumnConfigDialogComponent,
  ColumnConfigDialogData,
} from '../column-config-dialog/column-config-dialog.component';

/**
 * Small icon button placed directly on each grid.
 * Opens the column-config dialog pre-scoped to the given gridId.
 * Usage: <app-grid-column-btn gridId="portfolio-stocks" />
 */
@Component({
  selector: 'app-grid-column-btn',
  template: `
    <button
      mat-icon-button
      class="gcb-btn"
      [class.gcb-customised]="isCustomised()"
      [matTooltip]="tooltip()"
      (click)="open()"
      aria-label="Configure columns"
    >
      <mat-icon>view_column</mat-icon>
    </button>
  `,
  styles: [
    `
      :host {
        display: inline-flex;
        align-items: center;
      }
      .gcb-btn {
        opacity: 0.65;
        transition: opacity 150ms;
      }
      .gcb-btn:hover {
        opacity: 1;
      }
      .gcb-btn.gcb-customised {
        opacity: 1;
        color: var(--mat-sys-primary);
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, MatTooltipModule],
})
export class GridColumnButtonComponent {
  readonly gridId = input.required<string>();

  private readonly dialog = inject(MatDialog);
  private readonly gridColService = inject(GridColumnService);

  private readonly _cols = computed(() => this.gridColService.getColumnKeys(this.gridId())());

  /** True when the grid has been customised (differs from defaults). */
  protected readonly isCustomised = computed(() => {
    const gridDef = GRID_REGISTRY.find((g) => g.id === this.gridId());
    if (!gridDef) return false;
    const current = this._cols();
    const defaults = gridDef.columns.map((c) => c.key);
    if (current.length !== defaults.length) return true;
    return current.some((k, i) => k !== defaults[i]);
  });

  protected readonly tooltip = computed(() => {
    const gridDef = GRID_REGISTRY.find((g) => g.id === this.gridId());
    const label = gridDef?.label ?? 'grid';
    const current = this._cols().length;
    const total = (gridDef?.columns ?? []).length;
    return `Configure ${label} columns (${current} / ${total} visible)`;
  });

  protected open(): void {
    this.dialog.open<ColumnConfigDialogComponent, ColumnConfigDialogData>(
      ColumnConfigDialogComponent,
      {
        data: { gridId: this.gridId() },
        autoFocus: false,
        panelClass: 'column-config-panel',
      },
    );
  }
}
