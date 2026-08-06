# Feature File Templates

## API Service (`foo-bar-api.service.ts`)

```typescript
import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { FooBarItem } from "../models/portfolio.models";

@Injectable({ providedIn: "root" })
export class FooBarApiService {
  private readonly http = inject(HttpClient);
  private readonly base = "/api/foo-bar";

  getAll(): Observable<FooBarItem[]> {
    return this.http.get<FooBarItem[]>(this.base);
  }
}
```

## State Service (`foo-bar-state.service.ts`)

```typescript
import { Injectable, computed, inject, signal } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { MatSnackBar } from "@angular/material/snack-bar";
import { FooBarItem } from "../models/portfolio.models";
import { DemoModeService } from "./demo-mode.service";
import { FooBarApiService } from "./foo-bar-api.service";

@Injectable({ providedIn: "root" })
export class FooBarStateService {
  private readonly api = inject(FooBarApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly demoMode = inject(DemoModeService);

  private readonly _items = signal<FooBarItem[]>([]);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);

  readonly items = this._items.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly count = computed(() => this._items().length);

  load(): void {
    this._loading.set(true);
    this.api
      .getAll()
      .pipe(takeUntilDestroyed())
      .subscribe({
        next: (data) => {
          this._items.set(data);
          this._loading.set(false);
        },
        error: () => {
          this._error.set("Failed to load");
          this._loading.set(false);
        },
      });
  }
}
```

## Page Component (`.component.ts`)

```typescript
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
} from "@angular/core";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { FooBarStateService } from "../../core/services/foo-bar-state.service";

@Component({
  selector: "app-foo-bar-page",
  templateUrl: "./foo-bar-page.component.html",
  styleUrl: "./foo-bar-page.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatProgressSpinnerModule],
})
export class FooBarPageComponent implements OnInit {
  protected readonly state = inject(FooBarStateService);

  ngOnInit(): void {
    this.state.load();
  }
}
```

## Page Template (`.component.html`)

```html
@if (state.loading()) {
<mat-spinner />
} @else if (state.error()) {
<p class="error">{{ state.error() }}</p>
} @else { @let items = state.items(); @for (item of items; track item.id) {
<div>{{ item.name }}</div>
} }
```

## Routes File (`foo-bar.routes.ts`)

```typescript
import { Routes } from "@angular/router";

export const FOO_BAR_ROUTES: Routes = [
  {
    path: "",
    loadComponent: () =>
      import("./foo-bar-page.component").then((m) => m.FooBarPageComponent),
  },
];
```
