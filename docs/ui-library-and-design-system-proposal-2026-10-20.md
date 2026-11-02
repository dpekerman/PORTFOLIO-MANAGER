# Portfolio Manager – UI Library & Design-System Proposal

Status: **Investigation + proposal (no code changed)**
Audience: Engineering, Business, UI/UX
Scope: `frontend/portfolio-manager-ui` (Angular 22, Material 22, NgRx Signals, zoneless)

---

## 1. Short answers to your questions

| Question | Answer |
|---|---|
| Do we need to redesign the whole project? | **No.** A rewrite is not needed and would be risky. The data layer (api services + signal stores) is already clean. The mess is in the **presentation layer** (templates + SCSS). Fix it by *incrementally extracting* a UI library, one page at a time. |
| Is VALUE_ACCESSOR the right main tool? | **Partly.** CVA is right only for *form inputs* (select, date, money, percent, symbol picker). It does not help with cards, badges, KPI tiles, tables, dialogs – those need *presentational components + design tokens + SCSS mixins*. Also, the project mandates **Signal Forms** for new forms (Angular 22, stable) which replace CVA with `FormValueControl<T>` (see §5). |
| Is this a big task? | Yes – **large but safely splittable** into ~8 independent phases. Each phase is shippable on its own and the app stays working throughout. |
| Token / effort cost? | Roughly: foundation (tokens + base components) is **small** (a few focused sessions). The migration of 12 feature pages is the **bulk** (the 4 largest pages alone are ~7,000 lines of SCSS and ~5,500 lines of HTML). Realistically many sessions, working page-by-page. Not "one big prompt". |
| Is it complex for me (the AI)? | Technically moderate; the risk is **visual regressions**, not logic. Mitigation: Playwright screenshots (4 device projects already exist) as a safety net before each phase. |

---

## 2. What the investigation found (facts from the codebase)

### 2.1 Size
- 165 TS files (~24.5k lines), 83 HTML (~14.7k), **89 SCSS (~17.8k lines)** – SCSS is almost as large as the TypeScript.
- Biggest SCSS: portfolio page 1,586 · dashboard 1,561 · watchlist 1,226 · RSI scanner table 1,136 · `_config-tabs` 1,029.
- Biggest templates: portfolio page **2,119 lines**, watchlist 1,284, dashboard 1,193, transactions 892.

### 2.2 Design is not centralized (biggest problem)
| Metric | Count | Meaning |
|---|---|---|
| Distinct 6-digit hex colors in SCSS | **153** | Red/green/amber hard-coded everywhere (`#ff5252` ×48, `#00c853` ×47, `#ffa726` ×45, `#ef5350` ×43, `#c62828` ×40…). Several different "reds" and "greens" for the same meaning (gain/loss). |
| Hex + rgba color literals total | ~1,740 | vs only 731 `var(--…)` uses |
| Distinct `font-size` values | **58** | No type scale |
| Distinct `border-radius` values | 16 (1px…100px) | No radius scale |
| `@media` queries | 90, with **9 different max-width breakpoints** (480/599.98/600/640/768/900/959.98/1000/1400) | `600px` and `599.98px` both used for "phone" |
| `!important` | 172 | Specificity fights with Material |
| Badge/pill/chip class definitions | 24 files each re-define their own | Same visual, many implementations |
| gain/loss (pos/neg) class definitions | re-declared in 6+ files | Business-critical color logic duplicated |

Only **13 CSS variables** are defined (`--bg-*`, `--text-*`, `--border-*`, `--shadow-card`). There are **no semantic tokens** for: success/danger/warning/info, gain/loss, spacing, radius, font size, elevation, z-index, breakpoints.

### 2.3 Global SCSS is page-specific, not a library
`src/styles/` contains `_responsive-pages` (677), `_responsive-dashboard-cards` (548), `_config-tabs` (1,029) etc. These are *page overrides*, not reusable primitives, so every new page copies from an old page.

### 2.4 Template duplication (counts across all HTML)
| Pattern | Count |
|---|---|
| `<mat-form-field>` | 142 (in 38 files) – each re-declares label/hint/error/suffix/density |
| `mat-select` | 50 |
| `mat-datepicker-toggle` | 19 |
| `mat-card` | 83 |
| `mat-button-toggle` | 44 |
| badge/pill/chip classes | 169 |
| `mat-icon` | ~1,200 |
| number/currency/percent pipes | 295 (formatting rules scattered, plus ~70 manual `toFixed/toLocaleString/Intl` calls in TS) |
| Dialog components | **18**, each with own header/actions/layout SCSS |
| Tables (`mat-table`) | 12 files, responsive handling partially solved by `responsive-table` directive |

