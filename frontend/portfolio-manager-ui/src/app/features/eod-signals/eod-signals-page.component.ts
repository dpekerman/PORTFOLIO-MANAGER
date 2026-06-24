import { DatePipe, DecimalPipe, NgClass } from '@angular/common';
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
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute } from '@angular/router';
import { debounceTime, distinctUntilChanged, interval } from 'rxjs';
import {
  DailySignal,
  DailySignalPagedResponse,
  EodSignalFilters,
  EodSignalsMeta,
  SignalState,
} from '../../core/models/portfolio.models';
import { PortfolioApiService } from '../../core/services/portfolio-api.service';
import { ScannerStateService } from '../../core/services/scanner-state.service';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../shared/confirm-dialog/confirm-dialog.component';

type SortCol =
  | 'signalDate'
  | 'symbol'
  | 'scanType'
  | 'signalType'
  | 'rsi'
  | 'price'
  | 'reversalProbability'
  | 'volumeSignal'
  | 'ruleVersion'
  | 'signalState';
type SortDir = 'asc' | 'desc';

@Component({
  selector: 'app-eod-signals-page',
  templateUrl: './eod-signals-page.component.html',
  styleUrl: './eod-signals-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    NgClass,
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSortModule,
    MatTableModule,
    MatTooltipModule,
  ],
})
export class EodSignalsPageComponent implements OnInit {
  private readonly api = inject(PortfolioApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly scannerState = inject(ScannerStateService);

  // state
  protected readonly loading = signal(false);
  protected readonly response = signal<DailySignalPagedResponse | null>(null);
  protected readonly meta = signal<EodSignalsMeta | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly seeding = signal(false);
  protected readonly persistingNow = signal(false);
  protected readonly lastCheckedAt = signal<Date | null>(null);
  protected readonly autoRefreshing = signal(false);

  // EOD window status (reuses scanner state service — already polled every 30 s)
  protected readonly eodWindowActive = computed(() => this.scannerState.eodWindowActive());

  // filters
  protected readonly tickerControl = new FormControl<string>('');
  protected readonly scanTypeFilter = signal<string>('');
  protected readonly signalTypeFilter = signal<string>('');
  protected readonly signalStateFilter = signal<string>('');
  protected readonly ruleVersionFilter = signal<string>('');
  protected readonly dateFromControl = new FormControl<Date | null>(null);
  protected readonly dateToControl = new FormControl<Date | null>(null);
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(50);

  // sort
  protected readonly sortCol = signal<SortCol>('signalDate');
  protected readonly sortDir = signal<SortDir>('desc');

  // computed
  protected readonly rawSignals = computed(() => this.response()?.items ?? []);
  protected readonly totalCount = computed(() => this.response()?.totalCount ?? 0);

  protected readonly sortedSignals = computed<DailySignal[]>(() => {
    const col = this.sortCol();
    const dir = this.sortDir() === 'asc' ? 1 : -1;
    return [...this.rawSignals()].sort((a, b) => {
      let av: string | number;
      let bv: string | number;
      switch (col) {
        case 'signalDate':
          av = a.signalDate;
          bv = b.signalDate;
          break;
        case 'symbol':
          av = a.symbol;
          bv = b.symbol;
          break;
        case 'scanType':
          av = a.scanType;
          bv = b.scanType;
          break;
        case 'signalType':
          av = a.signalType;
          bv = b.signalType;
          break;
        case 'rsi':
          av = a.rsi;
          bv = b.rsi;
          break;
        case 'price':
          av = a.price;
          bv = b.price;
          break;
        case 'reversalProbability':
          av = a.reversalProbability;
          bv = b.reversalProbability;
          break;
        case 'volumeSignal':
          av = a.volumeSignal;
          bv = b.volumeSignal;
          break;
        case 'ruleVersion':
          av = a.ruleVersion;
          bv = b.ruleVersion;
          break;
        case 'signalState':
          av = a.signalState;
          bv = b.signalState;
          break;
        default:
          av = a.signalDate;
          bv = b.signalDate;
      }
      if (typeof av === 'number' && typeof bv === 'number') return (av - bv) * dir;
      return String(av).localeCompare(String(bv)) * dir;
    });
  });

  protected readonly displayedColumns: string[] = [
    'signalDate',
    'symbol',
    'scanType',
    'signalType',
    'rsi',
    'price',
    'reversalProbability',
    'volumeSignal',
    'ruleVersion',
    'signalState',
    'actions',
  ];

  protected readonly scanTypeOptions = ['Oversold', 'Overbought'];
  protected readonly signalTypeOptions = ['EodConfirm', 'Confirmed', 'EarlyWarning'];
  protected readonly signalStateOptions: SignalState[] = [
    'Active',
    'FollowThrough',
    'Invalidated',
    'Expired',
    'Reversed',
  ];
  protected readonly ruleVersionOptions = ['Legacy', 'Enhanced'];

  protected readonly hasFilters = computed(
    () =>
      !!(
        this.tickerControl.value?.trim() ||
        this.scanTypeFilter() ||
        this.signalTypeFilter() ||
        this.signalStateFilter() ||
        this.ruleVersionFilter() ||
        this.dateFromControl.value ||
        this.dateToControl.value
      ),
  );

  ngOnInit(): void {
    const tickerParam = this.route.snapshot.queryParamMap.get('ticker');
    if (tickerParam) this.tickerControl.setValue(tickerParam, { emitEvent: false });

    this.api
      .getEodSignalsMeta()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: (m) => this.meta.set(m) });

