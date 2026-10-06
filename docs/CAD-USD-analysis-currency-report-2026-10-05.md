# CAD / USD Analysis Currency – Implementation & Verification Report

Date: 2026-10-05 · Database: `PortfolioManagerLocal` (SQL Server 2019 Express)

## 1. Problem

`analysisTicker` / `analysisCurrency` were added to the models and `DailySignals`, but:

- Historical `DailySignals` rows had `NULL` ticker/currency (88 of 88 rows).
- New EOD signals only stored the metadata for underlying-mapped (CDR) symbols.
- Most screens showed prices as bare `$` or the default `currency` pipe, so CAD vs USD was not visible
  (e.g. a CDR such as `SPGI.TO` is quoted in CAD but *analyzed* in USD on `SPGI`).
- Manual SQL deployment masters did not include the new column step.

Decision (agreed): **no FX conversion**. Per-security prices are labelled with their native / analysis
currency; portfolio-wide aggregates keep their existing calculations.

## 2. Currency rules

| Value type | Source of currency |
|---|---|
| Traded-instrument quote, avg cost, shares value, options, transactions | symbol: `.TO` → CAD, otherwise USD (`currencyCodeForTradingSymbol`) |
| Analysis values (scanner price, SMA200, stop, Fibonacci, analyst target, EOD, leadership, priority candidates) | explicit `analysisCurrency` (CAD/USD) else symbol rule (`currencyCodeForAnalysis`) |

## 3. Database

**Backup (taken before any change, verified with `RESTORE VERIFYONLY ... WITH CHECKSUM`):**
`D:\PORTFOLIO-MANAGER-SQL-BACKUP-ALL\CAD-USD changes\PortfolioManagerLocal_PRE-CAD-USD_20261005-095755.bak`

| Step | Result |
|---|---|
| EF migration `20261002140402_AddAnalysisCurrencyToDailySignals` | already applied (columns existed, API reports "database is up to date") |
| `database/SCRIPTS/19_AddAnalysisCurrencyToDailySignals.sql` (idempotent backfill) | **88 rows updated** (CAD 65, USD 23); `NULL` rows remaining: **0** |
| Re-run of script 19 | 0 rows affected (idempotent) |
| Row counts of all tables before vs after | identical (no data loss) |

Backfill uses `COALESCE`, only fills missing values, and only joins global (`UserId IS NULL`) *Resolved* mappings;
rows analysed through a mapped underlying get the underlying ticker + `USD`, others `.TO`→CAD / else USD.
Script 19 is also step 10 in both `database/SCRIPTS/00_MASTER_DeployProduction.sql` and
`database/SQL/00_MASTER_DeployProduction.sql`.

**Restore if needed:** restore the `.bak` above over `PortfolioManagerLocal` (stop the API first).

## 4. Backend changes

- `EodSignalPersistenceService` – always stores `AnalysisTicker` / `AnalysisCurrency` (native CAD/USD included, `.TO`/USD fallback).
- `PortfolioActionScoreService` – `ActionScoreDto` now carries `AnalysisCurrency` (dashboard priority-candidate price is the *analysis* price).
- `ScannerModels`, `EmailNotificationService` – morning-check records carry ticker/currency; email suffix uses real currency.
- Tests: `EodSignalAnalysisCurrencyTests` (native RY.TO, USD fallback, mapped underlying) – **277/277 backend tests pass**.

## 5. Frontend changes

- `core/technical-display.ts` – `currencyCodeForTradingSymbol` / `currencyCodeForAnalysis` (null-safe) + new `technical-display.spec.ts`.
- `core/price-structure-display.ts` – tooltip money values show currency.
- Currency labels added on: Portfolio (table + card view, group rows, Fibonacci, options), Watchlist (table + card),
  RSI Scanner (price, SMA200, stop, Fibonacci, analyst target, morning panel, export), EOD Signals (entry/stop/risk/price/last/diff, export),
  Transactions (stocks + options), Allocation (position rows), Value Screener, Dashboard (priority candidates, market leadership).

## 6. Bugs found during this re-verification and fixed

| # | Found by | Issue | Fix |
|---|---|---|---|
| 1 | Playwright console on `/transactions` | `TypeError: Cannot read properties of undefined (reading 'trim')` – option rows used `a.item.symbol` (options only have `underlyingTicker`) | use `underlyingTicker`; helpers made null-safe; unit test added |
| 2 | Playwright table scan | Portfolio grouped rows still showed `DAY $` and `GAIN/LOSS` as bare `$` | now use the holding currency |
| 3 | Code review | Missing `analysisCurrency(row)` method in scanner table (compile blocker) | added; build passes |
| 4 | Code review | Dashboard price used trading-symbol currency but value is analysis price | carried `AnalysisCurrency` through action-score API |

