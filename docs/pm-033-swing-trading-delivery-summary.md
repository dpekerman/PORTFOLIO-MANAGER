# PM-033 — Swing Trading Management Workspace: Delivery Summary

Branch: `feature/pm-033-swing-trading-workspace` (created from `develop`). The original delivery is already in existing commits, including `7163706`; **the remediation remains uncommitted and unpushed**.

> **REMEDIATION STATUS:** See [the remediation and recovery report](pm-033-remediation-recovery-report.md) for current test results, backup evidence, the twelve-finding matrix and restart gates. The original migration/import below are historical operations; do not rerun them. The single corrective migration has been tested on an isolated restore only and is **not applied to `PortfolioManagerLocal`**. Development startup now refuses while that corrective migration is pending. PM-033 remains In Progress pending live-migration approval and manual UAT.

> **STATUS UPDATE (2026-10-28, after your approval):** G1 and G2 have been **applied to `PortfolioManagerLocal`**.
> Fresh verified backup: `D:\PORTFOLIO-MANAGER-SQL-BACKUP-ALL\PortfolioManagerLocal_PrePM033_Apply_*.bak`. Migration `20261028145833` applied; episode backfill applied
> (80 episodes, 82 lots linked, 48 left unresolved); the 8 stops imported (Stop Market, user-confirmed, unverified, covered quantity NULL; 8 audit events).
> Row counts unchanged (PortfolioItems 130, CashItems 47, OptionItems 30). The API was restarted on this branch. Sections 6 and 7 below describe what was done and remain valid as the record.
> Rollback now = restore the backup (the guarded rollback script refuses once data exists).

> Note: `Program.cs` calls `Database.MigrateAsync()` on startup, so any other environment (e.g. Azure) that runs this branch's API will apply the migration automatically.

## 1. What was delivered

| Area | Delivered |
|---|---|
| Swing Trading page | New `/swing-trading` route and nav item with **Open Trades**, **Risk Monitor**, **Trade Journal** tabs, 4 summary metrics, as-of/stale indicators, no Stop Downside KPI |
| Manage Risk dialog | One shared dialog (`shared/manage-risk-dialog`) opened from Swing Trading (all tabs) and from the Portfolio grid (shield icon on OPEN stock rows) |
| Risk plan vs broker stop | Planned stop/target (`RiskPlan`) and user-recorded broker orders (`BrokerStopRecord`) are separate; a mismatch banner shows both; saving a plan never changes a broker record |
| Audit | Append-only `RiskAuditEvent` (old/new values, user, time, reason) mirrored to the existing `AuditLog` (category `Risk`) in the **same `SaveChanges`** |
| Trade episodes | Persistent `TradeEpisodeId` on `PortfolioItems` + `TradeEpisode` table; assigned on new trades, propagated on partial-close splits, closed on full exit, new episode on reopen |
| Stop lifecycle | Additions/partial sales never change recorded covered quantity; full exit retires orders as `EpisodeClosed` + "broker cancellation unverified" reminder; reopen never inherits old orders |
| Risk evaluation | Pure `StopRiskEvaluator`: breach / approaching (advisory 5%) / missing plan / no broker stop / partial / unknown / ambiguous / over-covered / mismatch / stale; severity ordering |
| Initial 8 stops | Previously imported; preserved with their identities, prices, import keys and audit history. No remediation reimport. |
| Migration | Original migration applied historically; one corrective migration tested on an isolated restore, pending on the real database. |
| Exports | CSV + Excel for Open Trades, Risk Monitor, Trade Journal (filtered/sorted rows; unknown coverage stays blank) |
| Tests | Backend, Angular unit, mocked Playwright on 4 projects, opt-in scratch-DB integration test, read-only live Playwright suite |

Unchanged by design: Final Action engine/thresholds (CLS.TO / BRK.TO parity tests pass), Strategic/Core policies, all financial formulas, existing transaction data, background services, Yahoo throttling.

The Swing Trading **Refresh** button runs the existing app-wide quote refresh (bypassing the quote cache), then reloads Open Trades / Risk Monitor and the Trade Journal if it has been opened. It shows a busy label and rotating icon, prevents duplicate clicks, and retains a successful completion time or an explicit error. The completion time is separate from the market-data as-of time; unchanged provider prices or timestamps are not presented as new market evidence.

## 2. Where to look (files)

