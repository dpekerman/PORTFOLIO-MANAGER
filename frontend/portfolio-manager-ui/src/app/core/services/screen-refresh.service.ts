import { signal } from '@angular/core';

export type ScreenName = 'portfolio' | 'watchlist' | 'scanner' | 'eod-signals' | 'value-screener';

export interface ScreenRefreshState {
  screenName: ScreenName;
  isRefreshing: boolean;
  progress: { current: number; total: number };
  currentItem: string | null;
  error: string | null;
  cancel(): void;
}

/**
 * Per-screen refresh state management with AbortController-based cancellation.
 * Typically instantiated per component/screen for independent refresh tracking.
 * Use: const screenRefresh = new ScreenRefreshService('portfolio');
 */
export class ScreenRefreshService {
  private readonly _screenName = signal<ScreenName>('portfolio');
  private readonly _isRefreshing = signal(false);
  private readonly _progress = signal({ current: 0, total: 0 });
  private readonly _currentItem = signal<string | null>(null);
  private readonly _error = signal<string | null>(null);
  private _abortController: AbortController | null = null;

  readonly screenName = this._screenName.asReadonly();
  readonly isRefreshing = this._isRefreshing.asReadonly();
  readonly progress = this._progress.asReadonly();
  readonly currentItem = this._currentItem.asReadonly();
  readonly error = this._error.asReadonly();

  constructor(screenName: ScreenName = 'portfolio') {
    this._screenName.set(screenName);
  }

  startRefresh(total: number): void {
    this._isRefreshing.set(true);
    this._error.set(null);
    this._progress.set({ current: 0, total });
    this._currentItem.set(null);
    this._abortController = new AbortController();
  }

  updateProgress(current: number): void {
    this._progress.update((p) => ({ ...p, current }));
  }

  setCurrentItem(item: string | null): void {
    this._currentItem.set(item);
  }

  completeRefresh(): void {
    this._isRefreshing.set(false);
    this._currentItem.set(null);
    this._abortController = null;
  }

  errorRefresh(message: string): void {
    this._isRefreshing.set(false);
    this._error.set(message);
    this._abortController = null;
  }

  cancel(): void {
    if (this._abortController) {
      this._abortController.abort();
    }
    this._isRefreshing.set(false);
    this._error.set(null);
    this._currentItem.set(null);
    this._progress.set({ current: 0, total: 0 });
    this._abortController = null;
  }

  getAbortSignal(): AbortSignal | null {
    return this._abortController?.signal ?? null;
  }
}
