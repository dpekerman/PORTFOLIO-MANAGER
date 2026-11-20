import { CdkDrag, CdkDragDrop, CdkDropList, moveItemInArray } from '@angular/cdk/drag-drop';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogClose, MatDialogModule } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTooltipModule } from '@angular/material/tooltip';
import { GridColumnStore, isCustomisedLayout } from '../../core/store/grid-column.store';
import { ColumnPreference, GRID_REGISTRY } from '../../core/store/grid-registry';

export interface ColumnConfigDialogData {
  gridId: string;
  profile?: string;
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
  private readonly gridColumns = inject(GridColumnStore);
  private readonly data = inject<ColumnConfigDialogData>(MAT_DIALOG_DATA);

  /** The GridDef for this dialog instance. */
  protected readonly grid = GRID_REGISTRY.find((g) => g.id === this.data.gridId) ?? null;
  protected readonly profileLabel = this.data.profile ? `${this.data.profile} · ` : '';

  /** Working copy of column preferences. */
  protected readonly workingPrefs = signal<ColumnPreference[]>(
    this.gridColumns.getEditablePrefs(this.data.gridId, this.data.profile),
  );

  /** Count of currently visible columns. */
  protected readonly visibleCount = computed(
    () => this.workingPrefs().filter((p) => p.visible).length,
  );

  /** True when preferences differ from defaults. */
  protected readonly hasCustomisation = computed(() =>
    this.grid ? isCustomisedLayout(this.grid, this.workingPrefs(), this.data.profile) : false,
  );

  /** True when at least one column is hidden, so "Show all" has something to do. */
  protected readonly anyHidden = computed(() => this.workingPrefs().some((p) => !p.visible));

  // ── Column management ──────────────────────────────────────────────────────

  protected getLabel(key: string): string {
    return this.grid?.columns.find((c) => c.key === key)?.label ?? key;
  }

  protected isRequired(key: string): boolean {
    return this.grid?.columns.find((c) => c.key === key)?.required === true;
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
    if (!visible && this.isRequired(this.workingPrefs()[index].key)) return;
    this.workingPrefs.update((prefs) => {
      const copy = [...prefs];
      copy[index] = { ...copy[index], visible };
      return copy;
    });
    this.persist();
  }

  /** Shows every column, keeping the current order. */
  protected showAll(): void {
    this.workingPrefs.update((prefs) => prefs.map((p) => ({ ...p, visible: true })));
    this.persist();
  }

  protected resetToDefaults(): void {
    this.gridColumns.resetGrid(this.data.gridId, this.data.profile);
    this.workingPrefs.set(this.gridColumns.getEditablePrefs(this.data.gridId, this.data.profile));
  }

  // ── Private ────────────────────────────────────────────────────────────────

  private persist(): void {
    this.gridColumns.updatePrefs(this.data.gridId, this.workingPrefs(), this.data.profile);
  }
}
