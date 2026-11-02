---
name: responsive-material-table
description: "Make a Material table work on phones in Portfolio Manager. Use when adding or changing any <table mat-table>, a GRID_REGISTRY grid, or a page that must fit 360px phones (Samsung S21 Ultra / S23) and the Pixel Tablet."
argument-hint: "grid id or page (e.g. 'transactions-stocks')"
---

# Responsive Material table

Tables render as a normal table on tablet/desktop and as stacked cards on handsets (<600px), using the **same** `<td>` templates. Desktop must stay identical: every card rule is scoped to `table[data-responsive='cards']` or a handset media query.

## 3 steps (registry-backed grids)

1. **Prioritise columns.** In `core/store/grid-registry.ts` set `priority: 'primary'` on the 4-6 decision-relevant columns of the grid. `priority` controls handset visibility only; it never changes desktop.
2. **Use the responsive keys.** In the page, replace `getColumnKeys('<grid>')` with `getResponsiveColumnKeys('<grid>')` from `GridColumnStore`. On desktop/tablet it returns exactly the user's saved columns. On handsets it projects primary + actions columns at view time and never writes saved preferences.
3. **Opt the table in.** On the table add `appResponsiveTable` and `#sortRef="matSort"`, and above it:
   `<app-mobile-table-controls [sort]="sortRef" gridId="<grid>" label="..." />`
   (sort select + direction + "All fields" toggle, because the header row is hidden in card mode).

Import `ResponsiveTableDirective` and `MobileTableControlsComponent` from `shared/responsive-table/`.

## Tables without a registry grid

Still add the directive; card labels come from the header text. For wide tables derive the handset column list with `BreakpointStore.isHandset()` in a `computed`, keeping desktop columns unchanged.

## Pitfalls

- Fixed `width`/`min-width` on cells, sticky columns and `colgroup` fight cards; scope them to `@media (min-width: 600px)` or override under `[data-responsive='cards']` (shared rules live in `src/styles/_responsive-table.scss` and `_responsive-pages.scss`, so component SCSS budgets do not grow).
- Aggregated rows, expanded detail rows and `td[colspan]` "no results" rows must stay readable in card mode.
- Filters, chips and toolbars above a table must wrap at 360px with 44px touch targets.
- No new `::ng-deep`; use Material system tokens (`var(--mat-sys-*)`) so dark and light themes both work.
- Monetary values still go through `DemoModeStore.maskValue()` / `maskPercent()`.

## Verify

`npx playwright test --project=desktop-chrome --project=mobile-s23` plus the page's case in `e2e/responsive-cards-pages.spec.ts` (cards on handset, normal table on desktop/tablet, no page overflow). Devices in `e2e/`: `mobile-s21` (384x854), `mobile-s23` (360x780), `tablet` (800x1280).
