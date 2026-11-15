import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { GridColumnStore, resolveKeys } from '../../core/store/grid-column.store';
import { GRID_REGISTRY } from '../../core/store/grid-registry';
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
  templateUrl: './grid-column-btn.component.html',
  styleUrl: './grid-column-btn.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, MatTooltipModule],
})
export class GridColumnButtonComponent {
  readonly gridId = input.required<string>();
  readonly profile = input<string | undefined>(undefined);
  /** Renders a labelled "Columns (n)" button instead of the bare icon. */
  readonly showLabel = input(false);

  private readonly dialog = inject(MatDialog);
  private readonly gridColumns = inject(GridColumnStore);

  private readonly _cols = computed(() =>
    this.gridColumns.getColumnKeys(this.gridId(), this.profile())(),
  );

  /** Number of visible data columns (pinned chrome such as the config gear excluded). */
  protected readonly visibleCount = computed(() => {
    const gridDef = GRID_REGISTRY.find((g) => g.id === this.gridId());
    const pinned = new Set(gridDef?.columns.filter((c) => c.pinned).map((c) => c.key) ?? []);
    return this._cols().filter((k) => !pinned.has(k)).length;
  });

  /** True when the grid has been customised (differs from defaults). */
  protected readonly isCustomised = computed(() => {
    const gridDef = GRID_REGISTRY.find((g) => g.id === this.gridId());
    if (!gridDef) return false;
    const current = this._cols();
    const defaults = resolveKeys(gridDef, undefined, this.profile());
    if (current.length !== defaults.length) return true;
    return current.some((k, i) => k !== defaults[i]);
  });

  protected readonly tooltip = computed(() => {
    const gridDef = GRID_REGISTRY.find((g) => g.id === this.gridId());
    const label = gridDef?.label ?? 'grid';
    const current = this._cols().length;
    const total = (gridDef?.columns ?? []).length;
    const profile = this.profile();
    return `Configure ${profile ? `${profile} ` : ''}${label} columns (${current} / ${total} visible)`;
  });

  protected open(): void {
    this.dialog.open<ColumnConfigDialogComponent, ColumnConfigDialogData>(
      ColumnConfigDialogComponent,
      {
        data: { gridId: this.gridId(), profile: this.profile() },
        autoFocus: false,
        panelClass: 'column-config-panel',
      },
    );
  }
}
