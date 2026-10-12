import { inject } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { filter, take } from 'rxjs';
import { AuthStateService } from '../../services/auth-state.service';

/**
 * Runs `callback` once, the first time the user is authenticated.
 *
 * Stores load their initial data through this so nothing fires before the access token exists.
 * Loading earlier races the auth refresh on app start and produces a 401. Call it from a store's
 * `withHooks({ onInit })`, which runs in an injection context.
 */
export function onceAuthenticated(callback: () => void): void {
  const auth = inject(AuthStateService);
  toObservable(auth.isAuthenticated)
    .pipe(
      filter((authenticated) => authenticated),
      take(1),
      takeUntilDestroyed(),
    )
    .subscribe(callback);
}