**Backend** (`backend/PortfolioManager.Api`)
- `Models/RiskModels.cs` — `TradeEpisode`, `RiskPlan`, `BrokerStopRecord`, `RiskAuditEvent`, status constants
- `Services/Risk/TradeEpisodeResolver.cs` — episode derivation, unresolved rules, backfill planning, Days Held
- `Services/Risk/StopRiskEvaluator.cs` — risk classification + `PriceEvidence` (EOD/Live/Stale/Unavailable)
- `Services/Risk/RiskService.cs` — plan / broker-stop CRUD, transitions, validation, concurrency, backfill
- `Services/Risk/StopSheetImportService.cs` — initial stop import (dry run by default)
- `Services/Risk/SwingTradingService.cs` — Open Trades, Risk Monitor data, Trade Journal
- `Services/Risk/RiskSupport.cs` — atomic audit staging, `TradeEpisodeAssigner` (hooks), currency resolution
- `Controllers/RiskController.cs` — `api/risk/*` and `api/swing-trading/*`
- Edited: `Services/PortfolioService.cs` (episode assignment/propagation/retirement hooks), `Data/AppDbContext.cs`, `Models/PortfolioItem.cs` (+`TradeEpisodeId`), `Models/StockQuote.cs` + Yahoo mapping (+`Currency`), `Models/AuditLog.cs` (+`Risk` category), `Program.cs` (DI), `appsettings.json` (`Risk:ApproachingStopThresholdPct`)
- Migration: `Data/Migrations/20261028145833_AddTradeEpisodesAndRiskTracking.cs`
- SQL: `database/SQL/27_AddTradeEpisodesAndRiskTracking.sql`, `database/SQL/27_Rollback_TradeEpisodesAndRiskTracking.sql`

**Frontend** (`frontend/portfolio-manager-ui/src/app`)
- `features/swing-trading/` — page, `open-trades-tab`, `risk-monitor-tab`, `trade-journal-tab`, `journal-details-dialog`, `swing-filter-bar`, `swing-trading-logic.ts` (filters, validation, coverage, export)
- `shared/manage-risk-dialog/` — the shared dialog (Angular 22 **Signal Forms**)
- `core/store/swing-trading.store.ts`, `core/services/swing-trading-api.service.ts`, `core/models/swing-trading.models.ts`
- Edited: `app.routes.ts`, `shared/layout/layout.component.ts` (nav), `core/store/grid-registry.ts` (3 grids), `features/portfolio/portfolio-page.component.{ts,html}` (Manage Risk button)

**Tests**
- `backend/PortfolioManager.Tests/Pm033RiskTests.cs` (34 tests), `Pm033ScratchDbTests.cs` (opt-in)
- `src/app/features/swing-trading/swing-trading-logic.spec.ts`, `src/app/core/store/swing-trading.store.spec.ts`, `src/app/shared/manage-risk-dialog/manage-risk-dialog.component.spec.ts`
- `e2e/swing-trading.spec.ts`, `e2e/fixtures/swing.ts` (+ `/swing-trading` added to `e2e/support/routes.ts`, so the generic navigation/table/overflow specs cover it)
- Live read-only: `playwright.live.config.ts`, `e2e-live/swing-trading.live.spec.ts`

## 3. Key design decisions (as approved)

