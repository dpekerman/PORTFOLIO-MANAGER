# Shared UI building blocks

Presentational components (inputs/outputs + content projection, `OnPush`, no store/API injection).
Feature components own data and persistence and compose these.

| Component | Use it for |
|---|---|
| `app-page-header` | Page heading: icon, title, subtitle, badge/meta slots and an actions area (`ngProjectAs="[pageActions]"`). Wraps on phones. |
| `app-settings-section` | Card chrome (icon, title, subtitle) for a settings group. Loads `styles/_config-ui.scss`, scoped under `.cfg-tab`. |
| `app-sub-card` | Titled inner card inside a settings section. |
| `app-list-editor` | Filterable list with add / remove (sectors, industries, …). |

Helpers: `shared/utils/time.ts` (`HH:mm` ⇄ `Date` for the timepicker) and `shared/utils/forms.ts`
(`formDirtySignal`).

Config tabs register with `features/config/config-save.service.ts`, so the global Save / Reset All acts
on whatever tabs are mounted. Server state lives in the `core/store/*.store.ts` signal stores.

Mobile rules: design for 360px. Prefer `subscriptSizing: 'dynamic'` form fields (hints take space
instead of overlapping the next field) and verify with `npx playwright test` (all four projects).
