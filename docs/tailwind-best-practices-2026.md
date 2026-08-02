# Portfolio Manager — Best Practices & Modernisation Report

**Date:** 2026-08-02  
**Branch:** `feature/tailwind`  
**Scope:** Angular 22 front-end · .NET 8 back-end · Tailwind CSS integration

---

## 1. Tailwind CSS Setup (Current State — Correct)

### What is in place

| File                   | Purpose                                                                   |
| ---------------------- | ------------------------------------------------------------------------- |
| `.postcssrc.json`      | Registers `@tailwindcss/postcss` for the Angular build pipeline           |
| `src/tailwind.css`     | CSS entry point — imports Tailwind theme + utilities (no preflight)       |
| `src/styles/pm-ui.css` | Shared `@layer components` — project-specific reusable classes            |
| `angular.json` styles  | `tailwind.css` listed before `styles.scss` so Material overrides baseline |

### Why preflight is skipped

```css
/* tailwind.css */
@import "tailwindcss/theme"; /* Tailwind design tokens (colors, spacing…) */
@import "tailwindcss/utilities"; /* All utility classes */
/* NOT @import 'tailwindcss' — that would include preflight */
```

Tailwind Preflight resets browser defaults (headings, borders, colours). Angular Material already manages base styles; importing preflight breaks the dark/light theme switching, Angular Material form fields, and typography.

### Rule going forward

**Never** replace this with `@import "tailwindcss"`. If a future Tailwind version changes selective import syntax, check the v4 changelog before updating.

---

## 2. Shared Component Layer — `src/styles/pm-ui.css`

### What it contains (as of this branch)

| Class group  | Classes                                                                                                                                                                                              | Used for                                      |
| ------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------- |
| Buttons      | `.pm-btn`, `.pm-btn-primary`, `.pm-btn-outline`, `.pm-btn-accent`, `.pm-btn-info`, `.pm-btn-success`                                                                                                 | All action buttons across migrated pages      |
| Tab nav      | `.pm-tab-nav`, `.pm-tab-btn`, `.pm-tab-btn-active`, `.pm-tab-btn-demo`                                                                                                                               | Section tab navigation                        |
| Section card | `.pm-section`, `.pm-section-header`, `.pm-section-title`, `.pm-section-subtitle`, `.pm-section-body`, `.pm-section-footer`, `.pm-section-footer-hint`, `.pm-section-badge`, `.pm-section-eod-active` | Content section cards with header/body/footer |
| Sub-cards    | `.pm-sub-card`, `.pm-sub-card-header`, `.pm-sub-card-title`, `.pm-sub-card-body`                                                                                                                     | Secondary cards inside sections               |
| Badges       | `.pm-badge`, `.pm-badge-primary`, `.pm-badge-warning`                                                                                                                                                | Count/status pills                            |
| Banners      | `.pm-banner`, `.pm-banner-warning`, `.pm-banner-info`, `.pm-banner-demo-active`, `.pm-banner-neutral`                                                                                                | Informational/alert strips                    |
| Alloc panels | `.pm-alloc-panel`, `.pm-alloc-row`, `.pm-alloc-pct`, `.pm-alloc-total`, `.pm-alloc-total-ok`, `.pm-alloc-total-warn`                                                                                 | Allocation/risk table rows                    |
| List panels  | `.pm-list-panel`, `.pm-list-header`, `.pm-list-title`, `.pm-list-scroll`, `.pm-list-empty`, `.pm-list-item-btn`                                                                                      | Filterable lists (sectors, industries)        |

### Rule going forward

- **Never duplicate** any of these patterns in a component's SCSS file. Add to `pm-ui.css` once.
- Use Tailwind utilities (e.g. `flex gap-4 p-5`) for one-off layout needs in component HTML.
- Use `pm-ui.css` classes for semantically recurring patterns (cards, tabs, banners, buttons).

---

## 3. Per-Page Migration Checklist

When migrating a page from custom SCSS to Tailwind:

