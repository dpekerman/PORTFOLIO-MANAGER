---
applyTo: "**/*.ts,**/*.html,**/*.scss,**/*.css,angular.json,tsconfig*.json"
---

# Angular Best Practices — Full Reference

> Based on the [official Angular AI/LLM guide](https://angular.dev/ai/develop-with-ai) and [Angular Style Guide](https://angular.dev/style-guide). Angular version: **v22+**.

---

## TypeScript

- Strict type checking (`"strict": true` in `tsconfig.json`)
- Prefer type inference; avoid `any` — use `unknown` when type is uncertain
- Use `readonly` for properties that should not be reassigned
- Use `protected` for class members only used in templates

---

## Component Structure

```typescript
@Component({
  selector: 'app-feature',
  templateUrl: './feature.component.html',
  styleUrl: './feature.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FeatureComponent implements OnInit {
  // 1. inject() dependencies
  private readonly featureService = inject(FeatureService);

  // 2. Inputs / Outputs / Queries
  readonly itemId = input.required<string>();
  readonly itemSaved = output<void>();

  // 3. Signals / computed state
  protected readonly item = signal<Item | null>(null);
  protected readonly displayName = computed(() => this.item()?.name ?? 'Unknown');

  // 4. Lifecycle hooks
  ngOnInit(): void { this.loadItem(); }

  // 5. Protected methods (template handlers)
  protected saveItem(): void { ... }

  // 6. Private methods
  private loadItem(): void { ... }
}
```

### Rules

- **Always `OnPush`** change detection
- **`templateUrl` + `styleUrl`** — never inline `template:` or `styles:`
- **`inject()`** — never constructor injection
- **`input()` / `output()`** — never `@Input()` / `@Output()` decorators
- **`protected`** for all template-accessed members
- **Do NOT set `standalone: true`** — default since Angular v20+
- **No `@HostBinding` / `@HostListener`** — use `host: {}` object instead

---

## Signals

```typescript
// Writable signal
protected readonly count = signal(0);

// Computed (memoized, lazy)
protected readonly doubled = computed(() => this.count() * 2);

// Update patterns
this.count.set(5);
this.count.update(n => n + 1);

// Expose readonly from service
private readonly _items = signal<Item[]>([]);
readonly items = this._items.asReadonly();

// Resource for async (Angular v19+)
protected readonly user = resource({
  request: () => this.userId(),
  loader: ({ request }) => fetch(`/api/users/${request}`).then(r => r.json()),
});
```

---

## Templates

- **Native control flow**: `@if`, `@for`, `@switch` — NOT `*ngIf`, `*ngFor`
- **`[class.name]`** — NOT `ngClass`
- **`[style.prop]`** — NOT `ngStyle`
- `track` in `@for` — always track by stable identifier (`track item.id`)
- No complex expressions — move logic to `computed()` in the class

```html
@if (user()) {
<div [class.active]="isActive()">{{ user()!.name }}</div>
} @for (item of items(); track item.id) {
<app-item [item]="item" />
}
```

---

## Services

```typescript
@Injectable({ providedIn: "root" })
export class FeatureService {
  private readonly http = inject(HttpClient);

  getItems(): Observable<Item[]> {
    return this.http.get<Item[]>("/api/items");
  }
}
```

- Single responsibility per service
- `inject()` only — no constructor injection
- Prefer signals over `BehaviorSubject` for state

---

## Routing

```typescript
// Always lazy-load
export const routes: Routes = [
  {
    path: "feature",
    loadComponent: () =>
      import("./features/feature/feature.component").then(
        (m) => m.FeatureComponent,
      ),
  },
  {
    path: "admin",
    loadChildren: () =>
      import("./features/admin/admin.routes").then((m) => m.ADMIN_ROUTES),
  },
];
```

---

## Forms

- Reactive Forms with `FormBuilder` via `inject()`
- Typed forms (`FormControl<string>`, `FormGroup<{...}>`)
- Never Template-driven forms for complex cases

---

## Angular Material

- Import individual Material modules in component `imports: []`
- `MatFormFieldModule` + `MatInputModule` for all inputs
- `MatTableModule` for data tables
- `MatToolbarModule` for navigation/headers
- `MatCardModule` for card layouts
- `MatSnackBarModule` for notifications
- `MatDialogModule` for modals
- `MatIconModule` for icons
- `mat-flat-button` / `mat-raised-button` for primary actions
- `mat-stroked-button` / `mat-button` for secondary actions
- Configure theme once in `styles.scss` using `@use '@angular/material' as mat`

---

## File Naming & Project Structure

| Artifact  | File                        | Class                  | Selector           |
| --------- | --------------------------- | ---------------------- | ------------------ |
| Component | `user-profile.component.ts` | `UserProfileComponent` | `app-user-profile` |
| Service   | `user.service.ts`           | `UserService`          | —                  |
| Directive | `highlight.directive.ts`    | `HighlightDirective`   | `[appHighlight]`   |
| Pipe      | `truncate.pipe.ts`          | `TruncatePipe`         | `truncate`         |
| Guard     | `auth.guard.ts`             | `authGuard`            | —                  |
| Routes    | `users.routes.ts`           | `USER_ROUTES`          | —                  |

```
src/app/
├── core/           # Guards, interceptors, singleton services
├── shared/         # Reusable components, pipes, directives
├── features/
│   └── employees/
│       ├── employee-list.component.ts
│       ├── employee-list.component.html
│       ├── employee-list.component.scss
│       ├── employee.service.ts
│       ├── employee.model.ts
│       └── employee.routes.ts
├── app.component.ts
├── app.component.html
├── app.component.scss
└── app.config.ts
```

---

## Security

- Never `bypassSecurityTrust*` unless absolutely required
- Use `HttpClient` (not `fetch`) for interceptor support
- No sensitive data in `localStorage`

---

## Performance

- `OnPush` everywhere
- Signals over `async` pipe for granular re-rendering
- Lazy load all routes and heavy components
- `NgOptimizedImage` for all images
