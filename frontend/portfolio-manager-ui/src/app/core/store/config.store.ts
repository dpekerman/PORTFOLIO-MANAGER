import { computed, effect, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withHooks, withMethods, withState } from '@ngrx/signals';
import { UserPreferencesStore } from './user-preferences.store';
import { withStorageSync } from './features/storage-sync.feature';

export interface AppConfig {
  scanIntervalSeconds: number;
  appRefreshSeconds: number;
  rsiOversoldThreshold: number;
  rsiOverboughtThreshold: number;
  /** EOD confirmation window start time in HH:mm (Eastern Time). Default: 15:30 */
  eodWindowStart: string;
  /** EOD confirmation window end time in HH:mm (Eastern Time). Default: 16:00 */
  eodWindowEnd: string;
  /** Whether the EOD window is enabled. */
  eodWindowEnabled: boolean;
  /** User-manageable picklist for the Decision Source field. */
  decisionSources: string[];
  /** Idle session timeout in minutes. 0 = disabled. */
  sessionTimeoutMinutes: number;
}

const STORAGE_KEY = 'pm_app_config';

const DEFAULTS: AppConfig = {
  scanIntervalSeconds: 0, // 0 = disabled by default; user enables manually
  appRefreshSeconds: 120, // 2 minutes
  rsiOversoldThreshold: 30,
  rsiOverboughtThreshold: 75,
  eodWindowStart: '15:30',
  eodWindowEnd: '16:00',
  eodWindowEnabled: true,
  decisionSources: [
    'App Signal',
    'App Signal - Add',
    'App Signal - RSI Oversold',
    'App Signal - Trim',
    'Bought Deal',
    'Catalyst',
    'Legacy',
    'Loss Harvest',
    'Manual',
    'Manual - Buy on pullback',
    'Rebalance',
    'Risk Control',
    'Risk Control - all out',
    'Risk Control - Trim',
  ],
  sessionTimeoutMinutes: 480, // 8 hours
};

function load(): AppConfig {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) {
      const parsed = JSON.parse(raw) as Partial<AppConfig> & { portfolioRefreshSeconds?: number };
      // Migrate from old split intervals to single appRefreshSeconds
      if (parsed.portfolioRefreshSeconds !== undefined && parsed.appRefreshSeconds === undefined) {
        parsed.appRefreshSeconds = parsed.portfolioRefreshSeconds;
      }
      return { ...DEFAULTS, ...parsed };
    }
  } catch {
    // Corrupt storage — fall back to defaults
  }
  return { ...DEFAULTS };
}

interface ConfigState {
  config: AppConfig;
}

/**
 * Runtime app configuration (refresh intervals, RSI thresholds, EOD window, picklists).
 * Persists to localStorage on every change and writes through to the user's DB preferences; when
 * the DB preferences load, the DB value overlays the local one (DB wins over localStorage).
 */
export const ConfigStore = signalStore(
  { providedIn: 'root' },
  withState<ConfigState>(() => ({ config: load() })),
  withComputed(({ config }) => ({
    scanIntervalSeconds: computed(() => config().scanIntervalSeconds),
    appRefreshSeconds: computed(() => config().appRefreshSeconds),
    rsiOversoldThreshold: computed(() => config().rsiOversoldThreshold),
    rsiOverboughtThreshold: computed(() => config().rsiOverboughtThreshold),
  })),
  withMethods((store, userPrefs = inject(UserPreferencesStore)) => ({
    update(patch: Partial<AppConfig>): void {
      patchState(store, ({ config }) => ({ config: { ...config, ...patch } }));
      userPrefs.set(STORAGE_KEY, store.config()); // write-through to DB
    },

    reset(): void {
      patchState(store, { config: { ...DEFAULTS } });
      userPrefs.remove(STORAGE_KEY);
    },
  })),
  withStorageSync<ConfigState>(({ config }) => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(config));
  }),
  withHooks({
    onInit(store) {
      const userPrefs = inject(UserPreferencesStore);
      effect(() => {
        const dbValue = userPrefs.get<AppConfig>(STORAGE_KEY);
        if (dbValue) {
          patchState(store, { config: { ...DEFAULTS, ...dbValue } });
        }
      });
    },
  }),
);
