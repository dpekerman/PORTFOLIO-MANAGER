import { Injectable, computed, inject, signal } from '@angular/core';
import { toObservable } from '@angular/core/rxjs-interop';
import {
  Subject,
  catchError,
  distinctUntilChanged,
  filter,
  forkJoin,
  map,
  of,
  switchMap,
  tap,
} from 'rxjs';
import { RsiScanResult } from '../models/portfolio.models';
import { PortfolioApiService } from './portfolio-api.service';
import { WatchlistStateService } from './watchlist-state.service';

/** Root-scoped so RSI results survive navigation — no re-scan when returning to the watchlist page. */
@Injectable({ providedIn: 'root' })
export class WatchlistRsiStateService {
  private readonly watchlistState = inject(WatchlistStateService);
  private readonly api = inject(PortfolioApiService);

  private readonly _rsiMap = signal<Map<string, RsiScanResult>>(new Map());
  private readonly _loading = signal(false);

  readonly rsiMap = this._rsiMap.asReadonly();
  readonly rsiLoading = this._loading.asReadonly();

  private readonly rsiTrigger$ = new Subject<string[]>();

  /** Sorted symbol key — changes only when symbols are added or removed. */
  private readonly _symbolKey = computed(() =>
    [...this.watchlistState.items().map((w) => w.item.symbol)].sort().join(','),
  );

  /** Force a fresh RSI scan (e.g., when the user clicks manual refresh). */
  triggerRefresh(symbols: string[]): void {
    if (symbols.length > 0) this.rsiTrigger$.next(symbols);
  }

  constructor() {
    // Pipeline: batches symbols (max 50/request), cancels in-flight on new trigger.
    this.rsiTrigger$
      .pipe(
        tap((symbols) => {
          console.log(
            `[Watchlist RSI] Scan started — ${symbols.length} symbols @ ${new Date().toISOString()}`,
          );
          this._loading.set(true);
        }),
        switchMap((symbols) => {
          const batchSize = 50;
          const batches: string[][] = [];
          for (let i = 0; i < symbols.length; i += batchSize)
            batches.push(symbols.slice(i, i + batchSize));

          return forkJoin(
            batches.map((batch) =>
              this.api.analyzeSymbols(batch, 30, 75, 'Enhanced').pipe(
                catchError((err) => {
                  console.warn('[Watchlist RSI] Batch fetch failed', err);
                  return of([] as RsiScanResult[]);
                }),
              ),
            ),
          ).pipe(map((batchResults) => batchResults.flat()));
        }),
      )
      .subscribe({
        next: (results) => {
          const map = new Map<string, RsiScanResult>();
          for (const r of results) map.set(r.symbol.toUpperCase(), r);
          this._rsiMap.set(map);
          this._loading.set(false);
          console.log(
            `[Watchlist RSI] Scan complete — ${results.length} results @ ${new Date().toISOString()}`,
          );
        },
        error: () => {
          this._loading.set(false);
          console.error(`[Watchlist RSI] Scan failed @ ${new Date().toISOString()}`);
        },
      });

    // Trigger RSI only when the set of symbols actually changes (add/remove).
    // Role updates and quote refreshes do NOT change _symbolKey → no spurious scans.
    toObservable(this._symbolKey)
      .pipe(
        distinctUntilChanged(),
        filter((key) => key.length > 0),
        map((key) => key.split(',')),
      )
      .subscribe((symbols) => this.rsiTrigger$.next(symbols));
  }
}