```
[ ] Replace .sticky-save-bar / header wrapper → Tailwind arbitrary values with var(--bg-surface) etc.
[ ] Replace tab nav → .pm-tab-nav / .pm-tab-btn / [class.pm-tab-btn-active]
[ ] Replace card wrappers → .pm-section (or Tailwind arbitrary values for simpler cards)
[ ] Replace mat-flat-button → .pm-btn .pm-btn-primary  (remove mat-flat-button directive)
[ ] Replace mat-stroked-button → .pm-btn .pm-btn-outline  (remove mat-stroked-button directive)
[ ] Replace .spinning animation → Tailwind animate-spin
[ ] Clear the component's .scss file (or keep only truly component-specific rules)
[ ] Keep all Angular Material form controls (mat-form-field, mat-slide-toggle, mat-timepicker, mat-chips, mat-list, mat-icon-button) — these have no Tailwind equivalent
[ ] Do NOT add data-theme="..." attributes — the app already has a working dark/light theme system
[ ] Test dark mode AND light mode after migration
```

---

## 4. Angular 22 Best Practices (Late 2026)

### 4.1 Component authoring

| Rule                                                                 | Current status                                                  | Action                                                   |
| -------------------------------------------------------------------- | --------------------------------------------------------------- | -------------------------------------------------------- |
| No `standalone: false` (implicit `standalone: true` default in v19+) | ✅ App uses implicit standalone                                 | OK                                                       |
| `inject()` only — no constructor injection                           | ✅ Config page correct                                          | Audit older components (portfolio, transactions dialogs) |
| `input()` / `output()` signals — never `@Input()` / `@Output()`      | ⚠️ Most new components correct; older ones still use decorators | Migrate per page as touched                              |
| `@if` / `@for` / `@switch` — never `*ngIf` / `*ngFor`                | ✅ Config page correct                                          | Audit scanner, allocation, eod-signals                   |
| `ChangeDetectionStrategy.OnPush` on all components                   | ✅ Config page correct                                          | Confirm all new components use OnPush                    |
| `[class.x]` / `[style.x]` — never `ngClass` / `ngStyle`              | ✅ Correct throughout                                           | Keep                                                     |

### 4.2 Signals

| Pattern                                                                     | Recommendation                                                              |
| --------------------------------------------------------------------------- | --------------------------------------------------------------------------- |
| State in `*-state.service.ts` via `signal()`                                | ✅ Correct — keep                                                           |
| HTTP only in `*-api.service.ts` returning `Observable<T>`                   | ✅ Correct — keep                                                           |
| `computed()` for derived values (never signal subscriptions for derivation) | ✅ Correct — keep                                                           |
| Expose state as `.asReadonly()`                                             | ✅ Correct — keep                                                           |
| `toSignal()` to bridge RxJS → signals at integration points                 | Use when consuming `Observable` inside a component instead of `subscribe()` |

### 4.3 Template syntax to audit across the codebase

```typescript
// REMOVE these patterns wherever found:
*ngIf          → @if
*ngFor         → @for ... track
*ngSwitch      → @switch
ngClass        → [class.x]
ngStyle        → [style.x]
@Input()       → input()
@Output()      → output()
constructor injection → inject()
```

The scanner, portfolio dialogs, and transactions page have several of these patterns still.

---

## 5. SCSS / Styling Architecture

### Current state after this branch

```
styles.scss          — Angular Material theme, global CSS custom properties (--bg-*, --text-*, --border-*)
tailwind.css         — Tailwind theme + utilities entry point, imports pm-ui.css
src/styles/pm-ui.css — Shared component layer (buttons, tabs, cards, badges, banners)
component.scss       — Only truly component-specific rules (aim for empty on migrated pages)
```

### Rules going forward

