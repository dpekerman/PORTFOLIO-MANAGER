# Cash/Trade Linking — Completion Summary & Test Guide (2026-09-29)

Single place for "what was done, what changed, how to test, what is left".
Earlier reports: [root cause & fix](cash-1day-change-root-cause-and-fix-2026-09-29.md) · [review & sign-off](cash-trade-link-review-and-signoff-2026-09-29.md) · [verification report](cash-link-verification-report-2026-09-29.md).

## 1. Phase map

| Phase         | Content                                                                                                                                                          | Status   |
| ------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------- |
| 1             | Root cause: cash ledger decoupled from trades; 1-day change = value delta                                                                                        | Done     |
| 2             | Cash↔trade link (DB `SourceType/SourceItemId`, API, link dialog, prompts on add/edit/close/delete)                                                               | Done     |
| 3             | Verification pass (17 live scenarios), V3/V4 fixes                                                                                                               | Done     |
| 4 (this pass) | V1 stale snapshot, V5 unlinked-trades list, V2 429 handling, V6 partial-close split, V7 colour, Q1 dashboard auto-refresh, inline-edit data-loss bug, unit tests | **Done** |

## 2. What changed in this pass

### Backend

| Change                                                                                                                                                                                                                                                                                                                               | Files                                                                                                                |
| ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------- |
| **V1** Stored portfolio snapshot is now updated on every stock add / manual add / edit (incl. partial-close remainder) / delete, so a reload never shows pre-edit rows. Keeps quote, price structure, Final Action.                                                                                                                  | `Services/PortfolioSnapshotService.cs` (`UpsertItemsAsync`, `RemoveItemAsync`), `Controllers/PortfolioController.cs` |
| **V5** `GET /api/cash/unlinked-trades`: buys/sells (stocks + options, ×100) on/after ledger start with no linked cash row and no hand-entered look-alike (same account & type, ±1% / ≥$1, ±3 days, each row vouches for one trade). Zero-value legs (worthless expiry) are ignored; legs without a price are flagged `MissingPrice`. | `Services/UnlinkedTradeService.cs` (new), `Controllers/CashController.cs`, `Models/Dtos.cs`, `Program.cs`            |
| **V6** Partial close now splits the purchase cash row: closed shares keep their share, remainder gets its own linked row (same date/account). Net cash unchanged.                                                                                                                                                                    | `Services/CashService.cs` (`SplitPurchaseLinkAsync`), `PortfolioController.Update`                                   |

### Frontend

| Change                                                                                                                                                                                                                                                             | Files                                                                                                                                                                       |
| ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **V5 UI** "Trades without a cash entry" panel on _Portfolio Value History → Cash Ledger_ with a **Link cash** button per row (date pre-filled with the trade date, so history is back-dated correctly).                                                            | `features/portfolio-value-history/unlinked-trades/*`, `cash-state.service.ts`, `portfolio-api.service.ts`, `trade-cash-link.service.ts`, `link-cash-dialog` (`defaultDate`) |
| **V2** HTTP 429 no longer logs you out: startup checks retry (2s/4s/6s); refresh-token 429 keeps the session.                                                                                                                                                      | `auth-state.service.ts`, `auth.interceptor.ts`                                                                                                                              |
| **Q1** Dashboard is rebuilt automatically after a cash link is created/updated/removed and after a partial close.                                                                                                                                                  | `trade-cash-link.service.ts`, `portfolio-state.service.ts`                                                                                                                  |
| **V7** Negative cash rows are red in the Portfolio cash grid.                                                                                                                                                                                                      | `portfolio-page.component.html`                                                                                                                                             |
| **Bug fix (found now)** Inline "decision source" edits on stocks/options and the inline option market-price edit sent only some fields, so the API **nulled** TransactionType/AccountType/dates/closing price (or DecisionSourceClosed). They now send all fields. | `portfolio-page.component.ts`                                                                                                                                               |

### Tests added

