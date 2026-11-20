import { Directive, computed, inject, input, signal } from '@angular/core';

/**
 * Remembers which row of a Material table the user last worked on.
 *
 * The selected row is tracked by a stable business key (not the row object) so the highlight
 * survives dialogs opening/closing, data refreshes, sorting and filtering. If the key is no longer
 * in the data, nothing is highlighted.
 */
@Directive({
  selector: 'table[mat-table][appRowSelection]',
  exportAs: 'appRowSelection',
})
export class RowSelectionDirective {
  readonly selectedKey = signal<unknown>(null);

  select(key: unknown): void {
    this.selectedKey.set(key);
  }

  toggle(key: unknown): void {
    this.selectedKey.update((current) => (Object.is(current, key) ? null : key));
  }

  clear(): void {
    this.selectedKey.set(null);
  }
}

/**
 * Marks a `<tr mat-row>` as selectable. Any click inside the row (including its action buttons)
 * selects it; clicking the already selected row's background deselects it.
 */
@Directive({
  selector: 'tr[mat-row][appSelectableRow]',
  host: {
    '[class.row-selected]': 'selected()',
    '[attr.aria-selected]': 'selected()',
    '(click)': 'onClick($event)',
  },
})
export class SelectableRowDirective {
  private readonly table = inject(RowSelectionDirective);

  /** Stable, unique key of the row within its table. */
  readonly appSelectableRow = input.required<unknown>();

  protected readonly selected = computed(() =>
    Object.is(this.table.selectedKey(), this.appSelectableRow()),
  );

  protected onClick(event: MouseEvent): void {
    const key = this.appSelectableRow();
    const target = event.target as HTMLElement | null;
    const interactive = target?.closest('button, a, input, mat-checkbox, [role="button"]');
    if (interactive || !this.selected()) {
      this.table.select(key);
    } else {
      this.table.clear();
    }
  }
}

/** Convenience bundle for component imports. */
export const ROW_SELECTION = [RowSelectionDirective, SelectableRowDirective] as const;
