import { Injectable, effect, inject, signal } from '@angular/core';
import { UserPreferencesStateService } from './user-preferences-state.service';

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

@Injectable({ providedIn: 'root' })
export class ConfigService {
  private readonly userPrefs = inject(UserPreferencesStateService);
  private readonly _config = signal<AppConfig>(this.load());

  readonly config = this._config.asReadonly();

  readonly scanIntervalSeconds = () => this._config().scanIntervalSeconds;
  readonly appRefreshSeconds = () => this._config().appRefreshSeconds;
  readonly rsiOversoldThreshold = () => this._config().rsiOversoldThreshold;
  readonly rsiOverboughtThreshold = () => this._config().rsiOverboughtThreshold;

  constructor() {
    // Persist to localStorage on every change
    effect(() => {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(this._config()));
    });
    // When DB preferences load, overlay DB value (DB wins over localStorage)
    effect(() => {
      const dbValue = this.userPrefs.get<AppConfig>(STORAGE_KEY);
      if (dbValue) {
        this._config.set({ ...DEFAULTS, ...dbValue });
      }
    });
  }

  update(patch: Partial<AppConfig>): void {
    this._config.update((cur) => ({ ...cur, ...patch }));
    this.userPrefs.set(STORAGE_KEY, this._config()); // write-through to DB
  }

  reset(): void {
    this._config.set({ ...DEFAULTS });
    this.userPrefs.remove(STORAGE_KEY);
  }

  private load(): AppConfig {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (raw) {
        const parsed = JSON.parse(raw) as Partial<AppConfig> & { portfolioRefreshSeconds?: number };
        // Migrate from old split intervals to single appRefreshSeconds
        if (
          parsed.portfolioRefreshSeconds !== undefined &&
          parsed.appRefreshSeconds === undefined
        ) {
          parsed.appRefreshSeconds = parsed.portfolioRefreshSeconds;
        }
        return { ...DEFAULTS, ...parsed };
      }
    } catch {
      // Corrupt storage — fall back to defaults
    }
    return { ...DEFAULTS };
  }
}
