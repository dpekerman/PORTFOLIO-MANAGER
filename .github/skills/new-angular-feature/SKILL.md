---
name: new-angular-feature
description: "Scaffold a new Angular feature for Portfolio Manager. Use when creating a new page, feature module, route, or adding a new API-backed feature. Generates the full two-service pattern (api + state), component files, and route registration."
argument-hint: "feature name (e.g. 'tax-report', 'dividends')"
---

# New Angular Feature

Scaffolds a complete feature following the two-service state pattern used in this project.

## When to Use

- Adding a new page/route to the app
- Creating a new feature with backend data
- User asks to "add a new feature", "create a new page", "scaffold a feature"

## Files to Create

Given feature name `foo-bar`, create these files:

```
frontend/portfolio-manager-ui/src/app/features/foo-bar/
├── foo-bar-page.component.ts
├── foo-bar-page.component.html
├── foo-bar-page.component.scss
└── foo-bar.routes.ts

frontend/portfolio-manager-ui/src/app/core/services/
├── foo-bar-api.service.ts
└── foo-bar-state.service.ts
```

## Templates

See [./assets/templates.md](./assets/templates.md) for copy-paste file templates.

## Steps

1. Create the two service files in `core/services/` using the templates
2. Create the component files in `features/foo-bar/` using the component template
3. Register the route in `app.routes.ts`:
   ```typescript
   {
     path: 'foo-bar',
     loadChildren: () => import('./features/foo-bar/foo-bar.routes').then(m => m.FOO_BAR_ROUTES),
   }
   ```
4. Add nav link in the shared layout component (`shared/layout/`)
5. Add any new TypeScript model types to `core/models/portfolio.models.ts`
6. Add corresponding backend endpoints in `Controllers/` and `Services/` if needed

## Rules

- **Never** use `standalone: true` — default since Angular v20+
- **Always** `templateUrl` + `styleUrl` — never inline
- **Always** `ChangeDetectionStrategy.OnPush`
- **Always** `inject()` — no constructor injection
- State service exposes `.asReadonly()` signals; API service returns `Observable<T>` with zero state
- Wrap all monetary values through `demoMode.maskValue()` / `maskPercent()`
