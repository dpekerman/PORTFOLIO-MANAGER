import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject } from '@angular/core';
import {
  patchState,
  signalStore,
  withComputed,
  withMethods,
  withState,
} from '@ngrx/signals';
import { Router } from '@angular/router';
import { firstValueFrom, retry, throwError, timer } from 'rxjs';
import { AuthResponse, UserInfo } from '../models/portfolio.models';
import { AuthApiService } from '../services/auth-api.service';

/** Backoff before retrying a rate-limited (HTTP 429) auth call: 1x, 2x, 3x the base delay, then give up. */
export const retryOnRateLimit = <T>(baseDelayMs = 2000) =>
  retry<T>({
    count: 3,
    delay: (error: unknown, attempt: number) =>
      error instanceof HttpErrorResponse && error.status === 429
        ? timer(attempt * baseDelayMs)
        : throwError(() => error),
  });

interface AuthState {
  currentUser: UserInfo | null;
  /** In-memory only: the session survives reloads through the refresh cookie, not storage. */
  accessToken: string | null;
  /** `null` until the startup check has answered. */
  setupRequired: boolean | null;
  /** Base delay for 429 retries during startup; only tests change it. */
  rateLimitBackoffMs: number;
}

/**
 * The authenticated session: access token, current user and role flags. Every other store gates
 * its loading on `isAuthenticated` (see `onceAuthenticated`), so this must stay free of
 * dependencies on them.
 *
 * `initializeAuth()` runs as an app initializer and never rejects: a failed setup check sends the
 * user to /login, and a failed refresh just leaves them unauthenticated for the guard to handle.
 */
export const AuthStore = signalStore(
  { providedIn: 'root' },
  withState<AuthState>({
    currentUser: null,
    accessToken: null,
    setupRequired: null,
    rateLimitBackoffMs: 2000,
  }),
  withComputed(({ accessToken, currentUser }) => {
    const isAdmin = computed(() => currentUser()?.roles.includes('Admin') ?? false);
    const isTrader = computed(() => currentUser()?.roles.includes('Trader') ?? false);
    return {
      isAuthenticated: computed(() => accessToken() !== null),
      isAdmin,
      isTrader,
      canWrite: computed(() => isAdmin() || isTrader()),
    };
  }),
  withMethods((store, authApi = inject(AuthApiService), router = inject(Router)) => {
    const setAuth = (response: AuthResponse): void => {
      patchState(store, { accessToken: response.accessToken, currentUser: response.user });
    };

    return {
      setAuth,

      clearAuth(): void {
        patchState(store, { accessToken: null, currentUser: null });
      },

      setSetupRequired(setupRequired: boolean | null): void {
        patchState(store, { setupRequired });
      },

      /** Test hook: shortens the 429 retry delay. */
      setRateLimitBackoffMs(rateLimitBackoffMs: number): void {
        patchState(store, { rateLimitBackoffMs });
      },

      async initializeAuth(): Promise<void> {
        try {
          const { required } = await firstValueFrom(
            authApi.checkSetupRequired().pipe(retryOnRateLimit(store.rateLimitBackoffMs())),
          );
          patchState(store, { setupRequired: required });
          if (required) return; // setup guard will redirect
        } catch {
          router.navigate(['/login']);
          return;
        }

        try {
          const response = await firstValueFrom(
            authApi.refreshToken().pipe(retryOnRateLimit(store.rateLimitBackoffMs())),
          );
          setAuth(response);
        } catch {
          // No valid refresh cookie — guard will redirect to /login
        }
      },
    };
  }),
);
