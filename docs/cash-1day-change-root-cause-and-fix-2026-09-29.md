# Cash vs Stocks "1 Day Change" — Root Cause, Fix, and Test Guide

**Date:** 2026-09-29
**Status:** Implemented and verified (backend 261/261 tests pass, Angular build passes, migration applied to the local database).
**Safety backup (taken before any change):** `D:\PORTFOLIO-MANAGER-SQL-BACKUP-ALL\CASH-DISCREPANCY\20260929-110605\PortfolioManagerLocal_FULL.bak` (verified with `RESTORE VERIFYONLY ... WITH CHECKSUM`).

---

## 1. Summary

The 1 Day Change on **Dashboard** and **Portfolio** was wrong on any day a stock or option was bought or sold. Two independent defects combined:

| #   | Defect                                                                                                                                                               | Effect                                                                                                                                          |
| --- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| A   | Buying/selling only writes a `PortfolioItems`/`OptionItems` row. It never touches the Cash Ledger, so Cash does not move unless you type a separate cash entry.      | Cash and Portfolio Value are overstated after every buy (money counted as cash _and_ as the new position).                                      |
| B   | The **Stocks** part of 1 Day Change was `Σ shares × Quote.Change` (price movement of shares currently held). It ignores the value of a position opened/closed today. | Even when you _do_ add the cash entry, Cash shows −$X but Stocks never shows the matching +$X, so the headline is off by the full trade amount. |

**Fixing A alone would make B worse**, which is why both were fixed together:

- **A →** every buy/sell now offers a _Link cash_ dialog that writes the matching `TradePurchase` / `TradeProceeds` ledger row, tied to the trade in the database (unique per trade leg).
- **B →** Stocks change is now `live stocks value − last close's stocks value` (the same way Cash and Options were already computed), so **Stocks + Cash + Options = Total Value − yesterday's Total Value, always**.

### Worked example (today, from your screenshots and the database)

