# Angular 22 modernization: signal stores and phone support

Branch `feature/angular22-signalstore-mobile`. This document describes what changed, how the new
pieces fit together, and what was deliberately left for later.

## Corrections to the original assumptions

The work started from an outside analysis that assumed Angular 20 and NgRx. The repository was
already modern, so the effort was reshaped:

| Assumed | Reality |
|---|---|
| Needs a 20 -> 21 -> 22 migration | Already Angular 22.0.1; now 22.2.2 (minor bump) |
| Uses NgRx Signal Store | Hand-rolled signal services; NgRx Signals was added by this work |
| Material M2 theming | Already M3 `mat.theme()` with dark and light |
| Legacy APIs to clean up | None: no `@Input`, `*ngIf`, `ngClass`, constructor DI; all components OnPush |
| Needs `provideZonelessChangeDetection()` | Zoneless is the default since v21 and no zone.js is configured |

## Architecture: before and after

Before, every feature had a hand-written `*-state.service.ts` repeating the same boilerplate:

```typescript
private readonly _items = signal<T[]>([]);
private readonly _loading = signal(false);
private readonly _error = signal<string | null>(null);
readonly items = this._items.asReadonly();
readonly loading = this._loading.asReadonly();
readonly error = this._error.asReadonly();
// ...plus a load method doing loading.set(true) -> subscribe -> set(data) -> set(false) / error
```

After, a `signalStore` in `core/store/` (the `*-api.service.ts` files are unchanged):

```typescript
export const WatchlistStore = signalStore(
  { providedIn: 'root' },
  withState<WatchlistState>({ items: [], fromSnapshot: false }),
  withRequestStatus(),
  withComputed(({ items }) => ({ count: computed(() => items().length) })),
  withMethods((store, api = inject(PortfolioApiService)) => ({ /* rxMethod loads, mutations */ })),
  withHooks({ onInit: (store) => onceAuthenticated(() => store.loadSnapshot()) }),
);
```

Public signal names were kept, so components and templates changed very little. State can only be
patched inside the store, which fixed leaky public writable signals such as `fromSnapshot`.

### Shared store features (`core/store/features/`)

| Feature | Purpose |
|---|---|
| `withRequestStatus()` + `setLoading/setLoaded/setError` | `loading` / `error` state with one set of transitions |
| `onceAuthenticated(cb)` | Runs the initial load after the first authentication, avoiding the 401 race on app start |
| `withStorageSync(write)` | Mirrors store state to localStorage |

### Store inventory (23)

`WatchlistStore`, `ThemeStore`, `DemoModeStore`, `DashboardCollapseStore`, `ConfigStore`,
`UserPreferencesStore`, `AccountTypesStore`, `PortfolioBetaStore`, `MarketLeadershipStore`,
`SecurityAnalysisMappingStore`, `WatchlistRsiStore`, `AuthStore`, `ScannerStore`,
`PortfolioValueHistoryStore`, `EodSignalsStore`, `CashStore`, `GridColumnStore`, `DashboardStore`,
`PortfolioStore`, `OptionStore`, `AutomationStore`, `AppRefreshStore`, `BreakpointStore`.

Left as plain services on purpose: `decision-engine`, `portfolio-rsi-analysis`, `trade-cash-link`,
`portfolio-final-action-sync`, `session-timeout` (pure logic or orchestration) and
`ScreenRefreshService` (constructed per screen with `new`, owns an `AbortController`).

Financial computeds (`totalValue`, `totalCost`, `totalGainLoss`, percentages, option multipliers)
and the demo-mode masking algorithms were moved verbatim.

## Phone support

Supported devices: Samsung S21 Ultra (384px), Samsung S23 (360px), Pixel Tablet, and desktop.
Design minimum is 360px; desktop rendering is unchanged (compared pixel-for-pixel before and after).

Tables render as the normal table on tablet and desktop, and as stacked cards below 600px, reusing
the same cell templates:

1. `BreakpointStore` exposes `isHandset` / `isTablet` / `isDesktop` (CDK `BreakpointObserver`).
2. `ColumnDef.priority` (`'primary'` | `'secondary'`) in `core/store/grid-registry.ts` marks the
   columns kept on a phone. `GridColumnStore.getResponsiveColumnKeys(grid)` returns the user's saved
   columns unchanged on tablet and desktop, and the primary + actions subset on handsets. It is a
   view-time projection and never writes saved preferences.
3. `appResponsiveTable` (directive) sets `data-responsive="cards"` and generates per-cell labels
   from the header text; `app-mobile-table-controls` provides sort and an "All fields" toggle.
4. Card rules live in global partials (`styles/_responsive-table.scss`, `_responsive-pages.scss`)
   so component stylesheets stay within their budgets. Dialogs were made to fit phones.

All pages were made phone-ready: transactions, portfolio, watchlist, scanner, EOD signals,
value screener, allocation, config, portfolio history and dashboard. Details: the
`responsive-material-table` skill.

## Tooling and quality

- Angular 22.0.1 -> 22.2.2; `provideBrowserGlobalErrorListeners()` and `withFetch()` added;
  deprecated `provideAnimationsAsync` and the unused `@angular/animations` removed.
- Angular CLI MCP config fixed (a duplicate config without `cwd` served v20 docs); Context7 added.
- Official Angular skills vendored in `.github/skills/` (`VENDORED.md`); project skills rewritten.
- Unit tests: 160 -> 360 (Vitest). They now run in CI; previously they never did.
- Playwright e2e suite (`e2e/`, API fully mocked): smoke, navigation, tables, no-horizontal-overflow
  and card-layout specs on `desktop-chrome`, `tablet`, `mobile-s21`, `mobile-s23`. Runs in CI.
- All `::ng-deep` removed; all inline templates/styles moved to external files.

## Known limitations and deferred work

- **Signal Forms migration (18 forms) is deferred.** The app is zoneless, so programmatic Reactive
  Forms `setValue`/`patchValue` do not schedule change detection. No bug was confirmed, but it is a
  risk. A compatibility spike confirmed Signal Forms bind directly to `matInput`, `mat-select`,
  `mat-checkbox`, `mat-slide-toggle`, date pickers and autocomplete, and that `mat-error` works.
- A few dense tables (dashboard) scroll inside their card instead of becoming card lists.
- The global `.mat-mdc-header-cell` override is kept because the token replacement changed
  desktop headers.
- Five component stylesheets still exceed the 12 kB budget (pre-existing warnings, unchanged).
- `config-page.component.ts` (~1400 lines) was only made responsive, not decomposed.
- npm 10 cannot resolve Angular 22.2's exact-version peers from scratch; the lockfile was
  generated with npm 11 and installs fine with `npm ci` on npm 10.

## Verification

Production build (no errors, same 5 pre-existing style-budget warnings), 360 unit tests passing, and
the full Playwright suite on all 4 projects (282 passed, 66 skipped handset-only checks, 0 failed)
were run before the PR. Initial bundle: 473.5 kB at the start of the signal-store work, 494.9 kB at
the end (NgRx Signals plus the card-layout code), under the 500 kB warning budget but close to it.
