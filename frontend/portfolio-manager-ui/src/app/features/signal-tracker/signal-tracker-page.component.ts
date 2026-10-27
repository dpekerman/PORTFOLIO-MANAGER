import { DecimalPipe, NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatSnackBar } from '@angular/material/snack-bar';
import * as XLSX from 'xlsx';
import { SIGNALS_EXPORT_HEADERS, signalsExportRows, signalsToCsv } from './signal-tracker-export';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { SignalAuditRow, SignalHorizon, SignalMyAction, SignalStatusFilter } from '../../core/models/portfolio.models';
import { ActivatedRoute } from '@angular/router';
import { BreakpointStore } from '../../core/store/breakpoint.store';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { GridColumnStore } from '../../core/store/grid-column.store';
import { SignalsAuditStore } from '../../core/store/signals-audit.store';
import { AppRefreshButtonComponent } from '../../shared/app-refresh-button/app-refresh-button.component';
import { GridColumnButtonComponent } from '../../shared/column-config-dialog/grid-column-btn.component';
import { MobileTableControlsComponent } from '../../shared/responsive-table/mobile-table-controls.component';
import { ResponsiveTableDirective } from '../../shared/responsive-table/responsive-table.directive';
import { SignalActionDialogComponent } from './signal-action-dialog.component';
import { SignalDetailsDialogComponent } from './signal-details-dialog.component';

const TICKER_DEBOUNCE_MS = 350;
const PRIORITY_CANDIDATE_KEY = 'PRIORITY_CANDIDATE';
type SignalKpi = 'total' | 'priority' | 'entry' | 'trim' | 'actioned' | 'pending';

@Component({
  selector: 'app-signal-tracker-page',
  templateUrl: './signal-tracker-page.component.html',
  styleUrl: './signal-tracker-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DecimalPipe,
    FormsModule,
    NgTemplateOutlet,
    AppRefreshButtonComponent,
    GridColumnButtonComponent,
    MobileTableControlsComponent,
    ResponsiveTableDirective,
    MatButtonModule,
    MatExpansionModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatPaginatorModule,
    MatSelectModule,
    MatCheckboxModule,
    MatDatepickerModule,
    MatSortModule,
    MatTableModule,
    MatTooltipModule,
  ],
})
export class SignalTrackerPageComponent implements OnInit {
  protected readonly store = inject(SignalsAuditStore);
  protected readonly demoMode = inject(DemoModeStore);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly isHandset = inject(BreakpointStore).isHandset;
  private readonly route = inject(ActivatedRoute);

  protected readonly displayedColumns = inject(GridColumnStore).getResponsiveColumnKeys('signals-audit');

  /** Collapsed by default on handsets so the signals are visible without scrolling past the filters. */
  protected readonly filtersOpen = signal(!this.isHandset());

  /** Which summary card (if any) matches the current signal type / My Action filters. */
  protected readonly activeKpi = computed<SignalKpi | null>(() => {
    const types = this.store.signalType();
    const action = this.store.myAction();
    if (types.length === 0) {
      if (action === 'Actioned') return 'actioned';
      if (action === 'Pending') return 'pending';
      return action === 'All' ? 'total' : null;
    }
    if (action !== 'All') return null;
    const same = (keys: string[]) => keys.length === types.length && keys.every((k) => types.includes(k));
    if (same([PRIORITY_CANDIDATE_KEY])) return 'priority';
    if (same(this.keysFor('Entry'))) return 'entry';
    if (same(this.keysFor('TrimExit'))) return 'trim';
    return null;
  });

  /** Card click = filter the table and (server-side, same query) the effectiveness cards. Click again to clear. */
  protected selectKpi(kpi: SignalKpi): void {
    if (kpi === 'total' || this.activeKpi() === kpi) {
      this.store.setFilters({ signalType: [], myAction: 'All', status: 'All' });
      return;
    }
    const types: Record<SignalKpi, string[]> = {
      total: [],
      priority: [PRIORITY_CANDIDATE_KEY],
      entry: this.keysFor('Entry'),
      trim: this.keysFor('TrimExit'),
      actioned: [],
      pending: [],
    };
    const myAction = kpi === 'actioned' ? 'Actioned' : kpi === 'pending' ? 'Pending' : 'All';
    // These cards count open recommendations only, so the table shows the same rows.
    const status = kpi === 'actioned' ? 'All' : 'Open';
    this.store.setFilters({ signalType: types[kpi], myAction, status });
  }

  private keysFor(category: 'Entry' | 'TrimExit'): string[] {
    return this.store.signalTypes().filter((t) => t.category === category).map((t) => t.key);
  }

  protected readonly roles = ['Core', 'Strategic', 'Strategic-Income', 'Swing', 'Speculative', 'Options'];

  /** Filters hold yyyy-mm-dd text; the picker works with local Dates. */
  protected toDate(value: string): Date | null {
    const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
    return m ? new Date(+m[1], +m[2] - 1, +m[3]) : null;
  }