1. **`--bg-*`, `--text-*`, `--border-*` are the design tokens.** Never hard-code colours in component SCSS; reference the CSS custom properties instead (`var(--bg-surface)`, `var(--text-primary)` etc.).
2. **Light/dark mode is driven by `body.light-theme` in `styles.scss`.** Any new CSS variable must be defined in both `:root` (dark default) and `body.light-theme`.
3. **Tailwind arbitrary values** (`bg-[var(--bg-surface)]`) are the bridge between Tailwind utilities and the existing design token system. This is intentional and correct.
4. **Component SCSS should trend to zero lines** as pages are migrated. Any remaining SCSS is a candidate for either `pm-ui.css` (if reusable) or an inline Tailwind class (if one-off).

---

## 6. Recommended Migration Order

Priority based on complexity and user-facing impact:

| Priority | Page / Feature              | Rationale                                        |
| -------- | --------------------------- | ------------------------------------------------ |
| 1        | `features/scanner`          | High-traffic, relatively self-contained          |
| 2        | `features/watchlist-page`   | Shares patterns with scanner                     |
| 3        | `features/eod-signals`      | Good example of banner/status patterns           |
| 4        | `features/value-screener`   | Complex filters but mostly layout                |
| 5        | `features/transactions`     | Many dialogs — migrate page first, dialogs later |
| 6        | `features/portfolio` (page) | Largest page — do last                           |
| 7        | All dialogs                 | Each small; migrate after their parent page      |
| 8        | `shared/` components        | Column config, layout shell                      |

---

## 7. Angular Material Form Controls — Keep, Don't Replace

The following Material components have no clean Tailwind equivalent and should **remain as Angular Material** indefinitely:

- `mat-form-field` + `matInput` — floating labels, hint text, error messages, overlay
- `mat-slide-toggle` — accessible toggle with animation
- `mat-timepicker` — complex overlay component
- `mat-chip-grid` / `mat-chip-row` — chip input with keyboard navigation
- `mat-list` / `mat-list-item` — virtualised list with accessibility roles
- `mat-select` / `mat-option` — themed dropdown with overlay
- `mat-dialog` — CDK overlay, focus trap, animation
- `mat-snack-bar` — toast notification
- `mat-icon` — Material icon font
- `mat-icon-button` — small accessible circular tap target
- `matTooltip` — CDK tooltip
- `mat-timepicker-toggle`

**Rule:** Keep all of these. When Tailwind classes appear next to these components, they should only target **wrapper divs**, never the Material component element itself.

---

## 8. Backend (.NET 8) — Areas to Align

| Area                          | Current state                           | Recommended action                                                                                |
| ----------------------------- | --------------------------------------- | ------------------------------------------------------------------------------------------------- |
| OpenAPI / Swagger             | Not confirmed in scope                  | Add `Swashbuckle.AspNetCore` if not present; generate TypeScript client with `openapi-typescript` |
| Global error handling         | Per-controller try/catch likely         | Add `IExceptionHandler` middleware (available since .NET 8)                                       |
| Cancellation tokens           | Not confirmed                           | Pass `CancellationToken` to all async controller and service methods                              |
| EF query logging              | Dev only                                | Confirm `EnableSensitiveDataLogging` is OFF in production appsettings                             |
| Background service resilience | `RsiAlertBackgroundService` uses timers | Add `PeriodicTimer` (preferred over `Timer` in .NET 6+) and structured logging per cycle          |
| Health checks                 | Not in scope yet                        | Add `/healthz` endpoint using `Microsoft.Extensions.Diagnostics.HealthChecks`                     |

---

## 9. What This Branch Leaves Ready

- ✅ Tailwind CSS v4 + `@tailwindcss/postcss` installed and configured
- ✅ Dark mode works (preflight skipped; Angular Material owns the base layer)
- ✅ `src/styles/pm-ui.css` — complete shared component layer for all future migrations
- ✅ Config page migrated — matches original design pixel-for-pixel, zero SCSS
- ✅ DaisyUI removed
- ✅ Build passes, no TypeScript errors
- ✅ No auto-push — changes staged locally for code review
