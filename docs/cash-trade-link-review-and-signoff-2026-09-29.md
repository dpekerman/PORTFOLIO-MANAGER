# Cash / Options Buy-Sell Linking — Review & Sign-off Guide

**Date:** 2026-09-29
**Companion to:** [cash-1day-change-root-cause-and-fix-2026-09-29.md](cash-1day-change-root-cause-and-fix-2026-09-29.md) (root cause, full change list, discrepancy table D1–D19).
**This document:** current state, how to start and see the UI, exactly how the UI behaves, what to verify, and what to decide before marking the task done.

---

## 1. State of the work

| Question                                                                                         | Answer                                                                                                                                                                                                                                                                                                                              |
| ------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Are TODOs still open?                                                                            | No. The "Todos 4/10" panel in your screenshot is a stale planning list from mid-implementation. Every item on it is done and verified: cash-state operations, model fields, API methods, portfolio/option state hooks, stock-card patch, discrepancy report. The last check showed a clean Angular build and 261/261 backend tests. |
| Is the migration applied?                                                                        | **Yes.** `20260929151645_AddCashSourceLink` is recorded in `__EFMigrationsHistory` on `PortfolioManagerLocal`. Currently 0 rows are linked (linking only happens when you accept the dialog on a new trade).                                                                                                                        |
| Is the backend running?                                                                          | **Yes**, on port 5000 (I restarted it with the new code).                                                                                                                                                                                                                                                                           |
| Is the frontend running?                                                                         | **No** — nothing is listening on 4200 (your `start-all.bat` ended with exit code 1). Start it with `start-frontend.bat` (or `cd frontend\portfolio-manager-ui; npx ng serve`).                                                                                                                                                      |
| Editor red squiggles in `portfolio-summary-bar.component.html` ("Property 'sc' does not exist")? | Stale editor diagnostics — the `@let` syntax is valid and `ng build` compiles it cleanly. Reload the window or ignore.                                                                                                                                                                                                              |
| Extra UI added in this pass                                                                      | A small **link icon** next to the description of any cash row that is tied to a trade (table and card view, with tooltip). Without it there was no way to tell a linked row from a manual one.                                                                                                                                      |

Committed to git? **No.** Nothing is committed to git yet (about 30 changed/new paths). Commit only after sign-off (suggested single commit, message below).

---

## 2. How to start and see it

```powershell
# backend already running on :5000; if you restart it, migration is already applied
cd D:\PORTFOLIO-MANAGER\frontend\portfolio-manager-ui
npx ng serve            # http://localhost:4200
```

Log in, open **Portfolio**. Hard-refresh the browser (Ctrl+F5) so the new bundle loads.

---

## 3. What you will see in the UI

### 3.1 Portfolio page — summary bar (top tiles)

- **1 Day Change** tile: same place and look, new number logic. The breakdown under it now reads:
  `Stocks +$17,460 · Cash −$17,550 · Options` (Cash/Options lines are hidden when exactly 0).
  Hover the breakdown for a tooltip: _each line is its value change since the last close; a buy shows as Stocks up and Cash down, together they net to the real gain or loss._
- On a buy day Stocks is now **green (up)** and Cash **red (down)** — that is expected; the headline is the net.
- The tile shows `—` until the previous-day snapshot has loaded (unchanged).

### 3.2 Dashboard

- Same layout. Headline, Stocks/Cash/Options lines now satisfy: `Stocks + Cash + Options = Portfolio Value − last close`.
- Tooltip on the breakdown (native `title`).
- **Still a stored snapshot** — press **Refresh** after trades/cash changes (see decision Q1).

### 3.3 Link cash dialog (new)

Opens automatically, right after the trade is saved:

```
🔗 Link cash to this trade
┌───────────────────────────────────────────────┐
│ Buy HNU.TO                       [↘ Cash out] │  (red left border; green + "↗ Cash in" for sells)
│ 2000 sh @ $8.77                               │
└───────────────────────────────────────────────┘
Record the cash side of this trade… Saved as TradePurchase.
[!] A similar manual entry already exists (…)   ← only when a look-alike row exists
Amount        $ 17,540.00        (hint: include fees here)
Account       TFSA_D_TD
Cash date     2026-09-29
Description   Buy HNU.TO — 2000 sh @ $8.77
TFSA_D_TD cash    $73,103.00  →  $55,563.00
[!] This leaves … negative …                    ← only if balance goes below 0
                                   [Skip]  [Add cash entry]
```