  protected fromDate(value: Date | null): string {
    if (!value) return '';
    const p = (n: number) => String(n).padStart(2, '0');
    return `${value.getFullYear()}-${p(value.getMonth() + 1)}-${p(value.getDate())}`;
  }
  protected readonly sources = ['All', 'Portfolio', 'Watchlist'] as const;
  /** System lifecycle, separate from the investor's own My Action response. */
  protected readonly statuses: { value: SignalStatusFilter; label: string }[] = [
    { value: 'All', label: 'All statuses' },
    { value: 'Open', label: 'Open (Active)' },
    { value: 'Closed', label: 'Closed (any)' },
    { value: 'Acted', label: 'Acted' },
    { value: 'Dismissed', label: 'Dismissed' },
    { value: 'Expired', label: 'Expired' },
    { value: 'Invalidated', label: 'Invalidated' },
    { value: 'Superseded', label: 'Superseded' },
  ];
  protected readonly myActions: ('All' | SignalMyAction)[] = [
    'All',
    'Actioned',
    'Not Actioned',
    'Partial',
    'Pending',
  ];

  private readonly ticker$ = new Subject<string>();

  constructor() {
    this.ticker$
      .pipe(debounceTime(TICKER_DEBOUNCE_MS), distinctUntilChanged(), takeUntilDestroyed(inject(DestroyRef)))
      .subscribe((ticker) => this.store.setFilters({ ticker }));
  }

  ngOnInit(): void {
    // Drill-down from the Action Center: show that ticker's recommendations, unaudited ones included.
    const ticker = this.route.snapshot.queryParamMap.get('ticker');
    if (ticker) this.store.setFilters({ ticker, includeUnaudited: true });
    else this.store.load();
  }

  protected onTicker(value: string): void {
    this.ticker$.next(value);
  }

  protected onSort(sort: Sort): void {
    if (!sort.active || sort.direction === '') return;
    this.store.setSort(sort.active, sort.direction);
  }

  protected onPage(event: PageEvent): void {
    this.store.setPage(event.pageIndex + 1, event.pageSize);
  }

  protected dismiss(row: SignalAuditRow, event: Event): void {
    event.stopPropagation();
    void this.store.dismiss(row.id);
  }

  protected openAction(row: SignalAuditRow, event: Event): void {
    event.stopPropagation();
    this.dialog.open(SignalActionDialogComponent, {
      data: { row },
      width: '560px',
      maxWidth: '95vw',
      autoFocus: 'dialog',
    });
  }

  protected openDetails(row: SignalAuditRow): void {
    this.dialog.open(SignalDetailsDialogComponent, {
      data: { id: row.id },
      width: '640px',
      maxWidth: '95vw',
      autoFocus: 'dialog',
    });
  }

  /** Percent value for display, routed through demo-mode masking. */
  protected pct(h: SignalHorizon): number {
    return this.demoMode.maskPercent(h.pct ?? 0);
  }

  protected price(row: SignalAuditRow): number {
    return this.demoMode.maskValue(row.signalPrice);
  }

  protected horizonTip(h: SignalHorizon): string {
    if (h.status === 'Complete') return `Close ${h.evalPrice} on ${h.evalDate}`;
    return h.status === 'Pending' ? 'Evaluation period not complete yet' : 'Price history unavailable';
  }

  protected readonly exporting = signal(false);

  protected async exportCsv(): Promise<void> {
    await this.runExport((rows) => {
      const csv = signalsToCsv(rows, (v) => this.demoMode.maskValue(v));
      this.download(new Blob([csv], { type: 'text/csv;charset=utf-8;' }), 'csv');
    });
  }

  protected async exportExcel(): Promise<void> {
    await this.runExport((rows) => {
      const sheet = XLSX.utils.aoa_to_sheet([
        [...SIGNALS_EXPORT_HEADERS],
        ...signalsExportRows(rows, (v) => this.demoMode.maskValue(v)),
      ]);
      const book = XLSX.utils.book_new();
      XLSX.utils.book_append_sheet(book, sheet, 'Signal Tracker');
      XLSX.writeFile(book, this.exportName('xlsx'));
    });
  }

  private async runExport(write: (rows: SignalAuditRow[]) => void): Promise<void> {
    this.exporting.set(true);
    try {
      write(await this.store.fetchAllRows());
    } catch {
      this.snackBar.open('Export failed', 'Close', { duration: 4000 });
    } finally {
      this.exporting.set(false);
    }
  }

  private exportName(ext: string): string {
    return `signals-audit-${new Date().toISOString().slice(0, 10)}.${ext}`;
  }

  private download(blob: Blob, ext: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = this.exportName(ext);
    a.click();
    URL.revokeObjectURL(url);
  }

  protected roleClass(role: string | null | undefined): string {
    switch (role) {
      case 'Core':
        return 'role-core';
      case 'Strategic':
        return 'role-strategic';
      case 'Strategic-Income':
        return 'role-strategic-income';
      case 'Swing':
        return 'role-swing';
      case 'Speculative':
        return 'role-speculative';
      case 'Options':
        return 'role-options';
      default:
        return '';
    }
  }

  protected actionClass(action: SignalMyAction): string {
    switch (action) {
      case 'Actioned':
        return 'act-actioned';
      case 'Partial':
        return 'act-partial';
      case 'Not Actioned':
        return 'act-not';
      default:
        return 'act-pending';
    }
  }

  protected linkLabel(row: SignalAuditRow): string {
    switch (row.linkKind) {
      case 'TransactionVerified':
        return 'Linked to a matching transaction';
      case 'ManualConfirmed':
        return 'Confirmed manually (no linked transaction)';
      case 'LinkBroken':
        return 'Linked transaction no longer matches';
      default:
        return '';
    }
  }
}
