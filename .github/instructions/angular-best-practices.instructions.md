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

## State Services

State services in `core/services/` expose **readonly** signals:

```typescript
private readonly _data = signal<T[]>([]);
readonly data = this._data.asReadonly();
readonly count = computed(() => this._data().length);
```

Call `demoMode.maskValue(n)` / `maskPercent(n)` before displaying any monetary value in templates.

## Subscription Cleanup

Always use `takeUntilDestroyed()` for subscriptions inside services and components:

```typescript
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Injectable({ providedIn: 'root' })
export class FooStateService {
  constructor() {
    this.api.getAll().pipe(takeUntilDestroyed()).subscribe(...);
  }
}
```

**Never** manually implement `ngOnDestroy` + `Subject` just to clean up subscriptions — `takeUntilDestroyed()` replaces that pattern entirely.

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

- `resource()` / `httpResource()` — project uses the two-service API+State pattern instead
- `async` pipe — prefer signals; only use `async` pipe if converting a cold observable in a template with no state service
- `NgModules` — all components are standalone (default since v20)
- `Zone.js`-based change detection tricks — use `OnPush` + signals only

## Angular Material (used in this project)

`MatTableModule` | `MatSortModule` | `MatPaginatorModule` | `MatFormFieldModule` | `MatInputModule`
`MatSelectModule` | `MatButtonModule` | `MatIconModule` | `MatSnackBarModule` | `MatDialogModule`
`MatCardModule` | `MatToolbarModule` | `MatProgressSpinnerModule` | `MatTooltipModule` | `MatChipsModule`

Use `mat-flat-button` for primary actions, `mat-stroked-button` for secondary.
