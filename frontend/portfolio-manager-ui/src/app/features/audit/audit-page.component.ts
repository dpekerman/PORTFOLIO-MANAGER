import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AuditAction, AuditCategory, AuditLogEntry, AuditSource } from '../../core/models/portfolio.models';
import { AuditRange, AuditStore } from '../../core/store/audit.store';
import { AuthStore } from '../../core/store/auth.store';
import { BreakpointStore } from '../../core/store/breakpoint.store';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { AppRefreshButtonComponent } from '../../shared/app-refresh-button/app-refresh-button.component';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../shared/confirm-dialog/confirm-dialog.component';

/** Fields holding money values; they go through DemoModeStore like every other monetary display. */
const MONEY_FIELDS = new Set([
  'Amount',
  'AverageCostBasis',
  'ClosingPrice',
  'ManualMarketValue',
  'Premium',
  'Strike',
  'MarketPrice',
  'Price',
  'Balance',
]);

const SEARCH_DEBOUNCE_MS = 350;

@Component({
  selector: 'app-audit-page',
  templateUrl: './audit-page.component.html',
  styleUrl: './audit-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    NgTemplateOutlet,
    AppRefreshButtonComponent,
    MatButtonModule,
    MatButtonToggleModule,
    MatCheckboxModule,
    MatExpansionModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatSelectModule,
    MatSortModule,
    MatTableModule,
    MatTooltipModule,
  ],
})
export class AuditPageComponent {
  protected readonly store = inject(AuditStore);
  protected readonly isAdmin = inject(AuthStore).isAdmin;
  protected readonly isHandset = inject(BreakpointStore).isHandset;
  private readonly demoMode = inject(DemoModeStore);

  protected readonly displayedColumns = computed(() => {
    const cols = ['time', 'user', 'category', 'action', 'symbol', 'account', 'summary'];
    return this.isAdmin() ? ['select', ...cols, 'actions'] : cols;
  });
  private readonly dialog = inject(MatDialog);
  protected readonly expandedId = signal<number | null>(null);
  /** Collapsed by default on handsets so the entries are visible without scrolling past the filters. */
  protected readonly filtersOpen = signal(!this.isHandset());
  protected readonly selected = signal<ReadonlySet<number>>(new Set());
  protected readonly selectedCount = computed(() => this.selected().size);
  protected readonly allOnPageSelected = computed(
    () => this.store.items().length > 0 && this.store.items().every((r) => this.selected().has(r.id)),
  );
  private searchTimer: ReturnType<typeof setTimeout> | null = null;
  protected readonly emptySelection: ReadonlySet<number> = new Set();

  protected readonly ranges: { value: AuditRange; label: string }[] = [
    { value: 'today', label: 'Today' },
    { value: '7d', label: '7 days' },
    { value: '30d', label: '30 days' },
    { value: 'all', label: 'All' },
    { value: 'custom', label: 'Custom' },
  ];
  protected readonly sources: AuditSource[] = ['User', 'System'];
  protected readonly mobileSorts = [
    { value: 'time', label: 'Time' },
    { value: 'category', label: 'Category' },
    { value: 'action', label: 'Action' },
    { value: 'symbol', label: 'Symbol' },
    { value: 'user', label: 'User' },
  ];

  protected readonly categories = computed(() => this.store.filters().categories);
  protected readonly actions = computed(() => this.store.filters().actions);
  protected readonly users = computed(() => this.store.filters().users);

  constructor() {
    // The store loads once after login; refetch so the page always shows the latest events.
    this.store.load();
  }

  protected onRangeChange(range: AuditRange): void {
    if (range === 'custom') this.store.setRange('custom', this.store.customFrom(), this.store.customTo());
    else this.store.setRange(range);
  }

  protected onCustomDate(which: 'from' | 'to', value: string): void {
    const from = which === 'from' ? value || null : this.store.customFrom();
    const to = which === 'to' ? value || null : this.store.customTo();
    this.store.setRange('custom', from, to);
  }

  protected onSearch(value: string): void {
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.store.setSearch(value), SEARCH_DEBOUNCE_MS);
  }

  protected onSortChange(sort: Sort): void {
    this.store.setSort(sort.active || 'time', sort.direction || 'desc');
  }

  protected toggleMobileDir(): void {
    this.store.setSort(this.store.sortBy(), this.store.sortDir() === 'asc' ? 'desc' : 'asc');
  }

  protected onPage(e: PageEvent): void {
    this.store.setPage(e.pageIndex + 1, e.pageSize);
  }

  protected toggle(row: AuditLogEntry): void {
    this.expandedId.update((id) => (id === row.id ? null : row.id));
  }

  protected toggleSelected(id: number): void {
    this.selected.update((s) => {
      const next = new Set(s);
      if (!next.delete(id)) next.add(id);
      return next;
    });
  }

  protected toggleAllOnPage(): void {
    const ids = this.store.items().map((r) => r.id);
    this.selected.update((s) => {
      const next = new Set(s);
      if (this.allOnPageSelected()) ids.forEach((id) => next.delete(id));
      else ids.forEach((id) => next.add(id));
      return next;
    });
  }

  protected confirmDeleteSelected(): void {
    const ids = [...this.selected()];
    if (ids.length === 0) return;
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Delete Audit Entries',
          message: `Permanently delete ${ids.length} selected audit ${ids.length === 1 ? 'entry' : 'entries'}? This cannot be undone.`,
          confirmLabel: 'Delete',
          danger: true,
        } satisfies ConfirmDialogData,
        width: '400px',
        maxWidth: '95vw',
      })
      .afterClosed()
      .subscribe((confirmed: boolean) => {
        if (confirmed) this.store.deleteEntries(ids, () => this.selected.set(new Set()));
      });
  }

  protected confirmDelete(row: AuditLogEntry, event: Event): void {
    event.stopPropagation();
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Delete Audit Entry',
          message: `Permanently delete audit entry #${row.id} (${row.category} ${row.action}${row.symbol ? ' ' + row.symbol : ''})? This cannot be undone.`,
          confirmLabel: 'Delete',
          danger: true,
        } satisfies ConfirmDialogData,
        width: '400px',
        maxWidth: '95vw',
      })
      .afterClosed()
      .subscribe((confirmed: boolean) => {
        if (confirmed) this.store.deleteEntry(row.id);
      });
  }

  protected setCategory(v: AuditCategory[]): void {
    this.store.setCategory(v);
  }

  protected setAction(v: AuditAction[]): void {
    this.store.setAction(v);
  }

  protected actionClass(action: AuditAction): string {
    return `badge-${action.toLowerCase()}`;
  }

  protected who(row: AuditLogEntry): string {
    return row.source === 'System' ? 'System' : (row.userName ?? row.userId ?? '—');
  }

  /** Hides raw values in demo mode: the summary text embeds amounts and prices. */
  protected summary(row: AuditLogEntry): string {
    if (!this.demoMode.isDemoMode()) return row.summary;
    return `${row.action} ${row.entityType}${row.symbol ? ' ' + row.symbol : ''}`;
  }

  protected value(field: string, v: string | null): string {
    if (v === null) return '—';
    if (MONEY_FIELDS.has(field)) {
      const n = Number(v);
      if (!Number.isNaN(n)) return this.demoMode.displayValue(n, 2);
    }
    return v;
  }
}
