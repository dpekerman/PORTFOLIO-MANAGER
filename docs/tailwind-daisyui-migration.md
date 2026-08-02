# Tailwind CSS + DaisyUI Migration — Feature Branch `feature/tailwind`

## Overview

This document describes the setup, rationale, and implementation of the Tailwind CSS v4 + DaisyUI v5 integration on the `feature/tailwind` branch. The **Configuration page** was chosen as the proof-of-concept migration target.

---

## Packages Added

| Package | Version | Role |
|---|---|---|
| `tailwindcss` | v4.x | CSS framework — utility classes |
| `@tailwindcss/postcss` | v4.x | PostCSS plugin that drives Tailwind v4 |
| `postcss` | latest | CSS post-processing pipeline |
| `daisyui` | v5.x | Component library on top of Tailwind |

Install command used:
```bash
npm install tailwindcss @tailwindcss/postcss postcss --force
npm i -D daisyui@latest
```

---

## Configuration Files

### 1. `.postcssrc.json` (new)
Located at `frontend/portfolio-manager-ui/.postcssrc.json`.

```json
{
  "plugins": {
    "@tailwindcss/postcss": {}
  }
}
```

Tells the Angular build pipeline (Vite/esbuild) to run the Tailwind PostCSS plugin over all compiled CSS.

### 2. `src/tailwind.css` (new)
Located at `frontend/portfolio-manager-ui/src/tailwind.css`.

```css
@import "tailwindcss";
@plugin "daisyui";
```

A plain CSS file is required because SCSS will attempt to resolve `@import "tailwindcss"` as a Sass file and fail. This file is the PostCSS entry point that:
- Imports Tailwind's preflight (base reset), theme tokens, and utility engine
- Loads DaisyUI as a Tailwind plugin (adds component classes + CSS variables for themes)

### 3. `angular.json` — styles array updated
```json
"styles": ["src/tailwind.css", "src/styles.scss"]
```

`tailwind.css` is listed **first** so that Angular Material's styles (in `styles.scss`) load after Tailwind's preflight and can override base resets where needed.

---

## Why a Separate CSS File?

Tailwind v4 uses a CSS `@import "tailwindcss"` directive processed at the PostCSS level. In SCSS files, Dart Sass attempts to resolve any `@import` that doesn't end in `.css` as a Sass module — this would fail since `tailwindcss` is not a Sass file.

**Solution:** A dedicated `src/tailwind.css` file carries the Tailwind and DaisyUI imports. Angular's build processes `.css` files directly through PostCSS without SCSS compilation, so Tailwind intercepts the import correctly.

---

## DaisyUI Theming

DaisyUI v5 uses `data-theme` attributes on any ancestor element to scope its CSS variables. The config page wrapper sets:

```html
<div class="p-6 pt-4 flex flex-col" data-theme="night">
```

- `data-theme="night"` activates DaisyUI's dark theme on the config page only
- Other pages are unaffected until they also adopt Tailwind/DaisyUI
- Token references like `bg-base-100`, `bg-base-200`, `text-base-content`, `text-primary`, `badge-warning` etc. all resolve from this theme scope

---

## What Changed on the Config Page

### HTML — `config-page.component.html`

All custom CSS class names were replaced with **Tailwind utilities** and **DaisyUI component classes**. The Angular template logic (signals, event bindings, `@if`/`@for` blocks, form bindings) is unchanged.

| Old custom class | New Tailwind / DaisyUI equivalent |
|---|---|
| `.config-page` | `p-6 pt-4 flex flex-col` + `data-theme="night"` |
| `.sticky-save-bar` | `sticky top-14 z-50 -mx-6 px-6 bg-base-200 border-b border-base-300 shadow-md` |
| `.sticky-save-bar-inner` | `flex items-center justify-between py-2.5 gap-4` |
| `.cfg-tab-nav` | DaisyUI `tabs tabs-border flex-wrap` |
| `.cfg-tab-btn` | DaisyUI `tab gap-1.5 text-[11px] font-semibold` |
| `.cfg-tab-btn--active` | DaisyUI `tab-active` |
| `.cfg-panel` + `.cfg-panel--active` | `[class.hidden]="activeSection() !== 'x'"` |
| `.config-section` | DaisyUI `card bg-base-100 border border-base-300 shadow-sm` |
| `.section-header` | `flex items-center gap-2.5 px-5 py-3.5 border-b-2 border-base-300` |
| `.section-title` | `text-sm font-bold text-base-content m-0 mb-0.5` |
| `.section-subtitle` | `text-[11px] text-base-content/50 m-0 leading-relaxed` |
| `.section-header-badge` | DaisyUI `badge badge-warning badge-sm` |
| `.section-body` | `p-5 flex flex-col gap-4` |
| `.section-footer` | `flex items-center justify-end gap-2.5 px-5 py-3 border-t border-base-300 bg-base-200 flex-wrap` |
| `.section-footer-hint` | `flex-1 text-[11px] text-base-content/50 italic` |
| `.sub-card` | `rounded-lg border border-base-300 bg-base-200 overflow-hidden` |
| `.eod-info-banner` | DaisyUI `alert alert-warning text-sm gap-2.5` |
| `.eod-inline-badge` | DaisyUI `badge badge-warning badge-sm` |
| `.demo-active-banner` | DaisyUI `alert alert-warning gap-2.5 font-bold text-sm` |
| `.demo-info-card` | DaisyUI `alert text-sm gap-3` |
| `.email-info` | DaisyUI `alert text-xs gap-2` |
| `.list-badge` / `.alloc-badge` | DaisyUI `badge badge-primary badge-sm/xs` |
| `mat-flat-button` directive | DaisyUI `btn btn-primary btn-sm gap-1` |
| `mat-stroked-button` directive | DaisyUI `btn btn-outline btn-sm gap-1` |
| `.spinning` CSS animation | Tailwind `animate-spin` |
| `.eod-active-icon` CSS animation | `[class.animate-pulse]` + `[class.!text-warning]` bindings |