- Backend `UnlinkedTradeAndSnapshotTests` (11): detector (linked, pre-ledger, manual, look-alike once, wrong account/direction, both legs + ×100, zero-value, missing price), purchase split (+ idempotent), snapshot upsert/remove.
- Frontend `trade-cash-link.service.spec.ts` (15) and `auth-state.service.spec.ts` (4).

## 3. Verification results

| Check                                                                                                                                                                                                                                                       | Result                                             |
| ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------- |
| Backend `dotnet test`                                                                                                                                                                                                                                       | **272 / 272** (was 261)                            |
| Angular `ng test`                                                                                                                                                                                                                                           | **127 / 127** (was 108)                            |
| Angular `ng build`                                                                                                                                                                                                                                          | Clean (only the pre-existing SCSS budget warnings) |
| Live API (restarted with new code): add stock → snapshot has it; partial close 4→1 → snapshot shows CLOSE 1 + OPEN 3; purchase row −40 split into −10 / −30; delete → snapshot back to 115 rows; test data removed (cash rows 18, ledger $55,553 unchanged) | Pass                                               |
| Live UI: Cash Ledger tab shows the unlinked panel; no unexpected console errors                                                                                                                                                                             | Pass                                               |

### Numbers cross-check (DB vs Dashboard vs Portfolio page, 2026-09-29 ~13:50 ET)

| Figure                                    | Recomputed from DB          | Dashboard          | Portfolio page                |
| ----------------------------------------- | --------------------------- | ------------------ | ----------------------------- |
| Stocks (49 active, snapshot prices)       | 686,098.81                  | implied 686,098.81 | —                             |
| Cash (18 rows)                            | 55,553.00                   | —                  | —                             |
| Options (11 open, ×100)                   | 34,260.00                   | —                  | —                             |
| **Portfolio value**                       | **775,911.81**              | $775,911.81        | $775,911.81                   |
| 1-day change vs Sep 28 close (781,494.73) | −5,582.92                   | −5,582.92          | −5,582.92                     |
| – Stocks / Cash / Options                 | +11,967.08 / −17,550.00 / 0 | same               | Stocks +11,967 / Cash −17,550 |
| Week (vs Sep 25 close 766,289.36)         | +9,622.45                   | +9,622             | —                             |
| Month (vs Aug 31 close 801,943.68)        | −26,031.87                  | −26,03x            | —                             |
| Positions                                 | 49 + 11 + 1 (cash) = 61     | —                  | 61                            |

Cash −17,550 = 55,553 (now) − 73,103 (Sep 28 close). The whole 1-day drop is cash: see §5.

## 4. How to test (about 15 minutes)

Use `start-all.bat` (or restart backend + `ng serve`); the backend must be restarted to get the new endpoint. Take a DB backup first.

1. **Numbers** – Dashboard and Portfolio page show the same value and 1-day change; Stocks + Cash + Options = headline. Click **Refresh** on Dashboard first.
2. **Stale snapshot (V1)** – Add a test stock (`ZZ TEST`, 4 sh, $10). Accept the link dialog. Full page reload (F5) → row is present without pressing Refresh. Edit shares, reload → new value. Delete, reload → gone.
3. **Partial close (V6)** – Close 1 of the 4 shares (price $11) and accept the Sell dialog. _Cash Ledger_ shows purchase −$10 (original) and −$30 (remaining shares, linked); total cash change equals −$40 + $11. Deleting the remainder row offers to remove its linked cash.
4. **Inline edits** – Change a stock's _decision source_ in the Portfolio grid, reload, open Edit: account, open/close dates, closing price are still set. Same for an option's decision source and market price.
5. **Unlinked list (V5)** – _Portfolio Value History → Cash Ledger_: panel lists trades without cash (see §5). Click **Link cash** on one, confirm: row disappears, a 🔗 cash row appears with the trade date, Dashboard refreshes by itself.
6. **Dashboard auto-refresh (Q1)** – After step 5 the Dashboard value/change already reflect the new cash without pressing Refresh.
7. **429 (V2)** – Reload the page ~10 times quickly: you may see a short delay, but you are not sent to the login page.
8. **Negative cash (V7)** – Portfolio → Cash: negative entries are red.
9. **Cleanup** – Remove `ZZ TEST` rows and their cash entries.