|           | Sep 28 close | Now (Portfolio page) |                                    Change |
| --------- | -----------: | -------------------: | ----------------------------------------: |
| Stocks    |   674,131.73 |              691,592 | **+17,460** (includes the new HNU.TO buy) |
| Cash      |    73,103.00 |               55,553 |              **−17,550** (manual row #52) |
| Options   |    34,260.00 |               34,260 |                                         0 |
| **Total** |   781,494.73 |           781,404.73 |                                **−90.00** |

- **Before the fix** the headline showed **−$25,083** (Stocks −7,533 price-move + Cash −17,550). The purchase was counted as a loss.
- **After the fix** the headline shows **≈ −$90**, which is the real change in portfolio value.

---

## 2. Why it started (timeline, from git + migrations)

| Date           | Event                                                                                                                                                                                        |
| -------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 2026-06-18     | Cash Ledger created as a table separate from Portfolio (defect A exists from day one, but nothing compared day-over-day yet).                                                                |
| 2026-07-19     | `PortfolioValueHistories` (daily snapshots) added — the first baseline to compute a "1 day change" against.                                                                                  |
| 2026-08-28     | Commit `6c9c547` "1 Day Change fixes": Stocks change = value delta (`live − yesterday`). This _was_ the correct formula. Defect A still made buys look like gains (stock up, cash not down). |
| 2026-09-04     | Commit `e67db9d` "not sure if this is the fix": formula experiments.                                                                                                                         |
| 2026-09-05     | Cash Ledger rebuilt as a strict typed ledger (`TradePurchase`, `TradeProceeds`, …) with opening balances — but still manual-entry only.                                                      |
| **2026-09-16** | Commit `3fb8f89` "bugs fixing": Stocks change switched to `Σ shares × Quote.Change` and the headline became the sum of components. This introduced defect B.                                 |
| 2026-09-26     | Draft plan "Fix B — Auto-link cash" written but never implemented.                                                                                                                           |

So you were partly right: **before 2026-07-19 there was no 1-day-change number at all**, and from 2026-08-28 to 2026-09-16 the _formula_ was sound (the remaining error was only the unlinked cash). Since 2026-09-16 both defects apply.

---

## 3. Options that were evaluated

| Option | Description                                                                         | Verdict                                                                                                                                             |
| ------ | ----------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1      | Warn/remind only (banner "remember to add cash")                                    | Human still does the work; numbers wrong until they do. Rejected.                                                                                   |
| 2      | Silently auto-create cash on every trade                                            | No extra click, but can double-count when you already add cash by hand (you do, e.g. rows #45, #49–52) and cannot handle fees/backdating. Rejected. |
| **3**  | **Confirm dialog that creates a DB-linked cash row + value-delta formula (chosen)** | Correct numbers, human stays in control, duplicate detection, permanent DB-level link.                                                              |
| 4      | Time-weighted-return style (exclude all trades/flows from "change")                 | Best for performance reporting, but hides real cash movement and is a large redesign. Listed as follow-up (see §8, D12).                            |

Decisions confirmed with you: confirm dialog, stocks **and** options, forward-only (no historical backfill), negative cash allowed with a warning, fix the formula too.

---

## 4. Applied changes

### 4.1 Database

New EF migration `20260929151645_AddCashSourceLink` (additive, all nullable, applied to `PortfolioManagerLocal`):

| Change                   | Detail                                                                                                                                                                                                           |
| ------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `CashItems.SourceType`   | `nvarchar(20) NULL` — `PortfolioOpen`, `PortfolioClose`, `OptionOpen`, `OptionClose`.                                                                                                                            |
| `CashItems.SourceItemId` | `int NULL` — id of the `PortfolioItems`/`OptionItems` row. Intentionally **not** a foreign key (the cash entry may outlive a deleted trade).                                                                     |
| `IX_CashItems_TradeLink` | **Unique filtered index** on (`SourceType`, `SourceItemId`) `WHERE SourceType IS NOT NULL`. A trade leg can never be linked to two cash rows, even under concurrent requests. Manual rows (NULL) are unaffected. |

All 18 existing cash rows keep `SourceType = NULL`. Rollback: `dotnet ef migrations remove` is not needed for a DB rollback — use `dotnet ef database update 20260909233823_AddAutomationRunDiagnostics`, or restore the `.bak` above.

### 4.2 Backend (.NET)

| File                                     | Change                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| ---------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Models/CashItem.cs`                     | Added `SourceType`, `SourceItemId`.                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| `Data/AppDbContext.cs`                   | Column length + `IX_CashItems_TradeLink` index.                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| `Models/Dtos.cs`                         | `CashItemDto` gained optional `SourceType`/`SourceItemId` (trailing optional params, so all existing callers compile unchanged). New `AddLinkedCashRequest`.                                                                                                                                                                                                                                                                                                                                                   |
| `Services/TradeLinkSourceTypes.cs` (new) | The four leg names and the rule _Open → TradePurchase, Close → TradeProceeds_.                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| `Services/CashService.cs`                | New `AddLinkedAsync` and `GetLinkedAsync`. `AddLinkedAsync` validates the source type, positive amount, that the trade exists and is visible to the caller, that it is **not a manual position**, and that the leg is not already linked. It then reuses the existing transactional `ReconcileAfterMutationAsync` (same-day reseal marking / backdated cash-only snapshot recalculation), so no new ledger logic was invented. Sign and `CashFlowType` are derived server-side; the client cannot choose them. |
| `Controllers/CashController.cs`          | `POST /api/cash/link` (Admin/Trader), `GET /api/cash/link/{sourceType}/{sourceItemId}` (204 when none). Existing `PUT`/`DELETE /api/cash/{id}` are reused for edit/remove.                                                                                                                                                                                                                                                                                                                                     |
| `Services/DashboardService.cs`           | `todayStocksChange = liveStocksValue − yesterdayEntry.StocksValue` (falls back to the old price-move figure only when there is no baseline row).                                                                                                                                                                                                                                                                                                                                                               |

Not touched: `PortfolioService`, `OptionService`, snapshot/EOD services, `CashLedgerQueryService`, EOD email/automation. Trades and cash stay decoupled in the services — the linking is a separate, explicit call (no circular DI risk, no change to bulk import/restore paths).

### 4.3 Frontend (Angular)

| File                                                                        | Change                                                                                                                                                                   |
| --------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `shared/link-cash-dialog/*` (new, ts/html/scss)                             | The **Link cash** dialog (see §5).                                                                                                                                       |
| `core/services/trade-cash-link.service.ts` (new)                            | Decides _when_ to offer/correct a cash row; builds amounts; duplicate detection; delete follow-up.                                                                       |
| `core/services/cash-state.service.ts`                                       | `addLinked()`, `removeItems()`.                                                                                                                                          |
| `core/services/portfolio-api.service.ts`, `core/models/portfolio.models.ts` | `addLinkedCash`, `getLinkedCash`, `TradeLinkSourceType`, `AddLinkedCashRequest`, `CashItem.sourceType/sourceItemId`.                                                     |
| `core/services/portfolio-state.service.ts`                                  | `addItem` now resolves with the created row (was `void`); `updateItem` and `deleteItem` call the link service.                                                           |
| `core/services/option-state.service.ts`                                     | Same hooks for options (`updateItem`, `deleteItem`).                                                                                                                     |
| `features/portfolio/add-stock-dialog`, `add-option-dialog`                  | Offer the link after a successful add. (Deliberately **not** inside `addItem`, because the bulk Import dialog calls it per row.)                                         |
| `features/portfolio/portfolio-summary-bar/*`                                | Stocks change = `stocks value − last close's stocks value`; baseline day is now chosen with **Eastern-time** dates (was UTC, see D13); tooltip explaining the breakdown. |
| `features/dashboard/dashboard-page.component.html`                          | Tooltip on the 1 Day Change breakdown.                                                                                                                                   |
| `features/portfolio/stock-card/stock-card.component.ts`                     | **Bug fix found on the way:** editing a position from the card view sent only some fields and the API overwrites the rest with `null` (see D14).                         |

### 4.4 Tests

New `backend/PortfolioManager.Tests/CashTradeLinkTests.cs` (17 cases): sign/type per leg for stocks and options; same leg twice rejected; open + close legs of one position both allowed; manual positions rejected; missing trade rejected; invalid source type / zero / negative amount rejected; lookup returns row or null; account total drops by exactly the trade amount; **backdated link patches only snapshot cash (stocks/options preserved)**; delete frees the leg for re-linking; manual rows stay unlinked and don't block linking; **SQLite test proving the unique index** (InMemory cannot enforce it).

Result: **261 passed, 0 failed** (full suite). Angular `ng build` passes.

---

## 5. New behaviour (what you will see)

**When the Link cash dialog appears**

| Action                                                                                                          | Dialog                                                                                                  |
| --------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| Add Stock (OPEN) / Add Option (OPEN)                                                                            | _Buy …_ — Cash out, default amount = shares × cost (options: contracts × premium × 100).                |
| Edit a position to **CLOSE** (full or partial close; from grid, card, or Transactions page)                     | _Sell …_ — Cash in, amount = closed shares × closing price. Partial close links only the closed shares. |
| Edit shares/price/account of a position that **already has a linked cash row** and the amount no longer matches | _Update linked cash_ — offers to correct the same row.                                                  |
| Delete a position that had linked cash                                                                          | Confirm: _Remove linked cash?_ (Keep / Remove cash).                                                    |

**It never appears for:** inline edits (decision source, market price), sector/role changes, positions that were never linked (all your existing ones), manual positions, or bulk Import.

**Dialog content:** trade summary with Cash out / Cash in chip; editable Amount (add fees here), Account, Cash date, Description; live _account balance before → after_; **duplicate warning** if a hand-entered row of the same direction/account/amount (±1% or $1) exists within ±1 day — the primary button becomes _Add anyway_, so use **Skip**; **negative-balance warning** (allowed); back-dating hint. **Skip** always keeps today's fully manual workflow. Demo mode masks the informational amounts.

**Cash date defaults to today**, even for back-dated trades — see D15.

---

## 6. How to test

Restart the API once (`start-backend.bat`) — the migration is already applied. The frontend dev server hot-reloads.

### 6.1 Automated

```powershell
cd backend\PortfolioManager.Tests
dotnet test                                   # expect 261 passed
dotnet test --filter "FullyQualifiedName~CashTradeLinkTests|FullyQualifiedName~CashLedgerTests"

cd ..\..\frontend\portfolio-manager-ui
npx ng build --configuration development      # expect "Application bundle generation complete"
```

### 6.2 Manual scenarios (Portfolio page → Add Stock / Edit)

Do these with a small test position (e.g. 1 share) so you can delete it afterwards; use **Refresh** on the Dashboard after each step (the Dashboard is a stored snapshot, see D6).

1. **Buy, link.** Add Stock OPEN, 10 sh @ $50, account TFSA_D_TD → dialog shows _Cash out $500.00_, balance before → after. Click _Add cash entry_.
   - Portfolio tile 1 Day Change moves by only the market move, not by −$500 or +$500. Breakdown: Stocks ≈ +$500, Cash −$500.
   - SQL: `SELECT TOP 1 * FROM CashItems ORDER BY Id DESC` → `TradePurchase`, `-500`, `SourceType=PortfolioOpen`, `SourceItemId` = the new position id.
2. **Buy, skip.** Repeat and click _Skip_ → no cash row; behaviour identical to before.
3. **Duplicate protection.** First add a manual cash row (Add Cash → TradePurchase, −$500, same account, today), then add the stock → the dialog shows the _similar manual entry already exists_ warning and _Add anyway_. Click _Skip_.
4. **Sell.** Edit the position → Transaction type CLOSE, closing price $55 → dialog _Cash in $550.00_. Accept → row `TradeProceeds +550`, `SourceType=PortfolioClose`.
5. **Partial close.** Buy 10 sh, then close 4 → dialog amount = 4 × closing price; remaining 6 sh appear as a new OPEN row without a link.
6. **Correct a linked amount.** On a linked OPEN position change shares 10 → 12 → dialog _Update linked cash_ with the new amount; accept → the same cash row is edited (no new row).
7. **No nagging.** Change only the decision source or sector of any old position → no dialog.
8. **Delete cascade.** Delete the linked test position → _Remove linked cash?_ → _Remove cash_ deletes the linked row; the account balance returns.
9. **Options.** Add Option OPEN 2 contracts @ $1.50 → _Cash out $300.00_ (×100 multiplier); close at $2.00 → _Cash in $400.00_.
10. **Negative balance.** Enter an amount larger than the account balance → orange warning, still allowed.
11. **Double-link guard (DB level).** In SSMS run the same `INSERT` twice with the same `SourceType`/`SourceItemId` → second fails on `IX_CashItems_TradeLink`.
12. **Formula identity.** On both Dashboard (after Refresh) and Portfolio: `Stocks + Cash + Options` in the breakdown = the headline = `Portfolio Value − last close's total`. Last close values: `SELECT TOP 2 RecordedDate, StocksValue, CashValue, OptionsValue, TotalValue FROM PortfolioValueHistories ORDER BY RecordedDate DESC`.

Clean up the test data afterwards (delete the test position — accept the cash removal prompt — and any manual test cash rows).

---

## 7. Rollback

1. **Code:** `git checkout -- <files>` / `git clean` for the new files (nothing is committed yet).
2. **Schema:** `cd backend\PortfolioManager.Api; dotnet ef database update 20260909233823_AddAutomationRunDiagnostics` then `dotnet ef migrations remove`.
3. **Full data restore (last resort):** restore `D:\PORTFOLIO-MANAGER-SQL-BACKUP-ALL\CASH-DISCREPANCY\20260929-110605\PortfolioManagerLocal_FULL.bak`.

No existing data was modified by this change; the only DB write was the additive migration.

---

## 8. Discrepancies found (please review — none were changed)

Data findings come from read-only queries on the live local database; "verify" means compare with the brokerage statement.

### Data (your numbers)

| ID     | Severity | Finding                                                                                                                                                                                                                                                                                                       | Suggested action                                                                                                                                 |
| ------ | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| **D1** | **High** | **Two option purchases on 2026-09-25 have no cash offset:** MDA.TO (15 × 2.32 × 100 = **$3,480**, Margin_D_TD) and K.TO (10 × 4.70 × 100 = **$4,700**, TFSA_D_TD). Total **$8,180** is counted as cash _and_ as option value → Portfolio Value overstated by ≈ $8,180 since Sep 25 (and Sep 25–28 snapshots). | Add two Cash → `TradePurchase` entries dated 2026-09-25 (they will auto-correct historical snapshot cash). Confirm the real debit amounts first. |
| **D2** | High     | Corp_TD: row #45 (−$21,297, MCD/BOFA offset, Sep 25) is almost cancelled by row #46 "Balance adjustment (AdjustmentIncrease) **+$21,254**" on Sep 27 → net −$43. Corp_TD cash is now $23,531.                                                                                                                 | Verify Corp_TD's real cash. If the adjustment was meant to match a deposit, classify it as `Deposit`; if it was a mistake, remove it.            |
| **D3** | Medium   | XNDU.TO sold Sep 27 for $706 (100 × 7.06). Rows #49 (−706 TradePurchase) and #50 (+706 TradeProceeds) net to **0**, so the sale proceeds never reached cash.                                                                                                                                                  | Verify; likely one of the two rows should not exist or the proceeds row should be separate.                                                      |
| **D4** | Medium   | TFSA_D_TD sells: ATH.TO $10,880 (Sep 11) + META.TO $8,867.50 (Sep 16) + META.TO $9,437.50 (Sep 21) = **$29,185**, but proceeds rows are +1, +8, +27,246 = **$27,255** → **$1,930** unexplained.                                                                                                               | Verify against statements (withdrawal, fees, or missing entry).                                                                                  |
| **D5** | Low      | HNU.TO buy: $17,540 (2000 × 8.77) vs manual cash −$17,550 → $10 difference (commission?).                                                                                                                                                                                                                     | Fine if fees; the new dialog lets you include fees deliberately.                                                                                 |

### System / logic

| ID      | Severity | Finding                                                                                                                                                                                                                                                                                                                                                                                                    | Status                                                                                                                                                                       |
| ------- | -------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **D6**  | High     | **Dashboard is a stored snapshot** (rebuilt on Refresh), Portfolio is live. This produced your screenshot mismatch: Dashboard $798,955 vs Portfolio $781,404 = **$17,551**, exactly the new cash row entered after the snapshot.                                                                                                                                                                           | Not changed. Click Dashboard **Refresh** after any trade/cash change. Follow-up: auto-rebuild after a link (needs a light rebuild path, because a full rebuild calls Yahoo). |
| **D7**  | High     | Stocks change was price-only (`Quote.Change`) while Total included new positions → headline off by the trade amount.                                                                                                                                                                                                                                                                                       | **Fixed** (backend + Portfolio).                                                                                                                                             |
| **D8**  | Low      | Baseline basis: the EOD snapshot stocks value (Yahoo price at ~4:30 PM ET) differs from `previousClose × shares` by ≈ $0.9k (0.13%) for the same positions. Today's residual −$936 in the reconciliation of the true change is this.                                                                                                                                                                       | Inherent to price-timing; not changed.                                                                                                                                       |
| **D9**  | Medium   | **No FX handling.** USD-listed tickers are valued 1:1 with CAD (only 1 active non-Canadian-suffix holding now, but it grows with US holdings). Dashboard formats in CAD, Portfolio tile in USD.                                                                                                                                                                                                            | Not changed; needs a currency decision.                                                                                                                                      |
| **D10** | Medium   | `PortfolioValueHistories` and several services (`PerformanceSummaryService`, `PortfolioBetaService`) sum **all** users' cash/positions, while Dashboard live values are per user (own + legacy). Both current users are Admin (see everything) so it currently matches; a Trader/Viewer would get inconsistent changes. Also 3 dashboard snapshots exist, one for a user id (`b62d49e7…`) with no account. | Not changed; flag before adding non-admin users.                                                                                                                             |
| **D11** | Low      | Live cash (Dashboard + Portfolio) sums **all** ledger rows including future-dated ones; history uses "as of date". No future-dated rows exist today.                                                                                                                                                                                                                                                       | Not changed.                                                                                                                                                                 |
| **D12** | Medium   | 1 Day Change includes external flows: a $10k **Deposit** shows as +$10k change (rows #47/#48 ±$100 net out today). `ExternalCashFlow` is already stored per day.                                                                                                                                                                                                                                           | Follow-up option: subtract Deposits/Withdrawals for a performance-only figure.                                                                                               |
| **D13** | Medium   | Portfolio tile picked "today" with a **UTC** date; after ~7–8 PM ET the UTC date flips to tomorrow and it used a different baseline than the Dashboard.                                                                                                                                                                                                                                                    | **Fixed** (Eastern dates).                                                                                                                                                   |
| **D14** | High     | Editing a position from the **card view** omitted transaction fields; the API overwrites omitted fields with `null` (`TransactionType`, account, dates, closing price), and a `null` type is counted as an active position. `portfolio-page.updateDecisionSource` has the same flaw but is currently unused.                                                                                               | Card view **fixed**. Follow-up: make the API ignore omitted fields.                                                                                                          |
| **D15** | Medium   | **Back-dated trades:** snapshots keep old stock values (positions have no history), so back-dating the cash row lowers past days' totals (a dip).                                                                                                                                                                                                                                                          | Dialog defaults the cash date to **today** and shows a hint when the trade date differs.                                                                                     |
| **D16** | Low      | Option values depend on the manually maintained `MarketPrice` (no live option quotes). The Options part of 1 Day Change only moves when you update it.                                                                                                                                                                                                                                                     | Not changed.                                                                                                                                                                 |
| **D17** | Low      | EOD snapshot falls back to `AverageCostBasis` when a Yahoo quote is missing, silently distorting that day's stocks value.                                                                                                                                                                                                                                                                                  | Not changed; consider logging/alerting.                                                                                                                                      |
| **D18** | Low      | Not linked by design: bulk Import, bulk multi-select delete, Cash Restore. `Restore` also drops `CashFlowType` (pre-existing). Generic edit of a linked cash row can change its type away from Purchase/Proceeds.                                                                                                                                                                                          | Documented.                                                                                                                                                                  |
| **D19** | Info     | Manual positions (`IsManual`) cannot be linked (no trade price). There are none in the database today.                                                                                                                                                                                                                                                                                                     | By design.                                                                                                                                                                   |

---

## 9. Not in scope / follow-ups (recommended order)

1. Fix **D1–D4** data (largest effect on the absolute Portfolio Value).
2. Auto-refresh the Dashboard snapshot after a link/cash change (D6).
3. Exclude Deposits/Withdrawals from 1 Day Change, or show them as a separate line (D12).
4. Make `PUT /api/portfolio/{id}` and `/api/options/{id}` ignore omitted fields (D14).
5. Decide the currency policy (D9) and per-user scoping of history (D10).

---

## 10. Operational note

The dev API process was stopped once to unlock the build output (required for `dotnet ef`) and was **restarted** afterwards (`dotnet run --launch-profile http`, port 5000). The Angular dev server was not touched.
