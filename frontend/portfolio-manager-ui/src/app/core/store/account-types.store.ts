import { HttpErrorResponse } from '@angular/common/http';
import { DestroyRef, computed, effect, inject, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { tapResponse } from '@ngrx/operators';
import {
  patchState,
  signalStore,
  withComputed,
  withHooks,
  withMethods,
  withState,
} from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { Observable, firstValueFrom, pipe, switchMap } from 'rxjs';
import { AccountType } from '../models/portfolio.models';
import { AccountTypesApiService } from '../services/account-types-api.service';
import { AuthStore } from '../store/auth.store';
import { setError, setLoaded, setLoading, withRequestStatus } from './features/request-status.feature';

export function accountTypeError(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const body: unknown = error.error;
    if (typeof body === 'string') return body;
    if (body && typeof body === 'object' && 'detail' in body && typeof body.detail === 'string')
      return body.detail;
  }
  return 'Could not save/load account types. Refresh the list and try again.';
}

interface AccountTypesState {
  items: AccountType[];
  loaded: boolean;
  saving: boolean;
  /** Set when a rename is committed so dependent stores can re-key their data. */
  renamed: { oldName: string; newName: string } | null;
}

/**
 * The user's account-type catalog (the picklist behind every account selector).
 *
 * Every authentication change bumps a generation counter and resets the state, so a response or
 * mutation that resolves after a logout or user switch is dropped instead of leaking another
 * user's data. Writes apply only the server-committed result, and only one write runs at a time.
 */
export const AccountTypesStore = signalStore(
  { providedIn: 'root' },
  withState<AccountTypesState>({ items: [], loaded: false, saving: false, renamed: null }),
  withRequestStatus(),
  withComputed(({ items, loaded, loading, error }) => ({
    names: computed(() => items().map((x) => x.name)),
    ready: computed(() => loaded() && !loading() && !error()),
  })),
  withMethods(
    (
      store,
      api = inject(AccountTypesApiService),
      auth = inject(AuthStore),
      destroyRef = inject(DestroyRef),
    ) => {
      let generation = 0;

      const fetch = rxMethod<number>(
        pipe(
          switchMap((requestGeneration) =>
            api.getAll().pipe(
              tapResponse({
                next: (items) => {
                  if (requestGeneration !== generation) return;
                  patchState(store, { items, loaded: true }, setLoaded());
                },
                error: (error) => {
                  if (requestGeneration !== generation) return;
                  patchState(store, setError(accountTypeError(error)));
                },
              }),
            ),
          ),
        ),
      );

      const load = (): void => {
        if (!auth.isAuthenticated() || store.loading() || store.saving()) return;
        patchState(store, setLoading());
        fetch(++generation);
      };

      const mutate = async <T>(
        request: Observable<T>,
        apply: (result: T) => void,
      ): Promise<void> => {
        if (store.saving()) throw new Error('An account type operation is already pending.');
        const mutationGeneration = generation;
        patchState(store, { saving: true, error: null });
        try {
          const result = await firstValueFrom(request.pipe(takeUntilDestroyed(destroyRef)));
          if (mutationGeneration !== generation) throw new Error('The authenticated session changed.');
          apply(result);
        } catch (error: unknown) {
          if (mutationGeneration === generation) patchState(store, { error: accountTypeError(error) });
          throw error;
        } finally {
          if (mutationGeneration === generation) patchState(store, { saving: false });
        }
      };

      return {
        load,

        /** Discards in-flight work and clears all state; called on every auth change. */
        resetForAuthChange(): void {
          generation++;
          patchState(store, {
            items: [],
            loaded: false,
            error: null,
            renamed: null,
            loading: false,
            saving: false,
          });
        },

        async add(name: string): Promise<void> {
          await mutate(api.add(name), (item) => {
            patchState(store, ({ items }) => ({ items: [...items, item] }));
          });
        },

        async rename(item: AccountType, name: string): Promise<void> {
          await mutate(api.rename(item, name), (result) => {
            patchState(store, ({ items }) => ({
              items: items.map((x) => (x.id === item.id ? result.item : x)),
              renamed: { oldName: result.oldName, newName: result.item.name },
            }));
          });
        },

        async delete(item: AccountType): Promise<void> {
          await mutate(api.delete(item), () => {
            patchState(store, ({ items }) => ({ items: items.filter((x) => x.id !== item.id) }));
          });
        },
      };
    },
  ),
  withHooks({
    onInit(store) {
      const auth = inject(AuthStore);
      effect(() => {
        const authenticated = auth.isAuthenticated();
        const userId = auth.currentUser()?.id;
        store.resetForAuthChange();
        if (authenticated && userId) untracked(() => store.load());
      });
    },
  }),
);
