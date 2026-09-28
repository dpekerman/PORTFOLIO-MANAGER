import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { RsiScanResult } from '../models/portfolio.models';
import { PortfolioApiService } from './portfolio-api.service';
import { PortfolioFinalActionSyncService } from './portfolio-final-action-sync.service';
import { PortfolioStateService } from './portfolio-state.service';
import { ScannerStateService } from './scanner-state.service';

/**
 * Fetches full Enhanced-mode RSI/technical data for every open portfolio symbol (not just the
 * oversold/overbought scanner extremes), then pushes computed Final Actions to the backend.
 * Runs app-wide — not tied to the Portfolio page being open — so Dashboard Action Center reflects
 * today's recommendation the moment the app loads or refreshes.
 */
@Injectable({ providedIn: 'root' })
export class PortfolioRsiAnalysisService {
  private readonly api = inject(PortfolioApiService);
  private readonly portfolio = inject(PortfolioStateService);
  private readonly scanner = inject(ScannerStateService);
  private readonly finalActionSync = inject(PortfolioFinalActionSyncService);

  private readonly _resultMap = signal<Map<string, RsiScanResult>>(new Map());

  /** Full result map: symbol (upper) → RsiScanResult, covering ALL portfolio symbols. */
  readonly fullMap = computed<Map<string, RsiScanResult>>(() => {
    const map = new Map<string, RsiScanResult>();
    for (const [sym, r] of this._resultMap()) map.set(sym, r);
    for (const r of [...this.scanner.oversold(), ...this.scanner.overbought()])
      map.set(r.symbol.toUpperCase(), r);
    return map;
  });

  constructor() {
    // Whenever portfolio summaries change (app boot, manual refresh, auto-refresh timer),
    // load RSI for ALL non-manual symbols. Batched in groups of 50 (backend limit).
    effect(() => {
      const symbols = this.portfolio
        .summaries()
        .filter((s) => !s.item.isManual && s.item.transactionType !== 'CLOSE')
        .map((s) => s.item.symbol);
      if (symbols.length === 0) return;

      const batchSize = 50;
      const batches: string[][] = [];
      for (let i = 0; i < symbols.length; i += batchSize)
        batches.push(symbols.slice(i, i + batchSize));

      const merged = new Map<string, RsiScanResult>();
      let completed = 0;
      for (const batch of batches) {
        this.api.analyzeSymbols(batch, 30, 75, 'Enhanced').subscribe({
          next: (results) => {
            for (const r of results) merged.set(r.symbol.toUpperCase(), r);
            completed++;
            if (completed === batches.length) this.onBatchComplete(merged);
          },
          error: (err) => {
            console.warn('Portfolio RSI batch fetch failed', err);
            completed++;
            if (completed === batches.length) this.onBatchComplete(merged);
          },
        });
      }
    });
  }

  private onBatchComplete(merged: Map<string, RsiScanResult>): void {
    this._resultMap.set(new Map(merged));
    this.finalActionSync.computeAndSync(this.fullMap());
  }
}
