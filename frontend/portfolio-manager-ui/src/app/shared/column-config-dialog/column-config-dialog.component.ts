import { CdkDrag, CdkDragDrop, CdkDropList, moveItemInArray } from '@angular/cdk/drag-drop';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogClose, MatDialogModule } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  ColumnPreference,
  GRID_REGISTRY,
  GridColumnService,
} from '../../core/services/grid-column.service';

export interface ColumnConfigDialogData {
  gridId: string;
}

@Component({
  selector: 'app-column-config-dialog',
  templateUrl: './column-config-dialog.component.html',
  styleUrl: './column-config-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatDialogModule,
    MatDialogClose,
    MatButtonModule,
    MatIconModule,
    MatDividerModule,
    MatSlideToggleModule,
    MatTooltipModule,
    CdkDropList,
    CdkDrag,
  ],
})
export class ColumnConfigDialogComponent {
  private readonly gridColumnService = inject(GridColumnService);
  private readonly data = inject<ColumnConfigDialogData>(MAT_DIALOG_DATA);

  /** The GridDef for this dialog instance. */
  protected readonly grid = GRID_REGISTRY.find((g) => g.id === this.data.gridId) ?? null;

  /** Working copy of column preferences. */
  protected readonly workingPrefs = signal<ColumnPreference[]>(
    this.gridColumnService.getEditablePrefs(this.data.gridId),
  );

  /** Count of currently visible columns. */
  protected readonly visibleCount = computed(
    () => this.workingPrefs().filter((p) => p.visible).length,
  );

  /** True when preferences differ from defaults. */
  protected readonly hasCustomisation = computed(() => {
    const prefs = this.workingPrefs();
    if (!this.grid) return false;
    const defaults = this.grid.columns.filter((c) => !c.pinned);
    if (prefs.length !== defaults.length) return true;
    return prefs.some((p, i) => !p.visible || p.key !== defaults[i].key);
  });

  // ── Column management ──────────────────────────────────────────────────────

  protected getLabel(key: string): string {
    return this.grid?.columns.find((c) => c.key === key)?.label ?? key;
  }

  protected moveUp(index: number): void {
    if (index <= 0) return;
    this.workingPrefs.update((prefs) => {
      const copy = [...prefs];
      [copy[index - 1], copy[index]] = [copy[index], copy[index - 1]];
      return copy;
    });
    this.persist();
  }

  protected moveDown(index: number): void {
    if (index >= this.workingPrefs().length - 1) return;
    this.workingPrefs.update((prefs) => {
      const copy = [...prefs];
      [copy[index], copy[index + 1]] = [copy[index + 1], copy[index]];
      return copy;
    });
    this.persist();
  }

  protected onDrop(event: CdkDragDrop<ColumnPreference[]>): void {
    if (event.previousIndex === event.currentIndex) return;
    this.workingPrefs.update((prefs) => {
      const copy = [...prefs];
      moveItemInArray(copy, event.previousIndex, event.currentIndex);
      return copy;
    });
    this.persist();
  }

  protected setVisible(index: number, visible: boolean): void {
    this.workingPrefs.update((prefs) => {
      const copy = [...prefs];
      copy[index] = { ...copy[index], visible };
      return copy;
    });
    this.persist();
  }

  protected resetToDefaults(): void {
    this.gridColumnService.resetGrid(this.data.gridId);
    this.workingPrefs.set(this.gridColumnService.getEditablePrefs(this.data.gridId));
  }

  // ── Private ────────────────────────────────────────────────────────────────

  private persist(): void {
    this.gridColumnService.updatePrefs(this.data.gridId, this.workingPrefs());
  }
}
