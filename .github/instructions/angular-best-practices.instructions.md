---
applyTo: "**/*.ts,**/*.html,**/*.scss,**/*.css,angular.json,tsconfig*.json"
description: "Portfolio Manager Angular project-specific overrides. Covers Material module usage, state service pattern, and project deviations from standard Angular."
---

# Portfolio Manager -- Angular Project Delta

> Standard Angular rules (signals, OnPush, inject, input/output, @if/@for) are in the user-level Angular instructions.
> This file covers **project-specific patterns only**.

## Component Member Order

```typescript
@Component({
  selector: "app-x",
  templateUrl: "./x.component.html",
  styleUrl: "./x.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class XComponent {
  // 1. inject()
  // 2. input() / output()
  // 3. protected signals / computed (template-visible)
  // 4. private signals
  // 5. Lifecycle hooks
  // 6. protected methods (template handlers)
  // 7. private methods
}
```

## State Stores

State lives in NgRx signal stores in `core/store/` (see `watchlist.store.ts`). Stores expose readonly signals; only the store patches its state:

```typescript
export const FooStore = signalStore(
  { providedIn: 'root' },
  withState({ items: [] as Foo[] }),
  withRequestStatus(),
  withComputed(({ items }) => ({ count: computed(() => items().length) })),
  withMethods((store, api = inject(FooApiService)) => ({ /* rxMethod loads, mutations */ })),
  withHooks({ onInit: (store) => onceAuthenticated(() => store.load()) }),
);
```

Call `demoMode.maskValue(n)` / `maskPercent(n)` before displaying any monetary value in templates. Test stores with `unprotected(store)` (`@ngrx/signals/testing`) to patch state.

## Subscription Cleanup

Inside components use `takeUntilDestroyed()`; inside stores prefer `rxMethod` (it cleans up with the store). **Never** implement `ngOnDestroy` + `Subject` only to clean up subscriptions.

## Template: `@let` Variables

Use `@let` (Angular 18+) to bind a local template variable instead of repeating a signal call:

```html
@let user = currentUser(); @if (user) {
<span>{{ user.name }}</span>
}
```

## Template: `@defer` Blocks

Defer heavy or below-the-fold sections to improve initial load:

```html
@defer (on viewport) {
<app-heavy-chart />
} @placeholder {
<div class="chart-skeleton"></div>
}
```

## Writable Derived Signals

Use `linkedSignal()` when you need a writable signal whose default tracks another signal:

```typescript
protected readonly selected = linkedSignal(() => this.state.items()[0] ?? null);
```

## Things NOT Used in This Project

- `resource()` / `httpResource()` — the project uses api-service + signal store instead
- `async` pipe — prefer signals; only use `async` pipe if converting a cold observable in a template with no store
- `NgModules` — all components are standalone (default since v20)
- `Zone.js` — the app is zoneless; use `OnPush` + signals only

## Angular Material (used in this project)

`MatTableModule` | `MatSortModule` | `MatPaginatorModule` | `MatFormFieldModule` | `MatInputModule`
`MatSelectModule` | `MatButtonModule` | `MatIconModule` | `MatSnackBarModule` | `MatDialogModule`
`MatCardModule` | `MatToolbarModule` | `MatProgressSpinnerModule` | `MatTooltipModule` | `MatChipsModule`

Use `mat-flat-button` for primary actions, `mat-stroked-button` for secondary.
