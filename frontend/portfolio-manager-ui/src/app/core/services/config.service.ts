import { Injectable, effect, signal } from '@angular/core';

export interface AppConfig {
  scanIntervalSeconds: number;
  portfolioRefreshSeconds: number;
  watchlistRefreshSeconds: number;
}

const STORAGE_KEY = 'pm_app_config';

const DEFAULTS: AppConfig = {
  scanIntervalSeconds: 300, // 5 minutes
  portfolioRefreshSeconds: 120, // 2 minutes
  watchlistRefreshSeconds: 60, // 1 minute
};

@Injectable({ providedIn: 'root' })
export class ConfigService {
  private readonly _config = signal<AppConfig>(this.load());

  readonly config = this._config.asReadonly();

  readonly scanIntervalSeconds = () => this._config().scanIntervalSeconds;
  readonly portfolioRefreshSeconds = () => this._config().portfolioRefreshSeconds;
  readonly watchlistRefreshSeconds = () => this._config().watchlistRefreshSeconds;

  constructor() {
    // Persist to localStorage on every change
    effect(() => {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(this._config()));
    });
  }

  update(patch: Partial<AppConfig>): void {
    this._config.update((cur) => ({ ...cur, ...patch }));
  }

  reset(): void {
    this._config.set({ ...DEFAULTS });
  }

  private load(): AppConfig {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (raw) {
        const parsed = JSON.parse(raw) as Partial<AppConfig>;
        return { ...DEFAULTS, ...parsed };
      }
    } catch {
      // Corrupt storage — fall back to defaults
    }
    return { ...DEFAULTS };
  }
}
