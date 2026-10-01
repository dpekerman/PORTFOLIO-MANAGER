# Verification Report — Cash/Trade Linking & 1 Day Change (2026-09-29)

Companion documents: [root cause & fix](cash-1day-change-root-cause-and-fix-2026-09-29.md) · [review & sign-off guide](cash-trade-link-review-and-signoff-2026-09-29.md).

**Method:** automated suites + live UI walk-through (Playwright driving your signed-in browser against the real local API/DB). All test positions/cash rows were removed afterwards; final state matches the start: **18 cash rows, $55,553, 0 linked, 49 active stocks, 29 options**. Backup taken earlier: `D:\PORTFOLIO-MANAGER-SQL-BACKUP-ALL\CASH-DISCREPANCY\20260929-110605\PortfolioManagerLocal_FULL.bak`.

---

## 1. Results

### Automated

| Suite                                                                                 | Result                                               |
| ------------------------------------------------------------------------------------- | ---------------------------------------------------- |
| Backend (`dotnet test`, incl. 17 new link tests)                                      | **261 / 261 passed**                                 |
| Angular unit tests                                                                    | **108 / 108 passed**                                 |
| Angular production-style build                                                        | Clean                                                |
| DB unique index `IX_CashItems_TradeLink` on real SQL Server (rolled-back transaction) | Duplicate link rejected; many unlinked rows accepted |

### Live UI

