---
name: new-angular-feature
description: "Scaffold a new Angular feature for Portfolio Manager. Use when creating a new page, route, or API-backed feature. Generates the api service + NgRx signal store pair, a mobile-ready page component, and route registration."
argument-hint: "feature name (e.g. 'tax-report', 'dividends')"
---

# New Angular Feature

Scaffolds a feature in this project's pattern: an `*-api.service.ts` (HTTP only) plus a `*.store.ts` (NgRx `signalStore`, all state), a lazy-loaded page, and a phone-ready layout.

## Files to Create

Given feature name `foo-bar`:

```
frontend/portfolio-manager-ui/src/app/features/foo-bar/
├── foo-bar-page.component.ts
├── foo-bar-page.component.html
├── foo-bar-page.component.scss
└── foo-bar.routes.ts

frontend/portfolio-manager-ui/src/app/core/services/foo-bar-api.service.ts   (HTTP only, no state)
frontend/portfolio-manager-ui/src/app/core/store/foo-bar.store.ts            (state, loading/error, mutations)
frontend/portfolio-manager-ui/src/app/core/store/foo-bar.store.spec.ts
```

Templates: [./assets/templates.md](./assets/templates.md).

## Steps

1. Create the API service and the store from the templates. Look at `core/store/watchlist.store.ts` for a complete real example (snapshot-then-refresh, optimistic-free mutations, snackbar errors).
2. Create the page component files; register the lazy route in `app.routes.ts`; add the nav link in `shared/layout/`.
3. Add model types to `core/models/portfolio.models.ts`; add backend endpoints if needed (`/new-backend-service`).
4. **Tables:** follow the `responsive-material-table` skill so the page works on phones.
5. Write the store spec (load, error, a mutation) and a Playwright smoke + overflow case (`e2e/`).
6. Run `npx ng build --configuration production`, `npx ng test --watch=false`, and the relevant Playwright project.

## Rules

- State lives in a `signalStore`, never in a component or a hand-rolled `signal()` + `asReadonly()` service. Use `withRequestStatus()` for `loading`/`error`, `rxMethod` + `tapResponse` for loads, and `onceAuthenticated()` (inside `withHooks({ onInit })`) for the initial load so nothing fires before the token exists.
- State is patched only inside the store (`patchState`); expose methods, not writable signals.
- API services return `Observable<T>` and hold no state.
- No `standalone: true`; `templateUrl` + `styleUrl`; `ChangeDetectionStrategy.OnPush`; `inject()`; `input()`/`output()`; `@if`/`@for`/`@switch`.
- Every monetary value goes through `DemoModeStore.maskValue()` / `maskPercent()`.
- The app is **zoneless** (the Angular 21+ default). Use signals for anything the template reads; programmatic Reactive Forms `setValue`/`patchValue` do not schedule change detection, so prefer Signal Forms (`@angular/forms/signals`) for new forms.
- Check unfamiliar Angular APIs against the Angular CLI MCP (`search_documentation`) or Context7 before using them.
