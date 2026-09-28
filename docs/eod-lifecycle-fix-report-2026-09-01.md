# EOD Lifecycle Fix Report

## Completed fixes

- EOD persistence now derives `TradingDate` from the completed OHLCV bar and stores scanner execution time separately as `ScannedAt`.
- The EOD database migration was applied locally: `20260901174524_AddEodTradingDateAndScanTimestamp`.
- Repeated persistence for the same ticker, scan type, event identity, and completed market session updates one semantic record; a filtered unique index enforces this identity.
- Concurrent duplicate-key writes are handled as an idempotent concurrent outcome instead of failing the scanner workflow.
- EOD list queries, filtering, exports, snapshot enrichment, and latest-session selection use `TradingDate` rather than scan date or insertion time.
- The EOD screen displays `Trading Sessions Passed`, calculated from completed Yahoo daily bars, so weekends and exchange holidays without a bar do not count.
- The admin-only `POST /api/eod-signals/repair-trading-dates` endpoint repairs legacy records with no `TradingDate` from historical OHLCV data and merges semantic duplicates while reporting unresolved symbols.
- Dashboard EOD statuses no longer derive `Reversal watch` locally. The visible Latest EOD Signals panel consumes canonical action results; a portfolio/watchlist symbol with no canonical action displays `Action unavailable` rather than an independent recommendation.
- An active hard structural negative now produces canonical `AVOID`, including the ATD.TO oversold/Bull Turn/tight-wedge-breakdown case.
- Price-structure copy distinguishes primary pattern from current decision level, makes role-flipped labels explicit, uses resistance-oriented Recent High language, and prevents hard blockers from granting entry permission.
- Scanner presentation labels RSI momentum as `RSI Trend Shift` rather than a trade action.

## Verification

- Backend test suite: 166 passed.
- Focused Angular price-structure suite: 45 passed.
- API and Angular production builds completed successfully.
- `git diff --check` completed successfully.

## Operational follow-up

Run the authenticated Admin repair endpoint once against the migrated database, then review its `unresolved` response before treating all legacy history as repaired. The remaining CPH.TO, MSFT, repair, idempotency, holiday-session, and role-language fixtures are tracked in `eod-lifecycle-regression-todos.md` for expansion into database-backed tests.