## 7. Verification results

| Check | Result |
|---|---|
| `ng build` (production) | pass (only pre-existing SCSS budget warnings) |
| Frontend unit tests (vitest) | **135 / 135** |
| Backend tests | **277 / 277** |
| `git diff --check` | clean |
| Playwright – 10 screens (Dashboard, Portfolio, Watchlist, Scanner, EOD, Transactions, Allocation, Value Screener, Value History, Config) | **0 console/page errors** after fixes |
| Playwright – row-cell scan for `$` amounts without CAD/USD | none on Watchlist, Scanner, EOD, Transactions, Value Screener; Portfolio remaining = cash-ledger (aggregate) only |

Observed examples: Scanner `BIDU USD85.71`, `MTY.TO CAD31.13`, `SPGI.TO USD386.27` (CDR analyzed on SPGI);
EOD `$51.88 CAD`, `ROL $30.59 USD`; Portfolio card `BRK.TO CAD36.30`, Gain/Loss `-CAD28.00`;
Transactions option `CAD50.00 / CAD2.32`; Allocation position `$61.20 CAD`; Value Screener `SOXS.NE USD6.21`.

## 8. How to verify manually

1. API: `cd backend\PortfolioManager.Api; dotnet run --launch-profile http`; UI: `cd frontend\portfolio-manager-ui; npx ng serve`.
2. SQL: `SELECT AnalysisCurrency, COUNT(*) FROM DailySignals GROUP BY AnalysisCurrency;` → no NULLs.
3. Scanner: CDR rows (e.g. `SPGI.TO`) show price/SMA200/Fib in **USD**; `.TO` rows in **CAD**; open the Morning Check panel → price has currency.
4. EOD Signals: every price column ends with CAD/USD; export contains Analysis Ticker/Currency.
5. Portfolio: table + card view show per-position CAD/USD; options rows show currency; browser console is clean.
6. Transactions → Stocks and Options tabs; Allocation → *Expand All*; Watchlist; Value Screener; Dashboard widgets.
7. Tests: `dotnet test backend\PortfolioManager.Tests` and `cd frontend\portfolio-manager-ui; npm test -- --watch=false`.

## 9. Known limitations (intentional / out of scope)

- No FX conversion: portfolio totals, sector/allocation totals, history and cash amounts are existing unconverted aggregates.
- Currency rule is `.TO` = CAD, everything else USD (matches `SecurityAnalysisResolver`). Other Canadian suffixes
  (`.NE`, `.V`, `.CN`) are shown as USD – a data-driven currency from Yahoo would be needed to improve this.
- Angular's `code` display renders `CAD36.30` (no space); only cosmetic.
- User-specific mappings cannot be reconstructed for historical EOD rows (no user id on `DailySignals`).
- Prettier `--check` flags nearly every file in the repo (pre-existing line-ending/config baseline), so no reformatting was applied.
- The API and `ng serve` dev servers were restarted for testing and are currently running.

---

## 10. Update 2026-10-06 – Currency moved from number text to a "Currency" column

Business request: no CAD/USD prefix/suffix on numbers. Display-only change; no calculation, API or database change.

| Area | Change |
|---|---|
| Dashboard widgets (priority candidates, market leadership) | plain numbers (`$`), no CAD/USD text |
| Grids | new **Currency** column (registered in `grid-column.service.ts`, user can hide/reorder): Portfolio stocks + options, Transactions stocks + options, Watchlist, RSI Scanner, EOD Signals, Value Screener, Allocation position rows |
| Which currency | Scanner / EOD = analysis currency; all others = trading currency (`.TO` = CAD, else USD) |
| Cards, tooltips, Morning Check panel | plain numbers (no currency text) |
| Emails (EOD and others) | reverted to original plain numbers (`EmailNotificationService.cs` restored to HEAD) |
| Exports | unchanged (they already carry separate currency columns) |

Verification: frontend tests 135/135, production build passes, Playwright on all main screens: 0 console errors,
Currency column present with CAD/USD values, numbers shown as plain `$x.xx`.
Backend tests could not be re-run in this session because the API was running (DLL locked); only the email file was reverted to its committed state.