| #   | Scenario                                                                                               | Result                                                      |
| --- | ------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------- |
| 1   | Portfolio and Dashboard 1 Day Change                                                                   | Both −$4,009.11; Stocks +$13,541 + Cash −$17,550 = headline |
| 2   | Buy stock → Link dialog (amount, before→after balance, Cash out)                                       | Pass                                                        |
| 3   | Accept → one row `TradePurchase`, `SourceType=PortfolioOpen`; Portfolio Value and headline unchanged   | Pass                                                        |
| 4   | 🔗 icon on the cash row (Portfolio cash section)                                                       | Pass                                                        |
| 5   | Change shares 1→2 → Update dialog edits the **same** row                                               | Pass                                                        |
| 6   | Rename only (no cash-relevant change) → no dialog                                                      | Pass                                                        |
| 7   | Close full position → Sell dialog; Portfolio Value moved only by the gain                              | Pass                                                        |
| 8   | **Partial close** (4 sh, close 1): dialog uses 1 sh; remainder (3 sh) becomes a new OPEN row, unlinked | Pass                                                        |
| 9   | Delete position → "Remove linked cash?" listing both entries; _Remove cash_ restores ledger            | Pass                                                        |
| 10  | Option buy: ×100 multiplier ($100 for 2 × $0.50); Skip creates nothing                                 | Pass                                                        |
| 11  | Option close → Sell dialog → POST 201; option delete → remove prompt                                   | Pass                                                        |
| 12  | Duplicate protection (manual −$50 row, then matching buy) → warning + "Add anyway"                     | Pass                                                        |
| 13  | Card-view edit no longer nulls TransactionType/account/open date (verified in DB)                      | Pass                                                        |
| 14  | Card-view delete removed only the test row (real HNU.TO #618 intact)                                   | Pass                                                        |
| 15  | Import dialog opens; Adjust Cash Balance dialog opens (no prompts from link logic)                     | Pass                                                        |
| 16  | In-app navigation across all 10 pages: no console errors, no 4xx/5xx                                   | Pass                                                        |
| 17  | Portfolio Value History page unchanged (Sep 28 snapshot $781,494.73)                                   | Pass                                                        |

---

## 2. Issues found

### A. Fixed during this verification

| ID  | Issue                                                                                  | Fix                                                                |
| --- | -------------------------------------------------------------------------------------- | ------------------------------------------------------------------ |
| V3  | The Cash Ledger tab (Portfolio Value History) gave no sign that a row is trade-linked. | 🔗 icon + tooltip on the Type column (desktop) and cards (mobile). |
| V4  | Link dialog hint text sat tight against the next field label.                          | Field gap 4px → 10px.                                              |
| —   | (Earlier) Card-view edit wiped transaction fields (D14).                               | Fixed and DB-verified (scenario 13).                               |
| —   | (Earlier) Portfolio tile picked "today" by UTC date.                                   | Eastern-time dates.                                                |

### B. Open issues — need your decision or a follow-up

| ID     | Severity                | Issue                                                                                                                                                                                                                                                                                                                                  | Evidence / impact      | Recommendation                                                                                                                                                      |
| ------ | ----------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **V1** | **High** (pre-existing) | **Stale stored snapshot after a full page reload.** Portfolio/Transactions load a stored snapshot that is not updated by edits; after reload the Transactions page showed a row as `OPEN 1 sh` while the DB had `CLOSE 2 sh`. Editing from stale data can overwrite newer DB values, and link decisions use that stale "previous" row. | Reproduced live.       | Click header **Refresh** after reloading and before editing. Follow-up: update the snapshot on every position edit/delete, or reload data on entry to edit dialogs. |
| **V2** | Medium (pre-existing)   | **HTTP 429 (rate limit) logs the user out.** Ten quick full-page reloads triggered 429s and the app fell back to the login page.                                                                                                                                                                                                       | Reproduced (my sweep). | Treat 429 on token refresh as retryable, not as auth failure.                                                                                                       |
| **V5** | Medium (by design)      | **Skipping the link re-creates the original double count.** Skipping an option buy raised Portfolio Value by $100.                                                                                                                                                                                                                     | Reproduced live.       | Follow-up: an "unlinked trades" indicator/report (positions opened or closed since ledger start with no linked or look-alike cash row).                             |
| **V6** | Low                     | **Partial close semantics.** The original row keeps the _full_ purchase link (−$40 for 4 sh) while the remainder row is unlinked; deleting the closed row offers to remove both cash entries although 3 shares remain. Net cash math is correct.                                                                                       | Reproduced live.       | Accept, or split the purchase link on partial close (follow-up).                                                                                                    |
| **V7** | Low                     | Cash section amount cell uses a fixed "positive" style class, so negative cash entries are not visually distinguished (seen in markup; not confirmed visually).                                                                                                                                                                        | Markup review.         | Cosmetic follow-up.                                                                                                                                                 |
| **V8** | Low (pre-existing)      | Currency labelling is inconsistent: Portfolio tile `$` (USD format), Dashboard/History `CA$`/CAD; no FX conversion (D9).                                                                                                                                                                                                               | Observed.              | Decide currency policy.                                                                                                                                             |
| **V9** | Info                    | Option technical-data endpoint returns 404 for an unknown ticker (expected for the fake test ticker).                                                                                                                                                                                                                                  | Log.                   | None.                                                                                                                                                               |

### C. Not covered by this verification

Demo-mode masking inside the dialog (no UI toggle reachable from the header), actually importing a file, submitting Adjust Balance, non-Admin roles (Trader/Viewer 403), narrow mobile layout of the dialog beyond the browser's small viewport, and multi-user dashboards. Unit tests for `TradeCashLinkService` still do not exist (recommended).

---

## 3. Carry-over from the earlier reports (still open, your data)

D1 ≈ $8,180 missing option-purchase cash (MDA.TO $3,480, K.TO $4,700, Sep 25) · D2 +$21,254 Corp_TD adjustment on Sep 27 · D3 XNDU.TO $706 proceeds net to zero · D4 $1,930 gap on TFSA_D_TD sales · D6 Dashboard is a stored snapshot (click Refresh) · D12 deposits count as 1-day change · D9/D10 FX and per-user scoping. Details in the root-cause report §8.

---

## 4. Files changed in this verification pass

- `features/portfolio-value-history/cash-ledger-table/cash-ledger-table.component.html` and `.scss` — link icon.
- `shared/link-cash-dialog/link-cash-dialog.component.scss` — spacing.

## 5. Recommended next phase (priority order)

1. **V1** stale-snapshot handling (protects data integrity of every edit).
2. **V5** unlinked-trade detector + your D1–D4 data corrections.
3. **V2** 429 handling.
4. Auto-refresh Dashboard after a link (Q1), fees policy (Q4), deposits in 1-day change (Q5).
5. `TradeCashLinkService` unit tests; split purchase link on partial close (V6).
