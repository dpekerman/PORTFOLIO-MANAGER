import { inject } from '@angular/core';
import { tapResponse } from '@ngrx/operators';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { EMPTY, distinctUntilChanged, filter, pipe, switchMap, tap } from 'rxjs';
import { AuthStateService } from '../services/auth-state.service';
import { UserPreferencesApiService } from '../services/user-preferences-api.service';

interface UserPreferencesState {
  /** All DB-persisted preferences for the current user, keyed by preference key (JSON strings). */
  prefs: Record<string, string>;
  /** True once the initial DB load has completed successfully; reset on logout and reload. */
  isLoaded: boolean;
}

/**
 * The user's DB-persisted preferences. Reloads on login and clears on logout.
 *
 * Writes update local state immediately and fire-and-forget to the DB: localStorage (owned by the
 * consuming stores) is the fallback, so a failed upsert/delete or a failed initial load is
 * deliberately silent and leaves `isLoaded` false.
 */
export const UserPreferencesStore = signalStore(
  { providedIn: 'root' },
  withState<UserPreferencesState>({ prefs: {}, isLoaded: false }),
  withMethods((store, api = inject(UserPreferencesApiService), auth = inject(AuthStateService)) => {
    const fetchAll = () =>
      api.getAll().pipe(
        tapResponse({
          next: (prefs) => patchState(store, { prefs, isLoaded: true }),
          error: () => undefined, // localStorage is the fallback
        }),
      );

    // Reload whenever the access token transitions to non-null (login / token refresh).
    rxMethod<string | null>(
      pipe(
        distinctUntilChanged(),
        switchMap((token) => (token ? fetchAll() : EMPTY)),
      ),
    )(auth.accessToken);

    // Clear prefs on logout.
    rxMethod<boolean>(
      pipe(
        distinctUntilChanged(),
        filter((authenticated) => !authenticated),
        tap(() => patchState(store, { prefs: {}, isLoaded: false })),
      ),
    )(auth.isAuthenticated);

    return {
      /** Returns the parsed JSON value for a key, or null if not set in DB. */
      get<T>(key: string): T | null {
        const raw = store.prefs()[key];
        if (raw === undefined) return null;
        try {
          return JSON.parse(raw) as T;
        } catch {
          return null;
        }
      },

      /** Write a value to DB and update local state. Value is JSON-serialized. */
      set(key: string, value: unknown): void {
        const json = JSON.stringify(value);
        patchState(store, ({ prefs }) => ({ prefs: { ...prefs, [key]: json } }));
        api.upsert(key, json).subscribe({ error: () => {} }); // localStorage is the fallback
      },

      /** Remove a key from DB and local state (reverts to default). */
      remove(key: string): void {
        patchState(store, ({ prefs }) => {
          const next = { ...prefs };
          delete next[key];
          return { prefs: next };
        });
        api.delete(key).subscribe({ error: () => {} });
      },

      /** Force-reload preferences from DB (call after login if needed). */
      reload: rxMethod<void>(
        pipe(
          tap(() => patchState(store, { isLoaded: false })),
          switchMap(() => fetchAll()),
        ),
      ),
    };
  }),
);
