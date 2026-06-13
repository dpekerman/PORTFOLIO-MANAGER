import { Injectable, effect, signal } from '@angular/core';

const STORAGE_KEY = 'pm_demo_mode';

/**
 * Demo Mode — masks sensitive portfolio data so the app can be shown to others
 * without revealing real positions or dollar amounts.
 *
 * Strategy:
 *   - All dollar values are multiplied by a consistent random factor (0.3–0.7)
 *     stored in sessionStorage so it stays consistent within one session
 *     but changes on each new browser session.
 *   - Percentage values (returns, allocations) are randomized within ±30%.
 *   - Company names and symbols are kept (they're not sensitive).
 *   - A visible "DEMO MODE" banner is shown in the toolbar.
 */
@Injectable({ providedIn: 'root' })
export class DemoModeService {
  private readonly _isDemoMode = signal<boolean>(localStorage.getItem(STORAGE_KEY) === 'true');

  readonly isDemoMode = this._isDemoMode.asReadonly();

  /** Consistent multiplicative factor for the current browser session */
  private readonly _factor = this.getOrCreateFactor();

  constructor() {
    effect(() => {
      localStorage.setItem(STORAGE_KEY, String(this._isDemoMode()));
    });
  }

  toggle(): void {
    this._isDemoMode.update((v) => !v);
  }

  enable(): void {
    this._isDemoMode.set(true);
  }

  disable(): void {
    this._isDemoMode.set(false);
  }

  /**
   * Masks a currency value when demo mode is active.
   * Returns the masked number (caller formats it).
   */
  maskValue(value: number): number {
    if (!this._isDemoMode()) return value;
    return Math.round(value * this._factor);
  }

  /**
   * Masks a percentage value (keeps sign, varies magnitude).
   */
  maskPercent(value: number): number {
    if (!this._isDemoMode()) return value;
    // Preserve sign, randomize magnitude in ±30% of the factor range
    const noise = 0.7 + (this._factor % 0.3);
    return Math.round(value * noise * 10) / 10;
  }

  /** Returns a display-safe string for a dollar value in demo mode */
  displayValue(value: number, decimals = 0): string {
    if (!this._isDemoMode())
      return value.toLocaleString('en-US', {
        minimumFractionDigits: decimals,
        maximumFractionDigits: decimals,
      });
    return this.maskValue(value).toLocaleString('en-US', {
      minimumFractionDigits: decimals,
      maximumFractionDigits: decimals,
    });
  }

  private getOrCreateFactor(): number {
    const key = 'pm_demo_factor';
    const stored = sessionStorage.getItem(key);
    if (stored) {
      const v = parseFloat(stored);
      if (v >= 0.3 && v <= 0.7) return v;
    }
    // Generate a new factor for this session
    const factor = 0.3 + Math.random() * 0.4;
    sessionStorage.setItem(key, String(factor));
    return factor;
  }
}