- **Stop scope = owner + account + symbol**, with historical episode attribution. The actual broker order is not exclusively a Swing-lot order. Closing the last Swing lot while other roles remain preserves the order; old orders are visible as reminders but never inherited as protection by a new Swing episode.
- **Mixed roles:** Open Trades shows Swing quantity and total account quantity separately; coverage becomes **Ambiguous** and is never allocated to Swing only.
- **Episode integrity:** an episode is only inferred when unambiguous (zero-open gap or an unbroken same-role lot chain). A same-day/same-date remainder, an overlapping CLOSE, a CLOSE in another account, or mixed roles ⇒ **Unresolved** (shown, flagged, never guessed from dates alone). Unresolved positions stay fully manageable (TA.TO, WELL.TO).
- **Broker status honesty:** user-confirmed ≠ verified. `Fully Protected` is only reachable for *verified + fully covered* orders, which cannot occur in this release. `CoveredQuantity = NULL` means unknown (never 0, never full). Cancel/Fill/Replace are explicit user reports, never inferred from price.
- **Breach wording:** "Price at/below recorded stop as of <date> (EOD) … a market observation, not proof a broker order triggered or filled. Verify with your broker."
- **Freshness:** reuses the existing trading calendar, including its evidence grace. Live/EOD evidence requires Yahoo's regular-price source timestamp and session checks. Capture-only legacy snapshots are *Stale*, not fabricated completed closes; missing quotes are *Unavailable*.
- **Approaching-stop threshold:** 5%, configurable (`Risk:ApproachingStopThresholdPct`), labelled everywhere *"advisory alert threshold, not a broker order level"* and written into exports.
- **Currency:** provider-reported currency (new `StockQuote.Currency`, populated from the Yahoo quote you already fetch) wins; otherwise a labelled suffix fallback. The analysis-security (DIS/NVDA US underlyings) is never used as the trading currency.
- **Concurrency/atomicity:** `RowVersion` on plans and broker records, 409 with "Reload latest" in the dialog; record + audit event commit together.
- **Import idempotency:** `ImportKey = sheetId:sheetVersion:row` (unique filtered index) + per-order `OrderIdentity`; two genuine identical orders are kept; same key with a different price is a **conflict**, never a silent update.
- **Legacy NULL-owner rows:** never rewritten. Reads follow the existing `PortfolioService.OwnedItems()` rule; risk records are owned by the user who wrote them (admins see all).
- **Angular 22 forms:** verified `@angular/forms/signals` `form()` + `[formField]` works with `matInput`, `mat-select` and `mat-checkbox` on 22.2.2 (13 component tests). Note: the `maxlength` attribute is not allowed on `[formField]` nodes (server enforces lengths).

## 4. Test results (all run on this branch)

| Suite | Result |
|---|---|
| Backend `dotnet test` (whole suite incl. Final Action parity) | **843 passed**, 3 skipped, 0 failed |
| PM-033 backend tests (`Pm033RiskTests`) | 34 passed — episodes, backfill, evaluator, import (8 real rows), idempotency, conflicts, lifecycle, audit, concurrency, journal, DI, no-broker-dependency |
| Scratch SQL Server integration (`Pm033ScratchDbTests`, restored copy) | passed — dry runs, apply, re-apply no-op, real SQL row-version conflict, financial columns unchanged |
| Angular unit (`ng test`) | **474 passed** (54 files) — new: logic 19, store 12, dialog 13 |
| Playwright (mocked API, desktop-chrome / tablet / mobile-s21 / mobile-s23), full suite | **749 passed**, 139 skipped (project-specific), 0 failed |
| Angular production build | succeeds (only pre-existing SCSS budget warnings) |

Playwright covers: navigation/tabs, filters/sorting, Manage Risk dialogs, broker-stop status, partial/unknown coverage, breach + stale evidence, save/edit/audit history, conflict, previous-trade orders, journal (episodes not merged), export contents, phone layout/touch targets, keyboard (Enter/Escape), Portfolio entry point. All mutations use isolated in-memory mock data.

## 5. How to test

### Automated
```powershell
# Backend (stop the running API first, or build to a separate output dir as done here)
cd backend\PortfolioManager.Tests; dotnet test

# Angular unit tests
cd frontend\portfolio-manager-ui; npx ng test --watch=false

# Mocked Playwright (own dev server; use another port if 4200 is your HTTPS dev server)
$env:E2E_PORT = '4300'; npx playwright test swing-trading
$env:E2E_PORT = '4300'; npx playwright test            # full suite, 4 projects

# Optional scratch-DB integration test (scratch copy only; name must contain PM033Test)
$env:PM033_SCRATCH_CONNECTION = 'Server=localhost;Database=PortfolioManagerPM033Test;Trusted_Connection=True;TrustServerCertificate=True'
dotnet test --filter Pm033ScratchDbTests
```

### Live HTTPS, read-only (run from a separate terminal; **after G1** and with the backend running)
```powershell
cd frontend\portfolio-manager-ui
$env:PM_LIVE_URL      = 'https://localhost:4200'
$env:PM_LIVE_EMAIL    = '<your email>'
$env:PM_LIVE_PASSWORD = '<your password>'
npx playwright test -c playwright.live.config.ts            # desktop, tablet, S21, S23
npx playwright show-report playwright-report-live
```
Credentials are only read from the environment. The live suite only navigates, filters, opens/closes dialogs and takes screenshots (`test-results-live/`); it never saves a plan or stop.

