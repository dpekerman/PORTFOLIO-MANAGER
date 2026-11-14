import {
  ChangeDetectionStrategy,
  Component,
  afterEveryRender,
  computed,
  inject,
  input,
  linkedSignal,
  signal,
  untracked,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSort, SortDirection } from '@angular/material/sort';
import { MatTooltipModule } from '@angular/material/tooltip';
import { BreakpointStore } from '../../core/store/breakpoint.store';
import { GridColumnStore } from '../../core/store/grid-column.store';
import { CONFIG_COLUMN_KEY, GRID_REGISTRY } from '../../core/store/grid-registry';

/** One entry of the "Sort by" dropdown. */
interface SortOption {
  key: string;
  label: string;
}

/**
 * The controls a card-mode table loses when its header row is hidden.
 *
 * In card mode there are no column headers, so there is nothing to click to sort and no gear to
 * reveal a hidden column. This bar restores both for handsets and renders **nothing** anywhere
 * else, so a page can include it unconditionally:
 *
 * - "Sort by" + a direction toggle drive the page's existing {@link MatSort}; the page keeps
 *   handling `matSortChange` exactly as it does for header clicks, so no sorting logic is
 *   duplicated.
 * - "Show all fields" flips {@link GridColumnStore} `showAllColumnsOnHandset`, which widens the
 *   card from the primary columns to every column the user has visible — a view-only switch that
 *   never touches saved preferences.
 *
 * The option list comes from {@link GRID_REGISTRY} filtered by the grid's responsive keys (so it
 * lists exactly the fields on screen) and intersected with the sortables MatSort knows about. The
 * intersection is skipped while MatSort is still empty, which happens for one change-detection
 * pass before the header cells register themselves.
 */
@Component({
  selector: 'app-mobile-table-controls',
  templateUrl: './mobile-table-controls.component.html',
  styleUrl: './mobile-table-controls.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatTooltipModule,
  ],
})
export class MobileTableControlsComponent {
  private readonly breakpoints = inject(BreakpointStore);
  private readonly gridColumns = inject(GridColumnStore);

  /** The `matSort` instance of the table being controlled. */
  readonly sort = input.required<MatSort>();
  /** Registry id of the grid, used for labels and for the "Show all fields" toggle. */
  readonly gridId = input.required<string>();
  /** Accessible name of the bar, e.g. "Stock transactions". */
  readonly label = input('Table');

  protected readonly isHandset = this.breakpoints.isHandset;
  protected readonly showAllColumns = this.gridColumns.showAllColumnsOnHandset;

  /**
   * Sorted, comma-joined ids currently registered on the MatSort.
   *
   * `MatSort.sortables` is a plain Map filled by the header cells as they initialise, so it is
   * invisible to `computed`. Snapshotting it after every render — and only writing when the set
   * actually changed — turns it into a signal without risking a render loop.
   */
  private readonly sortableIds = signal('');

  constructor() {
    afterEveryRender(() => {
      const ids = Array.from(this.sort().sortables.keys()).sort().join(',');
      if (ids !== untracked(this.sortableIds)) this.sortableIds.set(ids);
    });
  }

  protected readonly sortOptions = computed<SortOption[]>(() => {
    const gridDef = GRID_REGISTRY.find((g) => g.id === this.gridId());
    if (!gridDef) return [];

    const onScreen = new Set(this.gridColumns.getResponsiveColumnKeys(this.gridId())());
    const candidates = gridDef.columns.filter(
      (c) => c.key !== CONFIG_COLUMN_KEY && !c.pinned && c.label && onScreen.has(c.key),
    );

    const sortables = new Set(this.sortableIds().split(',').filter(Boolean));
    const sortable = candidates.filter((c) => sortables.has(c.key));
    const chosen = sortable.length > 0 ? sortable : candidates;
    return chosen.map(({ key, label }) => ({ key, label }));
  });

  /** Follows MatSort, but is writable so a selection applies before MatSort emits back. */
  protected readonly activeKey = linkedSignal<string>(
    () => this.sort().active || this.sortOptions()[0]?.key || '',
  );
  protected readonly direction = linkedSignal<SortDirection>(() => this.sort().direction || 'asc');

  protected readonly directionLabel = computed(() =>
    this.direction() === 'desc' ? 'Descending' : 'Ascending',
  );

  protected onSortKeyChange(key: string): void {
    this.apply(key, this.direction() || 'asc');
  }

  protected toggleDirection(): void {
    this.apply(this.activeKey(), this.direction() === 'asc' ? 'desc' : 'asc');
  }

  protected onShowAllChange(showAll: boolean): void {
    this.gridColumns.setShowAllColumnsOnHandset(showAll);
  }

  /**
   * Drives MatSort programmatically.
   *
   * `MatSort.sort()` only *toggles* through the configured direction cycle, so an explicit
   * direction is applied by setting the state and emitting `sortChange` — the same event a header
   * click produces, which is what every page already listens to.
   */
  private apply(key: string, direction: SortDirection): void {
    if (!key) return;
    const sort = this.sort();
    sort.active = key;
    sort.direction = direction;
    this.activeKey.set(key);
    this.direction.set(direction);
    sort.sortChange.emit({ active: key, direction });
  }
}