### SCSS — `config-page.component.scss`

The file was cleared to two comment lines. All ~900 lines of custom SCSS are now expressed via Tailwind utilities and DaisyUI components in the HTML.

---

## What Was Kept as Angular Material

The following complex interactive components were **not** migrated to DaisyUI equivalents — they remain as Angular Material components:

- `mat-form-field` + `matInput` — form fields with floating labels, hints, and error messages
- `mat-slide-toggle` — toggle switches (Demo Mode, EOD Window, VS Schedule)
- `mat-timepicker` / `mat-timepicker-toggle` — time pickers
- `mat-chip-grid` / `mat-chip-row` — email recipient chip input
- `mat-list` / `mat-list-item` — Sectors and Industries lists
- `mat-icon` — Material icons (kept throughout)
- `mat-icon-button` — small circular icon buttons for edit/delete actions
- `matTooltip` — tooltips on buttons

These would be the next candidates for DaisyUI equivalents (`input`, `toggle`, `chip`, `menu`) in a full migration pass.

---

## Coexistence Strategy

Tailwind and Angular Material coexist with no conflicts because:
1. **Tailwind's preflight** loads first (element selectors only) — Angular Material's more specific component styles override it
2. **DaisyUI component classes** are applied to structural wrapper elements (`card`, `alert`, `badge`, `btn`, `tabs`) which do not overlap with Angular Material's internal DOM
3. **Tailwind utilities** on wrapper `div`s do not conflict with Material's internal component styles
4. **`data-theme="night"`** scopes DaisyUI's CSS variables to the config page only — no effect on other pages

---

## How to Extend to Other Pages

To migrate additional pages:

1. Add `data-theme="night"` (or any DaisyUI theme) to the component's root element
2. Replace custom SCSS layout classes with Tailwind utilities in the HTML
3. Replace static buttons with DaisyUI `btn` variants (remove `mat-flat-button` / `mat-stroked-button` directives)
4. Replace info banners/callouts with DaisyUI `alert`
5. Replace count pills with DaisyUI `badge`
6. Replace cards/panels with DaisyUI `card`
7. Clear the component's `.scss` file when all classes are moved to the template
8. Optionally replace Angular Material form controls with DaisyUI `input`, `select`, `toggle` for a fully DaisyUI-styled form

---

## Branch Info

- **Branch:** `feature/tailwind`
- **Base:** `develop`
- **Files changed:**
  - `frontend/portfolio-manager-ui/.postcssrc.json` ← new
  - `frontend/portfolio-manager-ui/angular.json` ← styles array updated
  - `frontend/portfolio-manager-ui/package.json` ← new dependencies
  - `frontend/portfolio-manager-ui/src/tailwind.css` ← new
  - `frontend/portfolio-manager-ui/src/app/features/config/config-page.component.html` ← full rewrite
  - `frontend/portfolio-manager-ui/src/app/features/config/config-page.component.scss` ← cleared

---

## Known Limitations of This Proof-of-Concept

1. **Angular Material form fields** still use Material's outline styling inside a DaisyUI dark theme. Contrast may need tuning per theme.
2. **`mat-icon-button`** uses Angular Material's circular touch-target styling. DaisyUI's `btn btn-ghost btn-circle` is the full equivalent but requires removing the Material directive.
3. **Global styles** in `styles.scss` (Angular Material theme, custom CSS variables) are not yet replaced — they still drive colours on all other pages.
4. **`data-theme` scope** — only the Config page uses `data-theme="night"`. A full migration would move this to `<html>` or `<body>` in `index.html`.