### 2.4b Money masking (Demo mode) is not enforced by the type system
The rule "always use `maskValue()` / `maskPercent()`" is currently followed manually (few template hits). Every raw `| currency` is a leak risk. A single `<app-money>` / `appMoney` pipe removes this class of bug entirely.

### 2.5 What is already good (keep it)
- `core/services/*-api.service.ts` + `core/store/*.store.ts` pattern – clean, leave untouched.
- `shared/ui/` already has `page-header`, `settings-section`, `sub-card`, `list-editor` – seed of the library.
- `shared/responsive-table` (directive + mobile controls) – good base for `<app-data-table>`.
- `shared/skeleton`, `confirm-dialog`, `app-refresh-*` – reusable already.
- Exactly **one** CVA exists (`account-type-select`) – proves the pattern works here.
- Material 3 theme via `mat.theme()` with dark/light – we can map our tokens onto `--mat-sys-*`.
- Playwright with 4 device projects (desktop, tablet, S21, S23) – our regression net.

---

## 3. Target architecture

### 3.1 Layers
```
src/
  styles/                      <- the SCSS UI library (no page code)
    tokens/   _colors _semantic _spacing _radius _typography _elevation _breakpoints _motion
    themes/   _dark _light     (only maps raw palette -> semantic CSS variables)
    mixins/   _respond _card _badge _truncate _tabular-nums _focus-ring
    material/ _overrides       (ONE place for all mat-* customization; kills most !important/::ng-deep)
    utilities/_layout _text _spacing   (few helpers: .u-stack, .u-row, .u-gain, .u-loss …)
    index.scss
  app/ui/                      <- Angular UI library (presentational only, no stores/API)
    primitives/  icon-badge, status-badge, gain-loss, money, percent, kpi-tile, card, section, empty-state, skeleton
    form/        field (wrapper), select-field, date-field, money-field, percent-field, symbol-field,
                 toggle-field, segmented-field (button-toggle), chips-field, list-editor
    data/        data-table (+ mobile cards), column-config, toolbar, pagination
    overlay/     dialog-shell (header/body/actions), confirm, drawer/bottom-sheet on mobile
    layout/      page, page-header, toolbar, grid, split, tabs
    feedback/    progress, banner, toast
  app/features/                <- pages compose ui/* only; page SCSS shrinks to layout glue
```
Rule: `features/*` may import `ui/*` and `core/*`; `ui/*` may import neither features nor core stores (except a tiny `DemoModeStore` bridge inside `money`/`percent`).

### 3.2 Design tokens (the single source of truth for business/UI)
Three tiers, exposed as CSS custom properties so they can be re-themed at runtime (dark/light/future brand):

1. **Reference** (raw palette): `--ref-green-500` …
2. **Semantic** (what the UI means): `--color-gain`, `--color-loss`, `--color-warning`, `--color-info`, `--color-surface-1/2/3`, `--color-text-1/2/3`, `--color-border`, `--color-focus`, plus *on-* pairs for contrast.
3. **Component**: `--badge-radius`, `--card-padding`, `--field-height`, … (only where needed).

Scales to replace today's ad-hoc values:
- Spacing: 4-pt scale `--space-1…8` (4,8,12,16,24,32,48,64)
- Radius: `--radius-sm/md/lg/pill` (4/8/12/999) – replaces 16 values
- Type: ~8 steps `--text-xs…3xl` + `--text-metric` (tabular, large number) – replaces 58 values
- Breakpoints: **3 only** – `compact` (<600, phones 360–384), `medium` (600–959, Pixel Tablet portrait), `expanded` (≥960). One SCSS mixin `@include respond(compact)` replaces the 9 variants.
- Elevation/shadow, motion durations, z-index layers.

Business benefit: change "loss red" or card radius **once** → entire app (desktop + mobile) follows.

---

## 4. Which logic to extract (priority list)

