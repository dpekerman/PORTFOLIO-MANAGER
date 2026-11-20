import { inject } from '@angular/core';
import { tapResponse } from '@ngrx/operators';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { Observable, pipe, switchMap, tap } from 'rxjs';
import {
  NotificationApiService,
  NotificationRecipients,
} from '../services/notification-api.service';
import { setLoaded, setLoading, withRequestStatus } from './features/request-status.feature';

export const MAX_RECIPIENTS = 50;

interface RecipientsState {
  emails: string[];
  dirty: boolean;
}

/** Email recipients of the RSI alert notifications. */
export const NotificationRecipientsStore = signalStore(
  { providedIn: 'root' },
  withState<RecipientsState>({ emails: [], dirty: false }),
  withRequestStatus({ loading: true }),
  withMethods((store, api = inject(NotificationApiService)) => {
    const apply = (r: NotificationRecipients) =>
      patchState(store, { emails: r.emails ?? [], dirty: false });

    return {
      load: rxMethod<void>(
        pipe(
          tap(() => patchState(store, setLoading())),
          switchMap(() =>
            api.getRecipients().pipe(
              tapResponse({
                next: (r) => {
                  apply(r);
                  patchState(store, setLoaded());
                },
                error: () => patchState(store, setLoaded()),
              }),
            ),
          ),
        ),
      ),
      /** Adds a normalised address; ignores invalid, duplicate or over-limit input. */
      add(value: string): void {
        const email = value.trim().toLowerCase();
        if (!email || !email.includes('@')) return;
        const emails = store.emails();
        if (emails.includes(email) || emails.length >= MAX_RECIPIENTS) return;
        patchState(store, { emails: [...emails, email], dirty: true });
      },
      remove(email: string): void {
        patchState(store, { emails: store.emails().filter((e) => e !== email), dirty: true });
      },
      removeLast(): void {
        if (store.emails().length === 0) return;
        patchState(store, { emails: store.emails().slice(0, -1), dirty: true });
      },
      save(): Observable<NotificationRecipients> {
        return api.updateRecipients(store.emails()).pipe(tap(apply));
      },
    };
  }),
);