## 5. Your data decisions (cannot be automated)

The unlinked panel currently lists 8 real trades. Linking them changes cash history, so decide per row:

| Trade                                                | Amount                          | Note                                                                                                                                                                                                                         |
| ---------------------------------------------------- | ------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| K.TO CALL $35 buy (Sep 25)                           | −4,700                          | Missing option cash (D1)                                                                                                                                                                                                     |
| MDA.TO CALL $50 buy (Sep 25)                         | −3,480                          | Missing option cash (D1)                                                                                                                                                                                                     |
| MCD.TO, BOFA.TO, GOOG buys (Sep 25/26, Corp_TD)      | −10,085 / −11,212 / −344        | Together −21,641; compare with the **+21,254 Corp_TD adjustment on Sep 27 (D2)**. If that adjustment was made to compensate for these buys, link them **and** delete/reduce the adjustment, otherwise cash is counted twice. |
| META.TO sells (Sep 16, Sep 21), ATH.TO sell (Sep 11) | +8,867.50 / +9,437.50 / +10,880 | Proceeds never entered (or entered under another wording, D3/D4). If you added them by hand outside ±3 days / ±1%, edit that row instead.                                                                                    |

Recommended order: link the two options, then resolve the Corp_TD adjustment together with the three buys, then the sells. The 1-day change of −$17,550 in Cash comes from the Sep 26/27 entries; it will move as these rows are back-dated.

## 6. Decisions / known limits (no code pending)

| Item                                       | Decision                                                                                                                                                                    |
| ------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Q4 Fees                                    | Trade amount = shares × price only. Enter commissions as a separate `Fee` cash row.                                                                                         |
| Q5 Deposits in 1-day change                | Deposits/withdrawals remain part of Cash change (they are real cash movements); the ledger tags them `IsExternalFlow` if you later want a "performance excl. flows" figure. |
| Q6 / V8 Currency & multi-user              | No FX conversion (CAD amounts assumed); pages label `$` vs `CA$` differently. Dashboard uses the signed-in user's rows plus unowned rows. Not changed.                      |
| Restore backup                             | `POST /portfolio/restore` does not rewrite the stored snapshot — click Refresh after a restore.                                                                             |
| Dashboard snapshot after plain stock edits | Rebuilt on Refresh and after cash-link actions; not on every stock edit (rebuild calls Yahoo).                                                                              |

## 7. Files touched (this pass)

Backend: `PortfolioSnapshotService.cs`, `PortfolioController.cs`, `CashController.cs`, `CashService.cs`, `UnlinkedTradeService.cs` (new), `Dtos.cs`, `Program.cs`, tests `UnlinkedTradeAndSnapshotTests.cs` (new), `CashTradeLinkTests.cs` (helper made internal).
Frontend: `auth-state.service.ts`, `auth.interceptor.ts`, `cash-state.service.ts`, `portfolio-api.service.ts`, `portfolio-state.service.ts`, `trade-cash-link.service.ts`, `portfolio.models.ts`, `link-cash-dialog.component.ts`, `portfolio-page.component.{ts,html}`, `portfolio-value-history-page.component.{ts,html}`, `unlinked-trades/*` (new), specs `trade-cash-link.service.spec.ts`, `auth-state.service.spec.ts` (new).
No database migration in this pass. Nothing is committed to git yet.

## 7. Update 2026-10-08 - legacy gaps archived

The 8 trades listed in section 5 were decided *won't fix*. They are recorded in the new table `UnlinkedTradeBaselines` (additive migration `AddUnlinkedTradeBaselines`) and no longer shown in the "Trades without a cash entry" panel. No cash, trade or snapshot data was changed. A baselined trade that is edited later reappears. New unlinked trades are shown with a "Why?" diagnosis (expected cash row, nearest rejected cash row and the rule it failed) and logged as a server warning. To show a legacy row again, delete its row from `UnlinkedTradeBaselines`; `POST /api/cash/unlinked-trades/baseline` (Admin) archives whatever is currently listed.
