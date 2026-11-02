# E2E baseline

Measured on the `feature/angular22-signalstore-mobile` branch before the mobile-first refactor,
with the fixtures in `e2e/fixtures` (every table populated). Overflow = the widest scroll root
(`html` or `mat-sidenav-content`) is wider than the viewport. See `support/overflow.ts` for why
`document.documentElement.scrollWidth` alone is not enough: the shell scrolls inside
`mat-sidenav-content`, so the document itself never overflows.

## Results

Smoke, navigation and tables: **green on all 4 projects**. Overflow (content width vs viewport):
the table below is the pre-refactor baseline measured on the since-retired Pixel 7 (412px) and
iPhone (393px) projects; the supported phones are now `mobile-s21` (384px) and `mobile-s23`
(360px), and the tablet is 800px.

| Route                    | desktop-chrome 1920 | tablet 800 | mobile-s21 384 | mobile-s23 360 |
| ------------------------ | ------------------- | ---------- | -------------- | -------------- |
| /login                   | ok                  | ok         | ok             | ok             |
| /dashboard               | ok                  | ok         | ok (was 572)   | ok (was 572)   |
| /portfolio               | ok                  | ok         | ok             | ok             |
| /watchlist               | ok                  | ok         | ok             | ok             |
| /scanner                 | ok                  | ok         | ok (was 430)   | ok (was 430)   |
| /eod-signals             | ok                  | ok         | ok             | ok             |
| /transactions            | ok                  | ok         | ok             | ok             |
| /allocation              | ok                  | ok         | ok (was 422)   | ok (was 422)   |
| /value-screener          | ok                  | ok         | ok             | ok             |
| /portfolio-value-history | ok                  | ok         | ok             | ok             |
| /config                  | ok                  | ok         | ok (was 617)   | ok (was 617)   |

The backlog set in `support/known-mobile-backlog.ts` is empty; the mechanism is kept for new pages.

"ok" means the **page** does not scroll sideways. On phones the Material grids render as stacked
cards (`responsive-cards*.spec.ts`); on tablet/desktop they remain the untouched wide tables that
scroll inside their own wrapper.

## Offenders found in the baseline (all fixed)

| Route       | Widest offending elements (width)                                                                                                                 | Fix                                                              |
| ----------- | ------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------- |
| /dashboard  | pixel: `mat-card.db-panel` 560px, `div.db-movers-cols` 560px, `div.db-movers-col` 283px. iPhone: signals table `th` / `td.st-act.act-watch` 183px | Movers stack, dense tables scroll inside their card              |
| /scanner    | `div.stat-tile.oversold-tile` / `overbought-tile` 414px (pixel), 395px (iPhone)                                                                   | `box-sizing` on the shared market-header tiles                   |
| /allocation | `div.sector-col-headers-right` 380px, `span.col-hdr-glpct` 64px                                                                                   | Handset metric grid under the sector/industry name               |
| /config     | `div.sticky-save-bar-actions` 430px with a 173px button pushing to 617px                                                                          | Static stacked save bar, scrolling tabs, tighter section padding |

## Environment notes

- Tablet (800px) shows the desktop toolbar or the hamburger depending on the breakpoint; the
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