| # | Component / helper | Replaces | Value |
|---|---|---|---|
| 1 | **Tokens + `respond()` mixin + Material overrides** | 153 hex, 58 font sizes, 9 breakpoints, most `!important` | Highest, lowest risk, no template change |
| 2 | `app-money` / `app-percent` / `app-gain-loss` (+ pipe) | 295 pipes, manual `toFixed`, pos/neg classes, demo-mask rule | Business-critical correctness |
| 3 | `app-badge` (variants: gain, loss, warn, info, neutral; sizes) | 169 badge/pill usages, 24 style copies | Visual consistency |
| 4 | `app-field` + typed field controls (§5) | 142 form-fields | Biggest template reduction |
| 5 | `app-dialog-shell` (title, close, scrollable body, sticky actions; bottom-sheet on phones) | 18 dialogs | Mobile consistency |
| 6 | `app-card`, `app-kpi-tile`, `app-section` | 83 mat-card + KPI/summary tiles | Dashboard/portfolio cleanup |
| 7 | `app-data-table` (columns config + sticky header + mobile card mode built in) | 12 tables, GRID_REGISTRY glue | Largest SCSS saving |
| 8 | `app-page`, `app-toolbar`, `app-empty-state`, skeleton set | Page chrome repeated per page | Consistent look |

---

## 5. About VALUE_ACCESSOR (important decision for you)

Angular 22 gives two ways to build reusable form controls:

| | Reactive Forms CVA (`NG_VALUE_ACCESSOR`) | **Signal Forms** `FormValueControl<T>` / `FormCheckboxControl` |
|---|---|---|
| Status | Legacy, still supported | **Stable in v22; project rule says new forms use it** |
| Boilerplate | High (`writeValue`, `registerOnChange`, `registerOnTouched`, `setDisabledState`, providers, optional `NG_VALIDATORS`) | Low – a component with `value = model<T>()`, plus optional `disabled`, `errors`, `touched` inputs |
| Works with zoneless | Needs manual `markForCheck`/signals | Native |

**Recommendation:** build every field control against a small internal contract (`value`, `disabled`, `invalid`, `errors`, `touched`) implemented as **`FormValueControl`** (new forms). For the ~46 files still on Reactive Forms, provide a thin **CVA adapter base class** (`BaseValueAccessor<T>`) so the *same* component works in both worlds during the migration. When the last Reactive form is gone the adapter is deleted. This honours your VALUE_ACCESSOR request without locking us into legacy API.

### 5.1 Self-validating controls (NG_VALUE_ACCESSOR + NG_VALIDATORS)

Following the existing `account-type-select` pattern, every library control provides **both** `NG_VALUE_ACCESSOR` and `NG_VALIDATORS`, so a page only writes `<app-money-field formControlName="price">` and gets value handling, validation and error messages with no per-page code.

- `BaseFieldControl<T>` (abstract) implements `ControlValueAccessor` and `Validator` once: `writeValue`, `registerOnChange`, `registerOnTouched`, `setDisabledState`, `validate()`, plus signals for `value`, `disabled`, `touched` and `errors`. Both providers are registered through a single helper, `provideFieldControl(Component)`, to remove the boilerplate.
- Each concrete control only declares its own rules, for example:
  - `money-field`: required, min/max, 2-decimal precision, currency.
  - `percent-field`: range 0-100.
  - `symbol-field`: valid ticker format.
  - `quantity-field`: positive, optional integer.
  - `date-field`: min/max date, not in the future.
  - `account-type-field`: value exists in `AccountTypesStore`.
- Error messages come from one central map (`required`, `min`, `max`, `pattern`, ...). Controls render them inside `app-field`, so wording is consistent on desktop and mobile and can be translated or changed in one place.
- Cross-field rules (for example, a sell quantity must not exceed the holding) stay as form-level validators in the feature, not in the library.
- The same contract (`value`, `disabled`, `errors`, `touched`) maps to Signal Forms `FormValueControl`, so these controls survive the migration.

Typed field set: `select-field`, `symbol-field` (autocomplete + Yahoo lookup), `money-field` (currency, locale, demo-mask aware), `percent-field`, `quantity-field`, `date-field`, `toggle-field`, `segmented-field`, `chips-field`, `account-type-field` (exists – migrate).

---

## 6. Phased plan (each phase independently shippable)

