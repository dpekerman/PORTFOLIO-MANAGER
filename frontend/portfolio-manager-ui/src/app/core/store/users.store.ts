import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { tapResponse } from '@ngrx/operators';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap, tap } from 'rxjs';
import { AppRole, CreateUserRequest, UserInfo } from '../models/portfolio.models';
import { UsersApiService } from '../services/users-api.service';
import { setLoaded, setLoading, withRequestStatus } from './features/request-status.feature';

interface UsersState {
  users: UserInfo[];
  saving: boolean;
}

/** Application users (Admin only). Mutations update the list only after the server confirms. */
export const UsersStore = signalStore(
  { providedIn: 'root' },
  withState<UsersState>({ users: [], saving: false }),
  withRequestStatus(),
  withMethods((store, api = inject(UsersApiService), snackBar = inject(MatSnackBar)) => {
    const notify = (message: string, duration = 3000, action = 'OK') =>
      snackBar.open(message, action, { duration });

    const load = rxMethod<void>(
      pipe(
        tap(() => patchState(store, setLoading())),
        switchMap(() =>
          api.getAll().pipe(
            tapResponse({
              next: (users) => patchState(store, { users }, setLoaded()),
              error: () => patchState(store, setLoaded()),
            }),
          ),
        ),
      ),
    );

    return {
      load,
      create(req: CreateUserRequest): void {
        patchState(store, { saving: true });
        api.create(req).subscribe({
          next: (user) => {
            patchState(store, { users: [...store.users(), user], saving: false });
            notify(`User ${user.displayName} created.`);
          },
          error: (err) => {
            patchState(store, { saving: false });
            notify(
              err?.error?.errors?.[0] ?? err?.error?.message ?? 'Failed to create user.',
              5000,
              'Dismiss',
            );
          },
        });
      },
      changeRole(userId: string, role: AppRole): void {
        api.assignRole(userId, role).subscribe({
          next: () => {
            patchState(store, {
              users: store.users().map((u) => (u.id === userId ? { ...u, roles: [role] } : u)),
            });
            notify('Role updated.', 2000);
          },
          error: (err) => {
            notify(err?.error?.message ?? 'Failed to change role.', 4000, 'Dismiss');
            load(); // re-sync with the server
          },
        });
      },
      remove(userId: string): void {
        api.delete(userId).subscribe({
          next: () => {
            patchState(store, { users: store.users().filter((u) => u.id !== userId) });
            notify('User deleted.');
          },
          error: (err) => notify(err?.error?.message ?? 'Failed to delete user.', 4000, 'Dismiss'),
        });
      },
    };
  }),
);
