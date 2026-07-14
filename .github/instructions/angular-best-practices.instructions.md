---
applyTo: "**/*.ts,**/*.html,**/*.scss,**/*.css,angular.json,tsconfig*.json"
description: "Portfolio Manager Angular project-specific overrides. Covers Material module usage, state service pattern, and project deviations from standard Angular."
---

# Portfolio Manager -- Angular Project Delta

> Standard Angular rules (signals, OnPush, inject, input/output, @if/@for) are in the user-level Angular instructions.
> This file covers **project-specific patterns only**.

## Component Member Order

```typescript
@Component({ selector: 'app-x', templateUrl: './x.component.html', styleUrl: './x.component.scss', changeDetection: ChangeDetectionStrategy.OnPush })
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

## Angular Material (used in this project)

`MatTableModule` | `MatSortModule` | `MatPaginatorModule` | `MatFormFieldModule` | `MatInputModule`
`MatSelectModule` | `MatButtonModule` | `MatIconModule` | `MatSnackBarModule` | `MatDialogModule`
`MatCardModule` | `MatToolbarModule` | `MatProgressSpinnerModule` | `MatTooltipModule` | `MatChipsModule`

Use `mat-flat-button` for primary actions, `mat-stroked-button` for secondary.