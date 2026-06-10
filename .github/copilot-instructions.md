---
applyTo: "**/*.ts,**/*.html,**/*.scss,**/*.css,angular.json,tsconfig*.json"
---

# Angular Development Instructions

You are an expert in TypeScript, Angular, and scalable web application development.
You write functional, maintainable, performant, and accessible code following Angular and TypeScript best practices.

> **Source:** These instructions are based on the [official Angular AI/LLM guide](https://angular.dev/ai/develop-with-ai) and [Angular Style Guide](https://angular.dev/style-guide). Angular version: **v21+**.

---

## TypeScript Best Practices

- Use strict type checking (`"strict": true` in `tsconfig.json`)
- Prefer type inference when the type is obvious
- Avoid the `any` type; use `unknown` when the type is uncertain
- Use `readonly` for properties that should not be reassigned
- Use `protected` for class members only used in templates

---

## Angular Architecture Best Practices

### Standalone Components (Angular v17+)

- **Always use standalone components** over NgModules
- **Do NOT set `standalone: true`** inside Angular decorators — it is the default in Angular v20+
- Bootstrap with `bootstrapApplication()` in `main.ts`
- Use `provideRouter()`, `provideHttpClient()`, etc. in `app.config.ts`

### Project Structure — Organize by Feature

```
src/
├── app/
│   ├── core/               # Singleton services, guards, interceptors
│   ├── shared/             # Shared components, pipes, directives
│   ├── features/
│   │   ├── feature-name/   # Feature-specific components/services
│   │   │   ├── feature-name.component.ts
│   │   │   ├── feature-name.component.html
│   │   │   ├── feature-name.component.scss
│   │   │   └── feature-name.routes.ts
│   └── app.component.ts
├── main.ts
└── app.config.ts
```

- **One concept per file** — one component, directive, or service per file
- **Name files with hyphens**: `user-profile.ts`, `user-profile.spec.ts`
- **Group related files** in the same directory (component TS + HTML + SCSS + spec)
- Avoid generic file names like `helpers.ts`, `utils.ts`, `common.ts`
- Avoid type-based directories like `components/`, `services/`, `directives/`

---

## Components & Directives

### Component Template

```typescript
@Component({
  selector: "app-user-profile",
  templateUrl: "./user-profile.component.html",
  styleUrl: "./user-profile.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserProfileComponent implements OnInit {
  // 1. Injected dependencies first
  private readonly userService = inject(UserService);

  // 2. Inputs / Outputs / Queries
  readonly userId = input.required<string>();
  readonly userSaved = output<void>();

  // 3. Signals / computed state
  protected readonly user = signal<User | null>(null);
  protected readonly displayName = computed(
    () => this.user()?.name ?? "Unknown",
  );

  // 4. Methods
  ngOnInit(): void {
    this.loadUser();
  }

  private loadUser(): void {
    // ...
  }
}
```

### Rules

- **Always set `changeDetection: ChangeDetectionStrategy.OnPush`**
- **Use `input()` and `output()` functions** instead of `@Input()` / `@Output()` decorators
- **Use `inject()` function** instead of constructor parameter injection
- Use `protected` access for members only used by the template
- Use `readonly` for `input()`, `output()`, `model()`, and query results
- **Do NOT use `@HostBinding` / `@HostListener`** — put host bindings in the `host` object of `@Component` or `@Directive` instead
- Keep components focused on presentation; move business logic to services
- Keep lifecycle hooks (`ngOnInit`, etc.) simple — delegate to well-named methods
- Always implement lifecycle interfaces (`implements OnInit`, `OnDestroy`, etc.)

---

## Signals (Angular v17+)

- **Use signals for local component state** (`signal()`, `computed()`, `effect()`)
- Use `computed()` for all derived state — it is lazily evaluated and memoized
- Use `signal.set()` or `signal.update()` to mutate — **do NOT mutate signal value directly**
- Expose writable signals as readonly with `.asReadonly()` from services
- Use `linkedSignal()` for writable state dependent on other signals
- Use `resource()` for async/data-fetching signal integration
- Read signals **before** `await` expressions in async `effect()` to preserve tracking
- Use `untracked()` when reading a signal without creating a dependency

```typescript
// Good: expose readonly from service
private readonly _count = signal(0);
readonly count = this._count.asReadonly();

// Good: computed derived state
protected readonly fullName = computed(() => `${this.firstName()} ${this.lastName()}`);

// Good: update pattern
this.items.update(items => [...items, newItem]);
```

---

## Templates

- **Use native control flow**: `@if`, `@for`, `@switch` — NOT `*ngIf`, `*ngFor`, `*ngSwitch`
- **Do NOT use `ngClass`** — use `[class]` or `[class.name]` bindings instead
- **Do NOT use `ngStyle`** — use `[style]` or `[style.prop]` bindings instead
- Use the `async` pipe for observables in templates
- Avoid complex logic in templates — move to `computed()` in the component class
- Name event handlers for what they do: `saveUser()` not `handleClick()`
- Use `NgOptimizedImage` (`ngSrc`) for all static images (not base64)

```html
<!-- Good -->
@if (user()) {
<div [class.admin]="isAdmin()" [class.active]="isActive()">
  {{ user()!.name }}
</div>
} @for (item of items(); track item.id) {
<app-item [item]="item" />
}
```

---

## Services

- Design services around a **single responsibility**
- Use `providedIn: 'root'` for singleton services
- Use `inject()` instead of constructor injection
- Use signals (not BehaviorSubject) for reactive state in services when possible

```typescript
@Injectable({ providedIn: "root" })
export class UserService {
  private readonly http = inject(HttpClient);

  getUser(id: string): Observable<User> {
    return this.http.get<User>(`/api/users/${id}`);
  }
}
```

---

## Routing

- Use **lazy loading** for all feature routes
- Define routes in `*.routes.ts` files per feature
- Use `loadComponent()` for standalone component lazy loading

```typescript
// app.routes.ts
export const routes: Routes = [
  {
    path: "users",
    loadChildren: () =>
      import("./features/users/users.routes").then((m) => m.USER_ROUTES),
  },
  {
    path: "settings",
    loadComponent: () =>
      import("./features/settings/settings.component").then(
        (m) => m.SettingsComponent,
      ),
  },
];
```

---

## Forms

- **Prefer Reactive Forms** over Template-driven forms
- Use `FormBuilder` with `inject()` to build forms
- Use typed forms (`FormControl<string>`, `FormGroup<...>`)

---

## Dependency Injection

- **Always use `inject()`** over constructor injection
- Use `providedIn: 'root'` for app-wide singletons
- Use component/route-level `providers` for scoped instances

---

## Naming Conventions

| Artifact  | File Name                | Class Name             | Selector           |
| --------- | ------------------------ | ---------------------- | ------------------ |
| Component | `user-profile.ts`        | `UserProfileComponent` | `app-user-profile` |
| Service   | `user.service.ts`        | `UserService`          | —                  |
| Directive | `highlight.directive.ts` | `HighlightDirective`   | `[appHighlight]`   |
| Pipe      | `truncate.pipe.ts`       | `TruncatePipe`         | `truncate`         |
| Guard     | `auth.guard.ts`          | `authGuard`            | —                  |
| Routes    | `users.routes.ts`        | `USER_ROUTES`          | —                  |

- Component selector prefix: `app-`
- Directive selector: camelCase attribute, e.g., `[appTooltip]`

---

## Accessibility

- All components MUST pass AXE accessibility checks
- Follow WCAG AA: focus management, color contrast, ARIA attributes
- Use semantic HTML elements

---

## Security

- Use Angular's built-in XSS protection (never use `bypassSecurityTrust*` unless absolutely necessary)
- Sanitize any user-generated HTML content
- Use `HttpClient` (not `fetch`) to benefit from Angular's security interceptors
- Do not store sensitive data in `localStorage` — use secure, HttpOnly cookies

---

## Performance

- Always use `OnPush` change detection
- Use signals instead of `async` pipes where possible for granular re-rendering
- Use `trackBy` equivalent in `@for` with `track` expression
- Lazy load routes and heavy components
- Use `NgOptimizedImage` for images (lazy loading, proper sizing, LCP)

---

## Angular Material

- Use Angular Material components from `@angular/material` for all UI elements
- Import individual Material modules (e.g., `MatButtonModule`, `MatInputModule`) in component imports
- Use `MatFormFieldModule` + `MatInputModule` for all form inputs
- Use `MatTableModule` for data tables
- Use `MatToolbarModule` for app headers/navigation bars
- Use `MatCardModule` for card-based layouts
- Use `MatDialogModule` for modal dialogs
- Use `MatSnackBarModule` for notifications/toasts
- Use `MatIconModule` with Material Icons for icons
- Use Material theming: configure a custom theme in `styles.scss`
- Prefer `mat-raised-button` or `mat-flat-button` for primary actions
- Use `mat-stroked-button` or `mat-button` for secondary actions
