import { DatePipe, DecimalPipe } from '@angular/common';
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
import * as XLSX from 'xlsx';
import {
  DailySignal,
  DailySignalPagedResponse,
  EodSignalFilters,
  EodSignalsMeta,
  SignalState,
} from '../../core/models/portfolio.models';
import { GridColumnService } from '../../core/services/grid-column.service';
import { PortfolioApiService } from '../../core/services/portfolio-api.service';
import { ScannerStateService } from '../../core/services/scanner-state.service';
import { GridColumnButtonComponent } from '../../shared/column-config-dialog/grid-column-btn.component';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../shared/confirm-dialog/confirm-dialog.component';

type SortCol =
  | 'signalDate'
  | 'daysPassed'
  | 'symbol'
  | 'scanType'
  | 'signalType'
  | 'trendShift'
  | 'rsi'
  | 'rsiDelta1D'
  | 'entryPrice'
  | 'stopLoss'
  | 'riskPerShare'
  | 'riskPercent'
  | 'sma200'
  | 'price'
  | 'lastPrice'
  | 'priceDiff'
  | 'diffPct'
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
    GridColumnButtonComponent,
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
  /** Current price map: symbol (upper) → current price fetched after signals load */
  protected readonly currentPriceMap = signal<Map<string, number>>(new Map());
  /** Tracks the last unfiltered total count seen during background polling. Used to detect genuine new signals
   *  without being affected by active filter state (which changes the filtered totalCount). */
  private readonly lastKnownMetaCount = signal<number | null>(null);

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
        case 'daysPassed':
          av = this.daysPassed(a);
          bv = this.daysPassed(b);
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
        case 'trendShift':
          av = a.trendShift ?? '';
          bv = b.trendShift ?? '';
          break;
        case 'rsi':
          av = a.rsi;
          bv = b.rsi;
          break;
        case 'rsiDelta1D':
          av = a.rsiDelta1D ?? 0;
          bv = b.rsiDelta1D ?? 0;
          break;
        case 'entryPrice':
          av = a.entryPrice ?? 0;
          bv = b.entryPrice ?? 0;
          break;
        case 'stopLoss':
          av = a.stopLossPrice ?? 0;
          bv = b.stopLossPrice ?? 0;
          break;
        case 'riskPerShare':
          av = a.riskPerShare ?? 0;
          bv = b.riskPerShare ?? 0;
          break;
        case 'riskPercent':
          av = a.entryPrice && a.riskPerShare ? a.riskPerShare / a.entryPrice : 0;
          bv = b.entryPrice && b.riskPerShare ? b.riskPerShare / b.entryPrice : 0;
          break;
        case 'sma200':
          av = a.sma200 ?? 0;
          bv = b.sma200 ?? 0;
          break;
        case 'price':
          av = a.price;
          bv = b.price;
          break;
        case 'lastPrice':
          av = this.lastPrice(a) ?? 0;
          bv = this.lastPrice(b) ?? 0;
          break;
        case 'priceDiff':
          av = this.priceDiff(a) ?? 0;
          bv = this.priceDiff(b) ?? 0;
          break;
        case 'diffPct':
          av = this.diffPct(a) ?? 0;
          bv = this.diffPct(b) ?? 0;
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

  /** Days from signal date to today */
  protected daysPassed(row: DailySignal): number {
    const signalMs = new Date(row.signalDate).getTime();
    return Math.floor((Date.now() - signalMs) / (1000 * 60 * 60 * 24));
  }

  /** Current price from the fetched price map */
  protected lastPrice(row: DailySignal): number | null {
    return this.currentPriceMap().get(row.symbol.toUpperCase()) ?? null;
  }

  /** Price Diff = Last Price - Signal Price */
  protected priceDiff(row: DailySignal): number | null {
    const lp = this.lastPrice(row);
    if (lp === null) return null;
    return lp - row.price;
  }

  /** Diff % = Price Diff / Signal Price */
  protected diffPct(row: DailySignal): number | null {
    const pd = this.priceDiff(row);
    if (pd === null || row.price === 0) return null;
    return (pd / row.price) * 100;
  }

  protected readonly displayedColumns = inject(GridColumnService).getColumnKeys('eod-signals');

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
      .subscribe({
        next: (m) => {
          this.meta.set(m);
          this.lastKnownMetaCount.set(m.totalCount);
        },
      });

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
          this.fetchCurrentPrices(r.items);
        },
        error: () => {
          this.error.set('Failed to load EOD signals.');
          this.loading.set(false);
        },
      });
  }

  /** Fetches current prices for the given signals and stores in currentPriceMap. */
  private fetchCurrentPrices(signals: DailySignal[]): void {
    const symbols = [...new Set(signals.map((s) => s.symbol.toUpperCase()))];
    if (symbols.length === 0) return;
    const batchSize = 50;
    const batches: string[][] = [];
    for (let i = 0; i < symbols.length; i += batchSize)
      batches.push(symbols.slice(i, i + batchSize));
    const merged = new Map<string, number>(this.currentPriceMap());
    let completed = 0;
    for (const batch of batches) {
      this.api
        .analyzeSymbols(batch, 30, 75, 'Enhanced')
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (results) => {
            for (const r of results) merged.set(r.symbol.toUpperCase(), r.currentPrice);
            completed++;
            if (completed === batches.length) this.currentPriceMap.set(new Map(merged));
          },
          error: () => {
            completed++;
            if (completed === batches.length) this.currentPriceMap.set(new Map(merged));
          },
        });
    }
  }

  /** Silent background poll: fetches only meta (totalCount).
   *  Compares against lastKnownMetaCount (unfiltered) to avoid false positives
   *  when the user has active filters that reduce the displayed count. */
  private pollForUpdates(): void {
    this.autoRefreshing.set(true);
    this.api
      .getEodSignalsMeta()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (m) => {
          const prev = this.lastKnownMetaCount();
          this.meta.set(m);
          this.autoRefreshing.set(false);
          this.lastCheckedAt.set(new Date());
          // Only show snack-bar when total unfiltered count has actually increased
          if (prev !== null && m.totalCount > prev) {
            const diff = m.totalCount - prev;
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
          this.lastKnownMetaCount.set(m.totalCount);
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
          const detail =
            r.persisted > 0
              ? `${r.bullBearTurnCount} Bull/Bear Turn candidate(s) evaluated`
              : 'No signals met Stage-2 criteria';
          this.snackBar.open(`Persisted ${r.persisted} signal(s) — ${detail}.`, 'OK', {
            duration: 5000,
          });
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
      .subscribe({
        next: (m) => {
          this.meta.set(m);
          this.lastKnownMetaCount.set(m.totalCount);
        },
      });
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

  protected trendShiftClass(trendShift: string): string {
    if (trendShift.includes('Bull Turn') || trendShift.includes('Bear Turn'))
      return 'tag-trend-bull';
    if (trendShift.includes('Still Falling') || trendShift.includes('Still Rising'))
      return 'tag-trend-bear';
    if (trendShift.includes('Stabilizing')) return 'tag-trend-neutral';
    return '';
  }

  /** Risk % = RiskPerShare / EntryPrice × 100. Null when either value is missing. */
  protected riskPercent(row: DailySignal): number | null {
    if (row.riskPerShare == null || row.entryPrice == null || row.entryPrice === 0) return null;
    return (row.riskPerShare / row.entryPrice) * 100;
  }

  /**
   * Turn velocity label derived from RsiDelta1D, matching backend StagedSignalService thresholds.
   * Returns "" when not applicable.
   */
  protected turnStrength(row: DailySignal): string {
    const delta = row.rsiDelta1D;
    if (delta === null || delta === undefined) return '';
    const isTurn = row.scanType === 'Oversold' ? delta > 0.25 : delta < -0.25;
    if (!isTurn) return '';
    const abs = Math.abs(delta);
    if (abs >= 10) return 'Explosive';
    if (abs >= 5) return 'Strong';
    if (abs >= 1) return 'Normal';
    return 'Early';
  }

  /** "Elevated" when TurnStrength is Explosive; "" otherwise. */
  protected chaseRisk(row: DailySignal): string {
    return this.turnStrength(row) === 'Explosive' ? 'Elevated' : '';
  }

  protected exportToExcel(): void {
    const today = new Date().toISOString().slice(0, 10);
    const data = this.sortedSignals().map((r) => ({
      Date: r.signalDate,
      Ticker: r.symbol,
      'Scan Type': r.scanType,
      'Signal Type': r.signalType,
      'Trend Shift': r.trendShift ?? '',
      'Turn Strength': this.turnStrength(r),
      'RSI (14)': r.rsi != null ? +r.rsi.toFixed(2) : '',
      'RSI Δ1D': r.rsiDelta1D != null ? +r.rsiDelta1D.toFixed(3) : '',
      'Entry Price': r.entryPrice ?? '',
      'Stop Loss': r.stopLossPrice ?? '',
      'Risk / Share': r.riskPerShare != null ? +r.riskPerShare.toFixed(3) : '',
      'Risk %': this.riskPercent(r) != null ? +this.riskPercent(r)!.toFixed(2) : '',
      'SMA 200': r.sma200 ?? '',
      'Signal Price': r.price,
      Volume: r.volumeSignal ?? '',
      'Reversal P.': r.reversalProbability,
      Mode: r.ruleVersion,
      State: r.signalState,
      'Days Passed': this.daysPassed(r),
    }));
    const ws = XLSX.utils.json_to_sheet(data);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'EOD Signals');
    XLSX.writeFile(wb, `eod-signals-${today}.xlsx`);
  }
}
