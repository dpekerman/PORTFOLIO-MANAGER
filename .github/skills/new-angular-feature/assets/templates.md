# Feature File Templates

## API Service (`core/services/foo-bar-api.service.ts`)

HTTP only. No signals, no state.

```typescript
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { FooBarItem } from '../models/portfolio.models';

@Injectable({ providedIn: 'root' })
export class FooBarApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/foo-bar';

  getAll(): Observable<FooBarItem[]> {
    return this.http.get<FooBarItem[]>(this.base);
  }
}
```

## Store (`core/store/foo-bar.store.ts`)

```typescript
import { computed, inject } from '@angular/core';
import { tapResponse } from '@ngrx/operators';
import { patchState, signalStore, withComputed, withHooks, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap, tap } from 'rxjs';
import { FooBarItem } from '../models/portfolio.models';
import { FooBarApiService } from '../services/foo-bar-api.service';
import { onceAuthenticated } from './features/auth-gate';
import { setError, setLoaded, setLoading, withRequestStatus } from './features/request-status.feature';

/** Why this store exists / any non-obvious invariant goes here (not a restatement of the name). */
export const FooBarStore = signalStore(
  { providedIn: 'root' },
  withState({ items: [] as FooBarItem[] }),
  withRequestStatus(),
  withComputed(({ items }) => ({ count: computed(() => items().length) })),
  withMethods((store, api = inject(FooBarApiService)) => ({
    load: rxMethod<void>(
      pipe(
        tap(() => patchState(store, setLoading())),
        switchMap(() =>
          api.getAll().pipe(
            tapResponse({
              next: (items) => patchState(store, { items }, setLoaded()),
              error: () => patchState(store, setError('Failed to load')),
            }),
          ),
        ),
      ),
    ),
  })),
  withHooks({
    onInit(store) {
      // Wait for auth: loading earlier races the token refresh and 401s.
      onceAuthenticated(() => store.load());
    },
  }),
);
```

Test a store with `unprotected(store)` from `@ngrx/signals/testing` when a test must patch state.

## Page Component (`features/foo-bar/foo-bar-page.component.ts`)

```typescript
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { FooBarStore } from '../../core/store/foo-bar.store';

@Component({
  selector: 'app-foo-bar-page',
  templateUrl: './foo-bar-page.component.html',
  styleUrl: './foo-bar-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatProgressSpinnerModule],
})
export class FooBarPageComponent {
  protected readonly store = inject(FooBarStore);
}
```

## Page Template (`.component.html`)

```html
@if (store.loading()) {
  <mat-spinner />
} @else if (store.error()) {
  <p class="error">{{ store.error() }}</p>
} @else {
  @for (item of store.items(); track item.id) {
    <div>{{ item.name }}</div>
  }
}
```

## Routes File (`foo-bar.routes.ts`)

```typescript
import { Routes } from '@angular/router';

export const FOO_BAR_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./foo-bar-page.component').then((m) => m.FooBarPageComponent),
  },
];
```
