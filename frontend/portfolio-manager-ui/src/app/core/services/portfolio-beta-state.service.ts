import { Injectable, effect, inject, signal } from '@angular/core';
import { PortfolioBetaResult } from '../models/portfolio.models';
import { GlobalLoadingService } from './global-loading.service';
import { PortfolioBetaApiService } from './portfolio-beta-api.service';

const BETA_OVERRIDES_KEY = 'pm_beta_overrides_v1';

@Injectable({ providedIn: 'root' })
export class PortfolioBetaStateService {
  private readonly api = inject(PortfolioBetaApiService);
  private readonly globalLoading = inject(GlobalLoadingService);
  private readonly _loadingSync = effect((onCleanup) => {
    if (this._loading()) {
      this.globalLoading.push();
      onCleanup(() => this.globalLoading.pop());
    }
  });

  private readonly _result = signal<PortfolioBetaResult | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);

  /** User-editable beta overrides, keyed by symbol (upper-case). Persisted to localStorage. */
  private readonly _betaOverrides = signal<Record<string, number>>(this.loadOverrides());
  /** Fetched beta values from Yahoo Finance, keyed by symbol (upper-case). */
  private readonly _fetchedBetas = signal<Record<string, number>>({});

  readonly result = this._result.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly betaOverrides = this._betaOverrides.asReadonly();
  readonly fetchedBetas = this._fetchedBetas.asReadonly();

  /** Returns the effective beta for a symbol (override wins over fetched). */
  betaForSymbol(symbol: string): number | null {
    const key = symbol.toUpperCase();
    const override = this._betaOverrides()[key];
    if (override !== undefined) return override;
    const fetched = this._fetchedBetas()[key];
    return fetched !== undefined ? fetched : null;
  }

  setOverride(symbol: string, beta: number | null): void {
    const key = symbol.toUpperCase();
    this._betaOverrides.update((prev) => {
      const next = { ...prev };
      if (beta === null) delete next[key];
      else next[key] = beta;
      return next;
    });
    localStorage.setItem(BETA_OVERRIDES_KEY, JSON.stringify(this._betaOverrides()));
    // Reload beta card with new override
    this.load(true);
  }

  load(force = false): void {
    if (this._loading() && !force) return;
    this._loading.set(true);
    this._error.set(null);
    const overrides = this._betaOverrides();
    this.api.getBeta(Object.keys(overrides).length > 0 ? overrides : undefined).subscribe({
      next: (r) => {
        this._result.set(r);
        // Populate fetched betas from ALL contributors (not just top 5)
        const fetched: Record<string, number> = {};
        for (const c of r.topContributors) fetched[c.symbol.toUpperCase()] = c.beta;
        this._fetchedBetas.update((prev) => ({ ...prev, ...fetched }));
        this._loading.set(false);
      },
      error: (err) => {
        console.error('[PortfolioBeta] Failed to load beta', err);
        this._error.set('Failed to load portfolio beta');
        this._loading.set(false);
      },
    });
  }

  private loadOverrides(): Record<string, number> {
    try {
      const raw = localStorage.getItem(BETA_OVERRIDES_KEY);
      return raw ? JSON.parse(raw) : {};
    } catch {
      return {};
    }
  }
}