### Manual UAT checklist
1. Nav shows **Swing Trading** (desktop top bar and phone drawer); page loads with 4 tiles, an as-of time and "EOD-based: no intraday monitoring".
2. Open Trades lists only Swing positions; Swing qty vs account total shown for mixed-role holdings; Days Held matches the episode open date (additions don't reset it).
3. Filter by account, search, protection, risk status, Final Action, "needs attention"; Clear filters; sort by header (phones: sort control above the cards).
4. Final Action shown equals the Portfolio grid's Final Action for the same holding (click it for the existing details dialog).
5. Risk Monitor: breaches first; banner says *observation, not execution*; stale/no-price rows show no breach; 5% note says advisory only.
6. Manage Risk (Swing and Portfolio): change the planned stop → audit entry, broker stop unchanged, mismatch banner appears; try -5, 0, 4+ decimals (errors), a stop ≥ price (warning), covered qty > holding (blocked until "unusual case" + reason).
7. Record a broker stop with blank covered quantity → coverage **Unknown**; with partial quantity → **Partially Covered**; never "Fully Protected".
8. "It filled"/"I cancelled it"/"Replaced" record a user report and move the order out of the active list.
9. Open the same dialog in two tabs, save in one, save in the other → conflict banner + **Reload latest**.
10. Close a Swing position completely (Transactions/Portfolio edit) → its broker stop shows as retired with the "check at your broker" warning; re-open the security → no stop/plan is inherited.
11. Trade Journal: closed trades per episode (partial exits grouped, separate episodes not merged, unlinked CLOSE records separate), Details dialog shows thesis + stop/target history; Realized P/L = (closing − avg cost) × shares as on Transactions.
12. Export CSV/Excel from each tab: only the filtered rows; unknown coverage is blank with "Unknown" in *Coverage Status*; demo mode disables export.
13. Phones (S21 384px, S23 360px) and Pixel Tablet: no horizontal page scroll, Stop/Price/Protection/Manage Risk visible on each card, 44px targets, dialog fits width.

## 6. Gate G1 — database migration package (NOT applied to `PortfolioManagerLocal`)

Done so far (no change to the live database):
- Full **copy-only, checksum** backup: `D:\PORTFOLIO-MANAGER-SQL-BACKUP-ALL\PortfolioManagerLocal_PrePM033_20261028-105941.bak` (27.2 MB) — `RESTORE VERIFYONLY … WITH CHECKSUM` valid.
- Restore feasibility proven: restored to isolated `PortfolioManagerPM033Test`; row counts identical (PortfolioItems 130, CashItems 47, OptionItems 30, AuditLogs 155, migrations 53).
- On the scratch copy: forward script applied (4 tables + `PortfolioItems.TradeEpisodeId` + index), re-run is a no-op, guarded rollback **refused** when data exists and **succeeded** when tables were empty, then forward re-applied. `PortfolioItems` count stayed 130.
- Live DB re-checked at the end: no PM-033 objects, 130 items, last migration `AddRecommendationLifecycle`.

Schema change (additive only): new tables `TradeEpisodes`, `RiskPlans`, `BrokerStopRecords`, `RiskAuditEvents`; one nullable column `PortfolioItems.TradeEpisodeId` + index. **No existing value, date, quantity or formula changes.** (This corrects the earlier "PortfolioItems untouched" wording.)

G1 was applied historically. The former deployment/backfill commands are superseded, not instructions to rerun them.
For remediation, use only the separately approved corrective migration procedure in the [recovery report](pm-033-remediation-recovery-report.md).
Do not use API startup as migration approval, overwrite a backup, rerun backfill, downgrade EF directly or drop an existing scratch database without explicit authorization.
The original and corrective guarded rollback SQL scripts refuse populated PM-033 data; populated recovery requires the verified native backup.

## 7. Gate G2 — historical initial stop import mapping (already imported; do not rerun)

Source: your stop sheet; quantities/accounts from the read-only audit (restored-copy dry run of `StopSheetImportService`). All rows: **Stop Market · User Confirmed — Established · source "user-supplied stop sheet" · broker verification Unverified · Covered Quantity NULL · Coverage Unknown**. Quantities below are *holdings*, not proven order quantities.

| # | Symbol | Account | Swing qty | Account total | Currency | Episode (current trade) | Stop | Existing stop | Action |
|---|---|---|---|---|---|---|---|---|---|
| 1 | DIS.TO | TFSA_L_TD | 1,500 | 1,500 | CAD | Derived · open 2026-08-13 | 11.85 | none | Import |
| 2 | NVDA.TO | Corp_TD | 400 | 400 | CAD | Derived · 2026-08-27 | 46.50 | none | Import |
| 3 | NOVO.TO | TFSA_D_TD | 600 | 600 | CAD | Derived · 2026-10-15 | 16.20 | none | Import |
| 4 | EMP-A.TO | TFSA_L_TD | 200 | 200 | CAD | Derived · 2026-07-21 | 48.25 | none | Import |
| 5 | EFR.TO | TFSA_D_TD | 1,000 | 1,000 | CAD | Derived · 2026-10-15 (earlier trade closed 07-23 is a separate episode) | 10.75 | none | Import |
| 6 | TA.TO | Corp_TD | 1,000 | 1,000 | CAD | **Unresolved** (CLOSE #541 is in Margin_L_TD) — attaches to the current open position | 16.75 | none | Import (flagged) |
| 7 | WELL.TO | Corp_TD | 2,000 | 2,000 | CAD | **Unresolved** (1-share same-day CLOSE #637 not linked, no fictitious episode) | 3.95 | none | Import (flagged) |
| 8 | MAL.TO | TFSA_D_TD | 200 | 200 | CAD | Derived · 2026-10-26 (older Corp_TD history ignored) | 32.40 | none | Import |

Currency basis shows `AssumedFromSuffix` until the next quote refresh stores the provider currency; the sheet currency (CAD) matches in all 8 rows. Dry-run totals: 8 to import, 0 skipped, 0 blocked. JSON: `artifacts\pm033-reports\g2-stop-import-dry-run.json`.

Historical G2 request shape (retained for traceability, **not an instruction to reimport**): `POST /api/risk/import-stop-sheet` with
```json
{ "sheetId": "user-stop-sheet", "sheetVersion": "2026-10-28", "dryRun": true,
  "rows": [
    {"symbol":"DIS.TO","triggerPrice":11.85,"orderType":"StopMarket","currency":"CAD","rowNumber":1},
    {"symbol":"NVDA.TO","triggerPrice":46.50,"orderType":"StopMarket","currency":"CAD","rowNumber":2},
    {"symbol":"NOVO.TO","triggerPrice":16.20,"orderType":"StopMarket","currency":"CAD","rowNumber":3},
    {"symbol":"EMP-A.TO","triggerPrice":48.25,"orderType":"StopMarket","currency":"CAD","rowNumber":4},
    {"symbol":"EFR.TO","triggerPrice":10.75,"orderType":"StopMarket","currency":"CAD","rowNumber":5},
    {"symbol":"TA.TO","triggerPrice":16.75,"orderType":"StopMarket","currency":"CAD","rowNumber":6},
    {"symbol":"WELL.TO","triggerPrice":3.95,"orderType":"StopMarket","currency":"CAD","rowNumber":7},
    {"symbol":"MAL.TO","triggerPrice":32.40,"orderType":"StopMarket","currency":"CAD","rowNumber":8} ] }
```
The eight original records already exist. Remediation does not execute this request or infer fresh order quantities/statuses.

## 8. Known limitations / follow-ups

- "Days until/after stop breach" is not shown: only the observation date of the latest price is reliable without intraday history (no fabricated monitoring).
- Journal does not show the Final Action recorded at close (it is not stored per trade today).
- Episode hooks cover add/edit/partial-close/full-close. Protected lot deletion, ambiguous history edits, and destructive application-level replacement restore are now refused explicitly. Manual positions remain outside PM-033.
- Trailing stops are stored/displayed as a label only; no ratcheting math.
- Provider currency appears only after the next quote refresh (snapshots written before this change lack it); until then the suffix fallback is used and labelled.
- Risk records are owner-scoped; a non-admin user will not see risk records written by another user.
- Both dialog entry points load the same authoritative API facts, explicitly separating account and Swing values. Demo mode is masked/read-only and exports are disabled.
- No automated axe/screen-reader audit was run; semantics were verified by role/label/keyboard tests only (dialog labelled, `role=alert` errors, aria-live status, tab roles, 44px targets).
- The pre-existing e2e TypeScript project has Node-typing errors (`node:fs`) unrelated to this work; Playwright itself runs fine.
- Production build keeps the repo's existing SCSS budget warnings; none come from PM-033 files.
- Extra non-PM-033 edits: `playwright.config.ts` now honours `E2E_PORT` / `E2E_BASE_URL` (so mocked runs don't collide with the HTTPS dev server on 4200).