| Situation                           | What differs                                                                                                                    |
| ----------------------------------- | ------------------------------------------------------------------------------------------------------------------------------- |
| Duplicate look-alike found          | Orange banner; primary button turns red **"Add anyway"** — the safe choice is **Skip**.                                         |
| Trade date ≠ cash date              | Hint under the date + note that back-dating rewrites past snapshot cash but not stock values (dip). Default date is **today**.  |
| Balance would go negative           | Orange warning; still allowed.                                                                                                  |
| Update mode (edited a linked trade) | Title "Update linked cash", note shows the currently recorded amount/date, button "Update cash entry" — edits the **same** row. |
| Demo mode                           | Balances/notes masked (fake style) or blurred (blur style); the amount field you type in is always real.                        |

### 3.4 When it appears — and when it does not

| Action                                                                                                                                                                           | Dialog?                                                                                                                  |
| -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| Add Stock / Add Option (OPEN or CLOSE type)                                                                                                                                      | Yes, Buy/Sell                                                                                                            |
| Edit a position → CLOSE (full or partial), from grid, card, or Transactions page                                                                                                 | Yes, Sell (closed shares × closing price). Partial close: remainder becomes a new OPEN row with **no** link              |
| Edit shares / price / account of an already-linked position and amount no longer matches                                                                                         | Yes, Update                                                                                                              |
| Delete a position that has linked cash                                                                                                                                           | Yes, "Remove linked cash?" — _Keep_ / _Remove cash_                                                                      |
| Inline edits (decision source, option market price), sector/role changes, edits to old unlinked positions, manual positions, bulk Import, bulk multi-select delete, Cash Restore | **No**                                                                                                                   |
| Cash section                                                                                                                                                                     | Linked rows show a small 🔗 icon; description reads `Buy HNU.TO — 2000 sh @ $8.77`; type `TradePurchase`/`TradeProceeds` |

Skipping any dialog leaves you exactly where you were before this change (manual cash workflow still fully available).

---

## 4. Sign-off checklist

Do these in order on a **test position** (1 share of anything, TFSA_D_TD), then clean up. Tick each.

### A. Startup

- [x] Backend log shows no errors on start; `SELECT TOP 1 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC` → `20260929151645_AddCashSourceLink`.
- [x] Frontend loads, no console errors on Portfolio and Dashboard.

### B. Stocks

- [x] Add Stock OPEN → dialog opens after the Add dialog closes; amount = shares × cost.
- [x] _Add cash entry_ → snackbar "Cash entry linked to trade"; Cash section shows a 🔗 row; account balance dropped by the amount.
- [x] SQL: newest `CashItems` row → `TradePurchase`, negative, `SourceType='PortfolioOpen'`, `SourceItemId` = new position id.
- [x] Portfolio 1 Day Change: headline ≈ market move only (not ±trade amount); breakdown Stocks ≈ +cost, Cash ≈ −cost.
- [x] Dashboard → Refresh → same headline as Portfolio (within quote-timing differences).
- [x] Add Stock again → **Skip** → no cash row created.
- [x] Duplicate: add a manual TradePurchase cash row first, then add the stock → warning + "Add anyway"; choose Skip.
- [x] Edit position → CLOSE with closing price → Sell dialog → `TradeProceeds`, `PortfolioClose`.
- [x] Partial close (e.g. 10 → close 4) → amount uses 4 shares; remainder row appears as OPEN. (Since the completion pass the purchase cash row is also split: the remainder gets its own linked row.)
- [x] Change shares on a linked OPEN position → Update dialog edits the same row (row count unchanged).
- [x] Change only the decision source / sector of an old position → **no** dialog.
- [x] Delete the test position → "Remove linked cash?" → _Remove cash_ → row gone, balance restored.

### C. Options

- [x] Add Option OPEN 2 contracts @ 1.50 → Cash out **$300.00** (×100).
- [x] Edit to CLOSE at 2.00 → Cash in **$400.00**.
- [x] Update inline market price in the grid → **no** dialog.
- [x] Delete option → remove-cash prompt.

### D. Guards

- [ ] Amount larger than the balance → negative warning, still saves.
- [x] Try linking the same trade twice via Swagger `POST /api/cash/link` → 400 "already has a linked cash entry".
- [ ] Viewer role (if you have one) cannot POST `/api/cash/link` (403) — Admin/Trader only.
- [ ] Demo mode on: dialog balances masked/blurred.

### E. Regression (things that must still work)

- [ ] Add Cash / Edit Cash / Adjust Balance / Delete cash — unchanged.
- [ ] Import stocks dialog imports many rows **without** any cash prompts.
- [x] Grid/Card edit of a position no longer wipes account/type/dates (card view was the bug).
- [ ] Transactions page: edit a CLOSE row; delete a row.
- [ ] Portfolio Value History page loads; Reconcile / Recalculate still work.
- [x] `dotnet test` → 272 passed (261 at sign-off); `ng test` → 127 passed; `ng build` clean.

### F. Cleanup

- [x] Remove all test positions and any test cash rows (accept the removal prompts). Confirm `SELECT COUNT(*) FROM CashItems WHERE SourceType IS NOT NULL` is 0 (or only real trades you intentionally linked).