| Phase | Deliverable | Risk | Exit criteria |
|---|---|---|---|
| 0 | **Safety net**: Playwright visual baseline screenshots for every route × 4 projects; inventory of colors/sizes (script) | Low | Baseline committed; CI green |
| 1 | **Tokens & theme**: `styles/tokens`, dark/light semantic variables, 3 breakpoints, `respond()` mixin, `material/_overrides`. Map existing `--bg-*`/`--text-*` to new names (aliases kept) | Low | No visual diff beyond agreed tweaks |
| 2 | **Primitives**: money/percent/gain-loss, badge, card, kpi-tile, empty-state; documented in a living style guide page (`/ui-kit`, dev-only) | Low | Unit tests + style-guide page reviewed by UI team |
| 3 | **Form controls**: `field` wrapper + typed controls, CVA adapter + Signal Forms contract | Medium | Migrate Config + one dialog as pilot |
| 4 | **Dialog shell** + migrate 18 dialogs | Medium | Phone bottom-sheet behavior verified on S21/S23 |
| 5 | **Data table** (evolve `responsive-table`) + migrate Transactions, Audit, small tables | Medium | Existing responsive specs still pass |
| 6 | **Page migrations**, smallest → largest: Config, Audit, Transactions, Allocation, EOD, Value Screener, Scanner, Watchlist, Dashboard, **Portfolio last** | Medium/High | Page SCSS cut ≥50%, screenshots match |
| 7 | **Cleanup & guardrails**: stylelint rules (ban hex colors, ban non-token font-size/radius, ban custom `@media`), ESLint rule (no `| currency` outside ui/money), delete dead SCSS & `_responsive-*` page files | Low | Lint enforces the system |

Expected outcome (targets, to be validated in Phase 0 baseline): hex literals 1,700 → <100 (only in token files), distinct font sizes 58 → ~9, radii 16 → 4, breakpoints 9 → 3, `!important` 172 → <20, feature SCSS −50…−60%, templates −25…−35%.

---

## 7. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Visual regressions on 360px phones | Phase-0 screenshots on 4 device profiles; migrate page-by-page behind identical selectors first |
| Mega-pages (portfolio 2,119-line template) hard to untangle | Migrate last, split into sub-components as part of the move |
| Material internals change on upgrade | All Material tweaks isolated in `material/_overrides`, using official `--mat-*` tokens / mixins (not `::ng-deep`) |
| Two form worlds (Reactive + Signal) | Adapter base class, delete after migration |
| Over-engineering a library for one app | Keep it **inside the repo** (`app/ui` + `styles/`), no npm packaging, no Storybook until needed; the `/ui-kit` page doubles as the style guide |
| Business logic leaking into ui/* | Lint boundary rule (`ui` cannot import `features`/`core/store`) |
| Demo-mode money masking regressions | `money`/`percent` are the only place that reads `DemoModeStore`; unit-test it |

---

## 8. Decisions needed from Business / UI people

1. **Brand & palette**: keep current dark-first look, or provide an official palette? (Defines the *reference* tokens.)
2. **Gain/Loss semantics**: green/red only, or add colour-blind-safe cues (▲/▼ icons, +/− signs)? Recommended: yes, always icon + sign.
3. **Density**: one density for desktop and a larger touch density on mobile (recommended), or identical?
4. **Dialogs on phones**: full-screen sheet vs bottom sheet?
5. **Tables on phones**: card layout (current) for all tables, or only for wide ones?
6. **Light theme**: keep as first-class (doubles QA effort) or deprecate?
7. **Typography**: keep Inter; acceptable base size 16px desktop / 18px handset (current behaviour)?
8. **Priority order of pages** for migration (suggested in Phase 6).
9. **Visual changes allowed?** "Pixel-identical refactor" vs "consistency pass may change spacing/colors slightly". The latter removes ~60% of effort in matching legacy quirks.

---

## 9. Suggested first step (smallest valuable slice)

Phase 0 + Phase 1 only: baseline screenshots, token files, breakpoint mixin, Material overrides, plus a script that replaces the top ~15 repeated hex colours with semantic tokens. This is low risk, immediately visible to the UI team, and proves the approach before any component migration.

---

## 10. Out of scope
- Backend (.NET) changes.
- Rewriting stores/api services or `decision-engine` logic.
- Publishing the library as a separate npm package (can be revisited after Phase 7).
