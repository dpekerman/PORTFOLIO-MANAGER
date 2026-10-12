# E2E baseline

Measured on the `feature/angular22-signalstore-mobile` branch before the mobile-first refactor,
with the fixtures in `e2e/fixtures` (every table populated). Overflow = the widest scroll root
(`html` or `mat-sidenav-content`) is wider than the viewport. See `support/overflow.ts` for why
`document.documentElement.scrollWidth` alone is not enough: the shell scrolls inside
`mat-sidenav-content`, so the document itself never overflows.

## Results

Smoke, navigation and tables: **green on all 4 projects**. Overflow (content width vs viewport):

| Route                    | desktop-chrome 1920 | tablet 820 | mobile-pixel 412 | mobile-iphone 393 |
| ------------------------ | ------------------- | ---------- | ---------------- | ----------------- |
| /login                   | ok                  | ok         | ok               | ok                |
| /dashboard               | ok                  | ok         | **572** ❌       | **573** ❌        |
| /portfolio               | ok                  | ok         | ok               | ok                |
| /watchlist               | ok                  | ok         | ok               | ok                |
| /scanner                 | ok                  | ok         | **430** ❌       | **411** ❌        |
| /eod-signals             | ok                  | ok         | ok               | ok                |
| /transactions            | ok                  | ok         | ok               | ok                |
| /allocation              | ok                  | ok         | **422** ❌       | **423** ❌        |
| /value-screener          | ok                  | ok         | ok               | ok                |
| /portfolio-value-history | ok                  | ok         | ok               | ok                |
| /config                  | ok                  | ok         | **617** ❌       | **617** ❌        |

The ❌ cells are the entries in `support/known-mobile-backlog.ts` (marked `test.fail()`).

"ok" on table pages means the **page** does not scroll sideways; the wide Material tables
(e.g. `/portfolio` is ~4,300px wide, `/watchlist` ~3,400px) scroll inside their own wrapper. That is
the current design, not a card/stacked layout — the refactor may want a stricter check later.

## Offenders on the failing pages

| Route       | Widest offending elements (width)                                                                                                                 | Likely cause                                                     |
| ----------- | ------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------- |
| /dashboard  | pixel: `mat-card.db-panel` 560px, `div.db-movers-cols` 560px, `div.db-movers-col` 283px. iPhone: signals table `th` / `td.st-act.act-watch` 183px | Movers two-column panel and the signals table are not responsive |
| /scanner    | `div.stat-tile.oversold-tile` / `overbought-tile` 414px (pixel), 395px (iPhone)                                                                   | Stat tiles wider than the viewport minus padding                 |
| /allocation | `div.sector-col-headers-right` 380px, `span.col-hdr-glpct` 64px                                                                                   | Fixed-width sector header columns                                |
| /config     | `div.sticky-save-bar-actions` 430px with a 173px button pushing to 617px                                                                          | Sticky save bar actions do not wrap                              |

## Environment notes

- Tablet (820px) shows the desktop toolbar or the hamburger depending on the breakpoint; the
  navigation spec accepts either and exercises whichever is visible.
- `/transactions` only lists OPEN rows opened after the hard-coded `OPEN_DATE_THRESHOLD`
  (`2026-06-01`, `transactions-page.component.ts`); fixtures use relative dates so the Open and
  Closed tabs stay populated until that constant is revisited.
- Watchlist defaults to the **Active** tier tab; six fixture items are Active so the grid has rows.

## Unmocked endpoints

Endpoints that hit the generic fallback during the baseline run (all fire-and-forget writes issued
on page load). They now have explicit handlers, so `mocks.unmocked` is empty for every route:

- `PATCH /api/portfolio/final-actions` (Portfolio publishes Final Actions on recompute)
- `POST /api/portfoliovaluehistory/backfill` (Portfolio page)
- `PUT /api/users/preferences/pm_app_config` (Config page write-through)

No requests to non-`/api` hosts are made other than Google Fonts (Roboto + Material Icons), which
the harness passes through to the CDN (and stubs when `E2E_STUB_FONTS=1`).
