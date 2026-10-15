import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { AuditAction, AuditCategory, AuditLogEntry, AuditSource } from '../../core/models/portfolio.models';
import { AuditRange, AuditStore } from '../../core/store/audit.store';
import { AuthStore } from '../../core/store/auth.store';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { AppRefreshButtonComponent } from '../../shared/app-refresh-button/app-refresh-button.component';

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
    MatExpansionModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatSelectModule,
    MatSortModule,
    MatTableModule,
  ],
})
export class AuditPageComponent {
  protected readonly store = inject(AuditStore);
  protected readonly isAdmin = inject(AuthStore).isAdmin;
  private readonly demoMode = inject(DemoModeStore);

  protected readonly displayedColumns = ['time', 'user', 'category', 'action', 'symbol', 'account', 'summary'];
  protected readonly expandedId = signal<number | null>(null);
  private searchTimer: ReturnType<typeof setTimeout> | null = null;

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
