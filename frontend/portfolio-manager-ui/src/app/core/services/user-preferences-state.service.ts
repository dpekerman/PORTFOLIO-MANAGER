import { Injectable, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { EMPTY, catchError, distinctUntilChanged, filter, switchMap } from 'rxjs';
import { AuthStateService } from './auth-state.service';
import { UserPreferencesApiService } from './user-preferences-api.service';

@Injectable({ providedIn: 'root' })
export class UserPreferencesStateService {
  private readonly api = inject(UserPreferencesApiService);
  private readonly authState = inject(AuthStateService);

  /** All DB-persisted preferences for the current user, keyed by preference key. */
  private readonly _prefs = signal<Record<string, string>>({});
  /** True once the initial DB load has completed (success or failure). */
  readonly isLoaded = signal(false);

  readonly prefs = this._prefs.asReadonly();

  /** Returns the parsed JSON value for a key, or null if not set in DB. */
  get<T>(key: string): T | null {
    const raw = this._prefs()[key];
    if (raw === undefined) return null;
    try {
      return JSON.parse(raw) as T;
    } catch {
      return null;
    }
  }

  constructor() {
    // Reload all preferences whenever the user logs in (token transitions to non-null)
    toObservable(this.authState.accessToken)
      .pipe(
        takeUntilDestroyed(),
        distinctUntilChanged(),
        switchMap((token) => (token ? this.api.getAll().pipe(catchError(() => EMPTY)) : EMPTY)),
      )
      .subscribe((prefs) => {
        this._prefs.set(prefs);
        this.isLoaded.set(true);
      });

    // Clear prefs on logout
    toObservable(this.authState.isAuthenticated)
      .pipe(
        takeUntilDestroyed(),
        distinctUntilChanged(),
        filter((auth) => !auth),
      )
      .subscribe(() => {
        this._prefs.set({});
        this.isLoaded.set(false);
      });
  }

  /** Write a value to DB and update the local signal. Value is JSON-serialized. */
  set(key: string, value: unknown): void {
    const json = JSON.stringify(value);
    this._prefs.update((p) => ({ ...p, [key]: json }));
    this.api.upsert(key, json).subscribe({ error: () => {} }); // localStorage is the fallback
  }

  /** Remove a key from DB and local signal (reverts to default). */
  remove(key: string): void {
    this._prefs.update((p) => {
      const next = { ...p };
      delete next[key];
      return next;
    });
    this.api.delete(key).subscribe({ error: () => {} });
  }

  /** Force-reload preferences from DB (call after login if needed). */
  reload(): void {
    this.isLoaded.set(false);
    this.api
      .getAll()
      .pipe(catchError(() => EMPTY))
      .subscribe((prefs) => {
        this._prefs.set(prefs);
        this.isLoaded.set(true);
      });
  }
}