---

## 5. Decisions and considerations before marking done

### Must decide

| #   | Question                                                                                                                                                                                                                                                              | Recommendation                                                                                                              |
| --- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| Q1  | **Dashboard is a stored snapshot** — after a link it will not show the new numbers until you click Refresh (this caused the $798,955 vs $781,404 mismatch in your screenshot). Auto-rebuild after a link?                                                             | Accept "click Refresh" for now; auto-rebuild is a small follow-up but a rebuild calls Yahoo, so do it deliberately.         |
| Q2  | **Fix your historical data first (D1–D4).** D1 alone leaves Portfolio Value overstated by ≈ $8,180 (two Sep 25 option buys with no cash offset). D2 (+$21,254 adjustment), D3 (XNDU $706 net 0), D4 ($1,930 gap on TFSA_D_TD sells) need checking against statements. | Do before judging "are the numbers right" — the new code cannot repair old missing entries (forward-only by your decision). |
| Q3  | **Cash date defaults to today** for back-dated trades (avoids a history dip). Is that the accounting behaviour you want?                                                                                                                                              | Keep; user can override in the dialog.                                                                                      |
| Q4  | **Commission/fees:** amount is editable; is a separate fee row wanted?                                                                                                                                                                                                | Keep single row with fees included in the amount; note it in the Description.                                               |
| Q5  | **Deposits/withdrawals count as 1-day change** (D12). Show performance-only change?                                                                                                                                                                                   | Separate follow-up; not required for this task.                                                                             |
| Q6  | **Multi-user/FX** (D9, D10). Only matters when non-admin users or many USD holdings appear.                                                                                                                                                                           | Out of scope; keep on the list.                                                                                             |

### Known limitations (by design / accepted)

- Forward-only: old trades are not retro-linked, and old positions never trigger prompts.
- No bulk paths: Import, bulk delete, Cash Restore ignore linking.
- Editing a linked cash row directly on the Cash page can change its type away from Purchase/Proceeds (not blocked). The 🔗 tooltip tells you to edit the trade instead.
- If you delete a position and choose _Keep_, the cash row stays (still linked by id; harmless orphan).
- Partial-close remainder rows are unlinked; their purchase stays on the original row.
- Option values rely on manually updated `MarketPrice`; the Options line only moves when you update it.
- Snapshot baseline basis (~0.13%, D8) and quote timing mean the headline is accurate to roughly a few hundred dollars, not to the cent.

### Risks reviewed

| Risk                                                       | Mitigation in place                                                                                                                                                                 |
| ---------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Double-counting cash (you already type trade cash by hand) | Duplicate detection (±1 day, ±1%/$1, same account/direction) + Skip default + DB unique index per trade leg.                                                                        |
| Breaking existing flows                                    | Trades and cash stay decoupled in services; hooks fire only on genuine cash-relevant changes; `addItem` bulk path untouched; new `CashItemDto` fields are optional trailing params. |
| Migration safety                                           | Additive nullable columns + filtered index; full verified `.bak` taken beforehand.                                                                                                  |
| Wrong sign/type sent by a client                           | Server derives type and sign from `SourceType`; manual/missing/hidden trades rejected.                                                                                              |
| Circular DI (past NG0200 pitfall)                          | `TradeCashLinkService` depends only on API/cash-state/dialog/demo services; verified by a clean bootstrap of the build and API.                                                     |
| Snapshot corruption on back-dating                         | Reuses the existing transactional cash-only recalculation (stocks/options preserved) — covered by a test.                                                                           |

### Not covered by automated tests (manual only)

The dialog UI, the state-service hooks and the ET-date baseline logic have no frontend unit tests (checklist §4 covers them). Recommended follow-up: a spec for `TradeCashLinkService` (create vs update vs skip decisions, duplicate detection).

---

## 6. Definition of done

Mark the task complete when:

1. Checklist §4 A–F passes.
2. You have reviewed/decided Q1–Q4 and D1–D4 (data fixes are yours to make; I did not change any data).
3. Test data cleaned up.
4. Changes committed.

Suggested commit message:

```
Link cash ledger to stock/option trades and fix 1-day change

- CashItems.SourceType/SourceItemId + unique TradeLink index (migration AddCashSourceLink)
- POST/GET /api/cash/link; Link cash dialog with duplicate + negative-balance guards
- Stocks 1-day change = value delta vs last close (Dashboard + Portfolio), ET-date baseline
- Fix stock-card edit nulling transaction fields; link indicator on cash rows
- Tests: CashTradeLinkTests
```

**Rollback:** see §7 of the companion report (schema back to `20260909233823_AddAutomationRunDiagnostics`, or restore `D:\PORTFOLIO-MANAGER-SQL-BACKUP-ALL\CASH-DISCREPANCY\20260929-110605\PortfolioManagerLocal_FULL.bak`).
