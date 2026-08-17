import { Injectable, effect, signal } from '@angular/core';

const STORAGE_KEY = 'pm_demo_mode';
const STYLE_KEY = 'pm_demo_style';

export type DemoStyle = 'blur' | 'fake';

/**
 * Demo Mode â€” two masking strategies:
 *  'blur'  â€” multiplies real values by a session factor (0.3â€“0.7); keeps scale visible
 *  'fake'  â€” replaces values with completely unrelated plausible numbers; hides real scale
 */
@Injectable({ providedIn: 'root' })
export class DemoModeService {
  private readonly _isDemoMode = signal<boolean>(localStorage.getItem(STORAGE_KEY) === 'true');
  private readonly _demoStyle = signal<DemoStyle>(
    (localStorage.getItem(STYLE_KEY) as DemoStyle) ?? 'blur',
  );

  readonly isDemoMode = this._isDemoMode.asReadonly();
  readonly demoStyle = this._demoStyle.asReadonly();

  /** Consistent multiplicative factor for blur mode (stable within session) */
  private readonly _factor = this.getOrCreateFactor();

  constructor() {
    effect(() => localStorage.setItem(STORAGE_KEY, String(this._isDemoMode())));
    effect(() => localStorage.setItem(STYLE_KEY, this._demoStyle()));
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

  setStyle(style: DemoStyle): void {
    this._demoStyle.set(style);
  }

  maskValue(value: number): number {
    if (!this._isDemoMode()) return value;
    return this._demoStyle() === 'fake'
      ? this.fakeMaskValue(value)
      : Math.round(value * this._factor);
  }

  maskPercent(value: number): number {
    if (!this._isDemoMode()) return value;
    if (this._demoStyle() === 'fake') return this.fakeMaskPercent(value);
    const noise = 0.7 + (this._factor % 0.3);
    return Math.round(value * noise * 10) / 10;
  }

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

  // â”€â”€ Fake numbers â€” completely decoupled from real values â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  private fakeMaskValue(value: number): number {
    // Use Knuth hash on the rounded value for stable, unrelated fake numbers
    const seed = (Math.abs(Math.round(value)) * 2654435761) >>> 0;
    const ranges = [2500, 4800, 7200, 9500, 12000, 15500, 19000, 24000, 31000, 42000, 55000, 78000];
    const pick = ranges[seed % ranges.length];
    // Add small deterministic jitter so nearby values differ
    const jitter = (seed % 500) - 250;
    return Math.round((pick + jitter) / 100) * 100;
  }

  private fakeMaskPercent(value: number): number {
    const seed = (Math.abs(Math.round(value * 100)) * 2246822519) >>> 0;
    const pool = [-12.4, -8.7, -5.2, -2.1, 0.3, 1.8, 3.4, 6.1, 9.2, 14.5, 18.3, 22.7];
    const raw = pool[seed % pool.length];
    // Preserve sign direction of the original value
    return value >= 0 ? Math.abs(raw) : -Math.abs(raw);
  }

  private getOrCreateFactor(): number {
    const key = 'pm_demo_factor';
    const stored = sessionStorage.getItem(key);
    if (stored) {
      const v = parseFloat(stored);
      if (v >= 0.3 && v <= 0.7) return v;
    }
    const factor = 0.3 + Math.random() * 0.4;
    sessionStorage.setItem(key, String(factor));
    return factor;
  }
}
