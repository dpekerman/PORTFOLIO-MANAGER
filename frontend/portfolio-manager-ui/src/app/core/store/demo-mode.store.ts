import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { withStorageSync } from './features/storage-sync.feature';

const STORAGE_KEY = 'pm_demo_mode';
const STYLE_KEY = 'pm_demo_style';
const FACTOR_KEY = 'pm_demo_factor';

export type DemoStyle = 'blur' | 'fake';

interface DemoModeState {
  isDemoMode: boolean;
  demoStyle: DemoStyle;
}

/** Consistent multiplicative factor for blur mode (stable within the browser session). */
function getOrCreateFactor(): number {
  const stored = sessionStorage.getItem(FACTOR_KEY);
  if (stored) {
    const v = parseFloat(stored);
    if (v >= 0.3 && v <= 0.7) return v;
  }
  const factor = 0.3 + Math.random() * 0.4;
  sessionStorage.setItem(FACTOR_KEY, String(factor));
  return factor;
}

function fakeMaskValue(value: number): number {
  // Use Knuth hash on the rounded value for stable, unrelated fake numbers
  const seed = (Math.abs(Math.round(value)) * 2654435761) >>> 0;
  const ranges = [2500, 4800, 7200, 9500, 12000, 15500, 19000, 24000, 31000, 42000, 55000, 78000];
  const pick = ranges[seed % ranges.length];
  // Add small deterministic jitter so nearby values differ
  const jitter = (seed % 500) - 250;
  return Math.round((pick + jitter) / 100) * 100;
}

function fakeMaskPercent(value: number): number {
  const seed = (Math.abs(Math.round(value * 100)) * 2246822519) >>> 0;
  const pool = [-12.4, -8.7, -5.2, -2.1, 0.3, 1.8, 3.4, 6.1, 9.2, 14.5, 18.3, 22.7];
  const raw = pool[seed % pool.length];
  // Preserve sign direction of the original value
  return value >= 0 ? Math.abs(raw) : -Math.abs(raw);
}

/**
 * Demo mode for screenshots and sharing, with two masking strategies:
 *  - `blur`: multiplies real values by a session factor (0.3-0.7); keeps scale visible
 *  - `fake`: replaces values with unrelated plausible numbers; hides real scale
 *
 * Every monetary or percentage display must go through `maskValue` / `maskPercent` /
 * `displayValue`, never a raw signal value. The mode and style persist in localStorage.
 */
export const DemoModeStore = signalStore(
  { providedIn: 'root' },
  withState<DemoModeState>(() => ({
    isDemoMode: localStorage.getItem(STORAGE_KEY) === 'true',
    demoStyle: (localStorage.getItem(STYLE_KEY) as DemoStyle) ?? 'blur',
  })),
  withMethods((store) => {
    const factor = getOrCreateFactor();

    const maskValue = (value: number): number => {
      if (!store.isDemoMode()) return value;
      return store.demoStyle() === 'fake' ? fakeMaskValue(value) : Math.round(value * factor);
    };

    return {
      maskValue,

      maskPercent(value: number): number {
        if (!store.isDemoMode()) return value;
        if (store.demoStyle() === 'fake') return fakeMaskPercent(value);
        const noise = 0.7 + (factor % 0.3);
        return Math.round(value * noise * 10) / 10;
      },

      displayValue(value: number, decimals = 0): string {
        if (!store.isDemoMode())
          return value.toLocaleString('en-US', {
            minimumFractionDigits: decimals,
            maximumFractionDigits: decimals,
          });
        return maskValue(value).toLocaleString('en-US', {
          minimumFractionDigits: decimals,
          maximumFractionDigits: decimals,
        });
      },

      toggle(): void {
        patchState(store, ({ isDemoMode }) => ({ isDemoMode: !isDemoMode }));
      },

      enable(): void {
        patchState(store, { isDemoMode: true });
      },

      disable(): void {
        patchState(store, { isDemoMode: false });
      },

      setStyle(demoStyle: DemoStyle): void {
        patchState(store, { demoStyle });
      },
    };
  }),
  withStorageSync<DemoModeState>(({ isDemoMode, demoStyle }) => {
    localStorage.setItem(STORAGE_KEY, String(isDemoMode));
    localStorage.setItem(STYLE_KEY, demoStyle);
  }),
);
