import { signalStoreFeature, withState } from '@ngrx/signals';

/** Loading/error state shared by every store that talks to the API. */
export interface RequestStatusState {
  loading: boolean;
  error: string | null;
}

/**
 * Adds `loading` and `error` signals to a store.
 *
 * Mutate them only through the {@link setLoading}, {@link setLoaded} and {@link setError} updaters
 * so every store transitions the same way: `patchState(store, setLoading())`.
 */
export function withRequestStatus(initial: Partial<RequestStatusState> = {}) {
  return signalStoreFeature(
    withState<RequestStatusState>({ loading: false, error: null, ...initial }),
  );
}

/** Starts a request and clears any previous error. */
export const setLoading = (): RequestStatusState => ({ loading: true, error: null });

/** Ends a request successfully. */
export const setLoaded = (): Pick<RequestStatusState, 'loading'> => ({ loading: false });

/** Ends a request with a user-presentable message. */
export const setError = (error: string): RequestStatusState => ({ loading: false, error });
