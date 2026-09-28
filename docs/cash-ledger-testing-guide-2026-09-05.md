# Cash Ledger & EOD Snapshot Race-Safety — Testing Guide

**Implementation date:** 2026-09-05
**Status:** Fully implemented (Phases 0–5), applied to the real local database, verified.

## What changed, in one paragraph

`CashItem` rows are now a strict ledger — every row carries a required `CashFlowType`
(`Deposit`/`Withdrawal`/`TradeProceeds`/`TradePurchase`/`Dividend`/`Interest`/`Fee`/`Tax`/
`AdjustmentIncrease`/`AdjustmentDecrease`, plus migration-only `OpeningBalance`). "Editing the balance"
is now two distinct actions: **Adjust Balance** (type the new total, pick a Type, backend computes and
inserts a dated delta row) and **Edit Entry** (correct one existing row's own fields). Backdated ledger
changes trigger a transactional recalculation of `PortfolioValueHistories.CashValue`/`TotalValue` only —
`StocksValue`/`OptionsValue` on existing rows are never touched. Same-day snapshot resealing is debounced
(90s quiet period) and is a timing optimization only, never required for correctness.

---

## 1. Automated backend tests

```powershell
cd backend\PortfolioManager.Tests
dotnet test --filter "FullyQualifiedName~CashLedgerTests|FullyQualifiedName~CashFlowTypeRulesTests"
```

Expect **37 passed, 0 failed**. This covers:

- Required `CashFlowType` validation (missing/invalid/`OpeningBalance` rejected on Add)
- Sign derivation per Type (Deposit/TradeProceeds/etc. positive; Withdrawal/TradePurchase/etc. negative)
- `IsExternalFlow` classification (true only for Deposit/Withdrawal)
- Adjust Balance: valid delta inserted, contradictory direction rejected (400), zero delta rejected (400)
- Edit Entry: type-only change persists classification without touching `CashValue`; both-dates-future
  edits are no-ops
- Delete: blocked for `OpeningBalance` rows
- `RecalculateCashRangeAsync` preserves `StocksValue`/`OptionsValue` exactly, only patches
  `CashValue`/`TotalValue` (the regression test for the original bug this design avoids)
- `RecalculateCashRangeAsync` never touches rows dated before `LedgerStartDate`
- Idempotency: running the recalculation twice produces identical numbers
- **Real transactional rollback**: a forced failure inside the history recompute rolls back the ledger
  write too — the row is never left half-committed (uses an in-memory SQLite database, since EF Core's
  InMemory provider doesn't support real transactions)

Run the full regression suite to confirm nothing else broke:

```powershell
cd backend\PortfolioManager.Tests
dotnet test
```

Expect **220 passed, 0 failed**.

---

## 2. Backend API manual testing (Swagger or curl)

Start the API (`dotnet run --launch-profile http` from `backend\PortfolioManager.Api`), then use
`http://localhost:5000/swagger`. You'll need an Admin or Trader account (the cash endpoints are
`[Authorize(Roles = "Admin,Trader")]` for writes).

### 2.1 Add Cash (ledger entry)

`POST /api/cash`

```json
{
  "description": "Bank deposit",
  "amount": 5000,
  "cashFlowType": "Deposit",
  "accountType": "TFSA_D_TD",
  "transactionDate": "2026-09-05"
}
```

- Try omitting `cashFlowType` → expect **400**.
- Try `"cashFlowType": "OpeningBalance"` → expect **400** ("migration/admin-only").
- Try a **future** date (e.g. next month) → row is created, but `GET /api/cash` totals for "today" should
  not include it yet (verify via SQL, see §4).

### 2.2 Adjust Balance

`POST /api/cash/adjust-balance`

```json
{
  "accountType": "TFSA_D_TD",
  "desiredNewTotal": 9500,
  "cashFlowType": "TradeProceeds",
  "transactionDate": "2026-09-05"
}
```

- Check the current total for that account first (sum `CashItems` for that `AccountType`), pick a
  `desiredNewTotal` that's **higher**, and try `"cashFlowType": "Withdrawal"` → expect **400**
  (contradictory direction — Withdrawal is negative-only).
- Try `desiredNewTotal` equal to the current total → expect **400** ("no change to record").
- Try a valid combination → expect a new row, and the account's total should now equal `desiredNewTotal`.

### 2.3 Edit Entry

`PUT /api/cash/{id}`

```json
{
  "description": "Corrected description",
  "amount": 5000,
  "cashFlowType": "Deposit",
  "accountType": "TFSA_D_TD",
  "transactionDate": "2026-09-05"
}
```

- Edit only the `cashFlowType` (e.g. `TradeProceeds` → `Deposit`) on an existing entry, same amount/date →
  should succeed; verify (§4) that the `PortfolioValueHistories` row for that date is unchanged, but the
  `CashItems` row's `CashFlowType`/derived `IsExternalFlow` changed.
- Try editing an `OpeningBalance` row's `cashFlowType` to something else → expect **400**.

### 2.4 Delete

`DELETE /api/cash/{id}`

- Delete a historical (past-dated) entry → verify (§4) the affected `PortfolioValueHistories` rows from
  that date forward were recalculated.
- Delete a future-dated entry → verify no `PortfolioValueHistories` rows changed.
- Try deleting an `OpeningBalance` row → expect **400**.

### 2.5 Duplicate OpeningBalance (should never happen via the API, but verify the DB guard)

Only reachable via direct SQL (the API never lets you create `OpeningBalance` rows) — see §4 for the
SQL-level verification already performed during implementation.

---

## 3. Frontend manual testing

Start both servers (`start-all.bat` or `start-backend.bat` + `start-frontend.bat`), open
`http://localhost:4200`, go to the **Portfolio** page, expand the **Cash** section.

1. **Per-account subtotals**: confirm a row per `AccountType` appears above the cash list/table, each
   with a "tune" icon button ("Adjust Balance").
2. **Add Cash dialog**: click "+ Add Cash" — confirm a required **Type** dropdown appears (no
   `OpeningBalance` option), submit fails without a Type selected.
3. **Adjust Balance dialog**: click the tune icon on an account row — confirm current total is shown,
   type a new total, confirm the live delta preview updates, pick a Type, submit. Try a contradictory
   Type/direction combo — confirm the error message from the backend is shown inline (not just a generic
   toast).
4. **Edit Cash dialog**: click Edit on an existing entry — confirm it now shows Type and Transaction Date
   fields. Edit an `OpeningBalance` row (if any legacy rows still show as such) — confirm the Type field
   is disabled/pinned to `OpeningBalance`.
5. Confirm the grand total ("Total Cash" in the section header) still equals the sum of all rows after
   any Add/Adjust/Edit/Delete action.

---

## 4. Database-level verification (SQL Server Management Studio, Azure Data Studio, or sqlcmd)

Connect to `PortfolioManagerLocal` on `localhost`.

**Current ledger state per account:**

```sql
SELECT AccountType, CashFlowType, Amount, TransactionDate, Description
FROM CashItems
ORDER BY AccountType, TransactionDate;
```

**Grand total (should always equal the sum shown in the UI):**

```sql
SELECT SUM(Amount) AS GrandTotal FROM CashItems;
```

**Ledger start date (the accounting boundary — should stay 2026-09-05 forever, this migration only runs once):**

```sql
SELECT * FROM CashLedgerSettings;
```

**Confirm a backdated recalculation preserved Stocks/Options and only changed Cash:**

```sql
SELECT RecordedDate, TotalValue, StocksValue, CashValue, OptionsValue
FROM PortfolioValueHistories
WHERE RecordedDate = '<the date you backdated an entry to>';
```

Compare `StocksValue`/`OptionsValue` before and after your test edit — they must be identical.

**Confirm the OpeningBalance uniqueness guard (already verified during implementation, safe to re-check anytime):**

```sql
SET QUOTED_IDENTIFIER ON;
INSERT INTO CashItems (Description, Amount, AddedAt, AccountType, TransactionDate, CashFlowType)
VALUES ('dup test', 100, SYSUTCDATETIME(), 'Corp_TD', '2026-09-05', 'OpeningBalance');
-- Expect: "Cannot insert duplicate key row... unique index 'IX_CashItems_Account_OpeningBalance'"
```

---

## 5. Race/debounce verification (optional, requires the API running continuously)

1. Watch the API console/logs for `[PortfolioValueEod]` entries.
2. Make two quick cash/portfolio/option edits within a few seconds of each other during market hours.
3. Confirm the log shows the reseal is deferred until ~90 seconds after the _last_ edit
   (`"Resealing today's (...) snapshot after a quiet period."`), not immediately after the first edit.

---

## 6. Rollback / disaster recovery

Full-database backups were taken before any schema/data change:

- `D:\PORTFOLIO-MANAGER-SQL-BACKUP-ALL\pre-cash-ledger-migration-2026-09-05\PortfolioManagerLocal_20260905-113631.bak` (pre-rehearsal)
- `D:\PORTFOLIO-MANAGER-SQL-BACKUP-ALL\pre-cash-ledger-migration-2026-09-05\PortfolioManagerLocal_20260905-114938.bak` (immediately pre-real-migration — **use this one to restore if needed**)

To restore:

```sql
RESTORE DATABASE [PortfolioManagerLocal] FROM DISK = N'D:\PORTFOLIO-MANAGER-SQL-BACKUP-ALL\pre-cash-ledger-migration-2026-09-05\PortfolioManagerLocal_20260905-114938.bak' WITH REPLACE;
```

(Stop the API first so nothing holds a connection to the database during the restore.)

A disposable rehearsal database (`PortfolioManagerLocal_MigrationTest`) also still exists on the same SQL
Server instance if you want to experiment further without touching real data — drop it with
`DROP DATABASE PortfolioManagerLocal_MigrationTest;` once you no longer need it.

---

## 7. Known limitations / follow-ups (not blockers)

- `CashService.BackupAsync`/`RestoreAsync` (the JSON export/import feature, separate from the SQL backup
  above) were not updated to include `CashFlowType`/`TransactionDate` — a restore via that feature would
  currently drop the classification. Flag for a future pass if that feature is actively used.
- No dedicated Angular unit tests were written for the new/modified dialogs — verified via `ng build`
  (template type-checking) and manual walkthrough only.
- Recommendation #2 from the original analysis (bundling a stock buy/sell with its cash movement into one
  atomic API call) remains explicitly out of scope/deferred, as agreed in planning.