    this.tickerControl.valueChanges
      .pipe(debounceTime(400), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.pageIndex.set(0);
        this.loadSignals();
      });

    this.dateFromControl.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.pageIndex.set(0);
      this.loadSignals();
    });

    this.dateToControl.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.pageIndex.set(0);
      this.loadSignals();
    });

    // ── Auto-poll every 30 s: silently check for new records ────────────────
    interval(30_000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.pollForUpdates());

    this.loadSignals();
  }

  private dateToStr(d: Date | null | undefined): string | undefined {
    if (!d) return undefined;
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

  protected loadSignals(): void {
    this.loading.set(true);
    this.error.set(null);
    const filters: EodSignalFilters = {
      ticker: this.tickerControl.value?.trim() || undefined,
      scanType: this.scanTypeFilter() || undefined,
      signalType: this.signalTypeFilter() || undefined,
      signalState: this.signalStateFilter() || undefined,
      ruleVersion: this.ruleVersionFilter() || undefined,
      dateFrom: this.dateToStr(this.dateFromControl.value),
      dateTo: this.dateToStr(this.dateToControl.value),
      page: this.pageIndex() + 1,
      pageSize: this.pageSize(),
    };
    this.api
      .getEodSignals(filters)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (r) => {
          this.response.set(r);
          this.loading.set(false);
          this.lastCheckedAt.set(new Date());
        },
        error: () => {
          this.error.set('Failed to load EOD signals.');
          this.loading.set(false);
        },
      });
  }

  /** Silent background poll: fetches only meta (totalCount).
   *  If new records were added since the last full load, triggers a full reload. */
  private pollForUpdates(): void {
    const prevCount = this.totalCount();
    this.autoRefreshing.set(true);
    this.api
      .getEodSignalsMeta()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (m) => {
          this.meta.set(m);
          this.autoRefreshing.set(false);
          this.lastCheckedAt.set(new Date());
          if (m.totalCount > prevCount) {
            const diff = m.totalCount - prevCount;
            this.snackBar
              .open(
                `${diff} new EOD signal${diff > 1 ? 's' : ''} added by background scanner`,
                'Refresh',
                { duration: 8000 },
              )
              .onAction()
              .pipe(takeUntilDestroyed(this.destroyRef))
              .subscribe(() => this.loadSignals());
            this.loadSignals();
          }
        },
        error: () => this.autoRefreshing.set(false),
      });
  }

  protected onFilterChange(): void {
    this.pageIndex.set(0);
    this.loadSignals();
  }

  protected onPage(e: PageEvent): void {
    this.pageIndex.set(e.pageIndex);
    this.pageSize.set(e.pageSize);
    this.loadSignals();
  }

  protected onMatSortChange(sort: Sort): void {
    if (!sort.active || sort.direction === '') return;
    this.sortCol.set(sort.active as SortCol);
    this.sortDir.set(sort.direction as SortDir);
  }

  protected persistNow(): void {
    this.persistingNow.set(true);
    this.api
      .persistEodSignalsNow()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (r) => {
          this.persistingNow.set(false);
          this.snackBar.open(
            `Persisted ${r.persisted} signal(s) — ${r.eodConfirm} EodConfirm, ${r.confirmed} Confirmed.`,
            'OK',
            { duration: 5000 },
          );
          this.loadSignals();
          this.refreshMeta();
        },
        error: (err) => {
          this.persistingNow.set(false);
          const msg = err?.error?.detail ?? err?.error?.title ?? 'Persist failed — check backend.';
          this.snackBar.open(msg, 'Dismiss', { duration: 5000 });
        },
      });
  }

  protected clearFilters(): void {
    this.tickerControl.setValue('', { emitEvent: false });
    this.scanTypeFilter.set('');
    this.signalTypeFilter.set('');
    this.signalStateFilter.set('');
    this.ruleVersionFilter.set('');
    this.dateFromControl.setValue(null, { emitEvent: false });
    this.dateToControl.setValue(null, { emitEvent: false });
    this.pageIndex.set(0);
    this.loadSignals();
  }

  private refreshMeta(): void {
    this.api
      .getEodSignalsMeta()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: (m) => this.meta.set(m) });
  }

  protected updateState(row: DailySignal, newState: SignalState): void {
    this.api
      .updateEodSignalState(row.id, newState)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.loadSignals(),
        error: () => this.snackBar.open('Failed to update state.', 'Dismiss', { duration: 3000 }),
      });
  }

  protected deleteSignal(row: DailySignal): void {
    this.dialog
      .open<ConfirmDialogComponent, ConfirmDialogData, boolean>(ConfirmDialogComponent, {
        data: {
          title: 'Delete Signal',
          message: `Delete the ${row.scanType} signal for ${row.symbol} on ${row.signalDate}?`,
          confirmLabel: 'Delete',
          danger: true,
        },
        width: '380px',
      })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((confirmed) => {
        if (!confirmed) return;
        this.api
          .deleteEodSignal(row.id)
          .pipe(takeUntilDestroyed(this.destroyRef))
          .subscribe({
            next: () => {
              this.loadSignals();
              this.refreshMeta();
            },
            error: () =>
              this.snackBar.open('Failed to delete signal.', 'Dismiss', { duration: 3000 }),
          });
      });
  }

  protected deleteAll(): void {
    const ticker = this.tickerControl.value?.trim() || undefined;
    const dateFrom = this.dateToStr(this.dateFromControl.value);
    const dateTo = this.dateToStr(this.dateToControl.value);
    const count = this.totalCount();
    const filterMsg = ticker ? ` for ${ticker}` : '';

    this.dialog
      .open<ConfirmDialogComponent, ConfirmDialogData, boolean>(ConfirmDialogComponent, {
        data: {
          title: 'Delete All Signals',
          message: `Permanently delete all ${count} signal record(s)${filterMsg}? This cannot be undone.`,
          confirmLabel: 'Delete All',
          danger: true,
        },
        width: '420px',
      })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((confirmed) => {
        if (!confirmed) return;
        this.api
          .deleteAllEodSignals(ticker, dateFrom, dateTo)
          .pipe(takeUntilDestroyed(this.destroyRef))
          .subscribe({
            next: (r) => {
              this.snackBar.open(`Deleted ${r.deleted} signal(s).`, 'OK', { duration: 3000 });
              this.loadSignals();
              this.refreshMeta();
            },
            error: () => this.snackBar.open('Delete failed.', 'Dismiss', { duration: 3000 }),
          });
      });
  }

  protected seedTestData(): void {
    this.seeding.set(true);
    this.api
      .seedEodSignals()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (r) => {
          this.seeding.set(false);
          this.snackBar.open(`Seeded ${r.seeded} test signal(s).`, 'OK', { duration: 4000 });
          this.loadSignals();
          this.refreshMeta();
        },
        error: (err) => {
          this.seeding.set(false);
          const msg =
            err?.error?.title ??
            err?.error?.detail ??
            'Seed failed — check that the backend is running.';
          this.snackBar.open(msg, 'Dismiss', { duration: 5000 });
        },
      });
  }

  protected scanTypeClass(s: string): string {
    return s === 'Oversold' ? 'tag-oversold' : 'tag-overbought';
  }
  protected signalTypeClass(s: string): string {
    if (s === 'EodConfirm') return 'tag-eod-confirm';
    if (s === 'Confirmed') return 'tag-confirmed';
    return 'tag-early-warning';
  }
  protected signalStateClass(s: string): string {
    switch (s) {
      case 'FollowThrough':
        return 'state-follow-through';
      case 'Invalidated':
        return 'state-invalidated';
      case 'Expired':
        return 'state-expired';
      case 'Reversed':
        return 'state-reversed';
      default:
        return 'state-active';
    }
  }
  protected reversalClass(p: string): string {
    if (p === 'High') return 'tag-prob-high';
    if (p === 'Medium') return 'tag-prob-medium';
    return 'tag-prob-low';
  }
}
