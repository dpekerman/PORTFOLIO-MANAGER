import { BreakpointObserver } from '@angular/cdk/layout';
import { DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { patchState, signalStore, withHooks, withState } from '@ngrx/signals';

/**
 * Viewport class boundaries.
 *
 * Deliberately *our* queries instead of CDK's `Breakpoints.Handset`: the CDK set also matches
 * landscape phones and small tablets, which would flip wide data grids into card mode on devices
 * that have room for a real table. The `.98px` upper bounds avoid the gap that integer bounds
 * leave on fractional-DPI viewports (e.g. 599.5px would match neither 599 nor 600).
 */
export const HANDSET_QUERY = '(max-width: 599.98px)';
export const TABLET_QUERY = '(min-width: 600px) and (max-width: 959.98px)';
export const DESKTOP_QUERY = '(min-width: 960px)';

interface BreakpointState {
  isHandset: boolean;
  isTablet: boolean;
  isDesktop: boolean;
}

/**
 * The current viewport class, as signals.
 *
 * WHY a store rather than injecting {@link BreakpointObserver} everywhere: the observer is an
 * Observable API, so every consumer would need its own subscription, its own teardown and its own
 * `toSignal` conversion. One root store means one media-query listener for the whole app and a
 * synchronous signal read from templates, directives and other stores.
 *
 * Invariants:
 * - exactly one of `isHandset` / `isTablet` / `isDesktop` is true once the observer has emitted;
 * - the initial state assumes desktop so the first paint of a server-less bootstrap never flashes
 *   the mobile layout on a wide screen; `BreakpointObserver` emits synchronously on subscribe, so
 *   that assumption is corrected before the first change-detection pass completes;
 * - the subscription is torn down with the root injector (`takeUntilDestroyed`).
 */
export const BreakpointStore = signalStore(
  { providedIn: 'root' },
  withState<BreakpointState>({ isHandset: false, isTablet: false, isDesktop: true }),
  withHooks({
    onInit(store, observer = inject(BreakpointObserver), destroyRef = inject(DestroyRef)): void {
      observer
        .observe([HANDSET_QUERY, TABLET_QUERY, DESKTOP_QUERY])
        .pipe(takeUntilDestroyed(destroyRef))
        .subscribe(({ breakpoints }) => {
          patchState(store, {
            isHandset: !!breakpoints[HANDSET_QUERY],
            isTablet: !!breakpoints[TABLET_QUERY],
            isDesktop: !!breakpoints[DESKTOP_QUERY],
          });
        });
    },
  }),
);
