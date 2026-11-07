import { signalStoreFeature, type, watchState, withHooks } from '@ngrx/signals';

/**
 * Mirrors a store's state to an external sink (typically `localStorage`).
 *
 * `write` runs once with the initial state and again after every state change. Read the persisted
 * value when declaring the store's initial state (`withState(() => readFromStorage())`) so the
 * first render already shows the persisted value.
 */
export function withStorageSync<State extends object>(write: (state: State) => void) {
  return signalStoreFeature(
    { state: type<State>() },
    withHooks({
      onInit(store) {
        watchState(store, (state) => write(state as State));
      },
    }),
  );
}
