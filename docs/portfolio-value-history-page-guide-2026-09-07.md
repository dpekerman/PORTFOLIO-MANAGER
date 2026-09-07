# Portfolio Value History — Feature Guide & Test Plan

**Page route:** `/portfolio-value-history` (nav label: **Value History**, icon: `history`)
**Added:** 2026-09-05/06 · **Status:** Implemented, live-tested, working

---

## 1. Why this page exists

The Rev 6 cash-ledger rework (2026-09-05) changed how `CashValue`/`TotalValue` get recalculated after a
backdated or same-day cash correction. That created a real risk: **"did the recalculation actually
preserve historical Stocks/Options values, or did it silently corrupt them?"**

This page exists to answer three operational questions at a glance, without needing SQL access:

1. **What did the system record?** — the exact Stocks/Options/Cash/Total breakdown for every EOD
   snapshot, in one table.
2. **Why did it change?** — every snapshot shows _when_ it was first recorded, _whether_ it was ever
   recalculated afterward, and _why_ (same-day reseal vs. a backdated cash correction).
3. **Was it recalculated correctly?** — a per-row consistency check (green check / warning) confirms
   `Total = Stocks + Options + Cash` still holds, and the audit trail proves Stocks/Options were never
   touched by a cash-only recalculation.

It is intentionally **operational, not analytical** — no charts, no performance metrics. It is a direct
window onto the `PortfolioValueHistories` and `CashItems` tables.

---

## 2. Where to find it / who can see what

- **Navigation:** top nav bar → **Value History** (between _Value Screener_ and _Configuration_).
- **Read-only sections** (summary cards, Snapshots tab, Cash Ledger tab, filters): visible to **any
  authenticated user**.
- **Admin / Debug Actions** section at the bottom: visible **only if your account has the Admin role**
  (`AuthStateService.isAdmin()`). Non-admin users won't see this section at all — it doesn't just
  disable, it's absent from the DOM.
- The page is **read-only for historical data** — there is no way to edit a `PortfolioValueHistory` row
  from the UI, by design. The only "write" surfaces are the 4 admin actions (which recompute, never
  hand-edit a number) and the Cash Ledger's **Edit** action (which edits ledger entries, not snapshots).

---

## 3. Page layout

```
Portfolio Value History
├─ Summary cards (4)
├─ Filter bar (From / To / Snapshot Status / Reset Filters)
├─ Tabs: Snapshots | Cash Ledger
│   ├─ Snapshots: table + expandable row detail
│   └─ Cash Ledger: account subtotals + table (Edit action)
└─ Admin / Debug Actions (Admin only)
```

### 3.1 Summary cards

| Card                                | Value shown                                                               | Source                                                                                                                   |
| ----------------------------------- | ------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| **Latest Portfolio Value**          | Most recent snapshot's `TotalValue`                                       | First row of the loaded range (rows are most-recent-first)                                                               |
| **Latest Cash**                     | Most recent snapshot's `CashValue`                                        | Same row                                                                                                                 |
| **Latest External Flow**            | Most recent snapshot's `ExternalCashFlow`                                 | Same row — sum of that date's Deposit/Withdrawal ledger entries only                                                     |
| **Latest Recorded / Last Resealed** | The row's `RecordedAt`, or `LastRecalculatedAt` if it was ever recomputed | Label switches automatically: "Last Recorded" if never recalculated, "Last Resealed" if it has a recalculation timestamp |

All monetary values pass through `demoMode.maskValue()` — if Demo Mode is on, these numbers are masked
like everywhere else in the app.

### 3.2 Filter bar

- **From / To** — date range pickers. Filtering is done **client-side** over whatever the server
  returned (server default range is the **last 90 days**; if you need older data, widen From beyond what
  was fetched — note the table won't show dates outside the fetched window even if you set From further
  back, since the initial fetch is capped at 90 days by default).
- **Snapshot Status** — dropdown: `Original`, `Resealed`, `Cash Recalculated`, `Pending Reseal`. Selecting
  one hides every row that doesn't match.
- **Reset Filters** — clears both the date range and the status filter back to "show everything fetched."
- There is **no Account filter** on the Snapshots tab by design — a snapshot is a whole-portfolio total,
  it has no per-account breakdown to filter by. (Account filtering only makes sense on the Cash Ledger
  tab, where each row already belongs to one account.)

---

## 4. Snapshots tab

One row = one calendar day's EOD portfolio value snapshot.

### 4.1 Columns

| Column            | Meaning                                                                                                                                                                                                                                            |
| ----------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Date**          | `RecordedDate` — the ET trading date this snapshot represents                                                                                                                                                                                      |
| **Total Value**   | Stored `TotalValue` (should always equal Stocks+Options+Cash — see Status/icon below)                                                                                                                                                              |
| **Stocks**        | Stored `StocksValue` at the time this snapshot was written                                                                                                                                                                                         |
| **Options**       | Stored `OptionsValue`                                                                                                                                                                                                                              |
| **Cash**          | Stored `CashValue`                                                                                                                                                                                                                                 |
| **External Flow** | Sum of that date's ledger entries classified `Deposit`/`Withdrawal` only. **Never** inferred from day-over-day `CashValue` changes — an internal transfer (e.g. sell stock → cash) will correctly show `$0.00` here even though `CashValue` jumped |
| **Daily $**       | `TotalValue` − previous _chronological_ day's `TotalValue`. Computed from the full unfiltered list, so applying a status filter never distorts this number                                                                                         |
| **Daily %**       | Same delta, as a percentage of the previous day's `TotalValue`, passed through `demoMode.maskPercent()`                                                                                                                                            |
| **Status**        | See §4.2                                                                                                                                                                                                                                           |
| **Recorded**      | `RecordedAt` — the exact timestamp this row was first written                                                                                                                                                                                      |
| **Recalculated**  | `LastRecalculatedAt` if this row was ever recomputed after its initial write, else `—`                                                                                                                                                             |
| _(expand arrow)_  | Click anywhere on the row (or the arrow) to open/close the detail panel — see §4.3                                                                                                                                                                 |

### 4.2 Status badges — how they're derived

Status is **not** just "whatever the backend feels like right now" — it's a deterministic derivation so
it can never flicker or change on an app restart:

| Badge                 | Meaning                                                                                                                                                    | Derivation                                                                                                                                                                             |
| --------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Original**          | Never recalculated since first recorded                                                                                                                    | `LastRecalculatedAt` is null                                                                                                                                                           |
| **Resealed**          | Recomputed _the same calendar day_ it was first recorded (the 90-second quiet-period reseal after a same-day cash edit)                                    | `LastRecalculatedAt` is set, and its ET date == `RecordedDate`                                                                                                                         |
| **Cash Recalculated** | Recomputed on a _later_ day than it was first recorded (a backdated correction reached back and patched this historical row)                               | `LastRecalculatedAt` is set, and its ET date is after `RecordedDate`                                                                                                                   |
| **Pending Reseal**    | _Today's row only._ A cash/portfolio mutation happened moments ago and the system is waiting out its 90-second quiet period before it reseals the snapshot | Computed **live** from the backend's in-memory mutation clock — this is the _only_ status value allowed to depend on live state; every other status is 100% derived from stored fields |

**Reconciliation icon** (next to the status badge) is a separate, simpler signal:

- 🟢 **green check** — internally reconciled: `TotalValue == StocksValue + OptionsValue + CashValue` exactly.
- ⚠️ **warning** — a mismatch was detected (the three components don't sum to the stored total). This is a
  pure arithmetic check, not a live market-price recompute — it exists to catch data corruption, not to
  second-guess whether Stocks/Options are "up to date."
- 🕐 **clock** — shown instead of the check/warning only when the row's status is `Pending Reseal`.

### 4.3 Row expand — detail panel

Click any row to expand it. You'll see:

```
<Date>

Portfolio Components
Stocks                 $xxx,xxx
Options                  $xx,xxx
Cash                     $xx,xxx
--------------------------------
Total                   $xxx,xxx

External flows
<Net external flow, or "None">

Cash ledger events
<every CashItem whose effective date == this row's date, with sign, type, account>
(or "None")

Snapshot audit
Original snapshot:   <RecordedAt>
Last resealed / recalculated: <LastRecalculatedAt>   (only shown if it was ever recomputed)
Reason:              <derived short text based on status>
Cash recalculated:   Yes/No   (Yes iff LastRecalculatedAt is set)
Stocks recalculated: No       (always No — Stocks are never touched by a cash recalculation, by design)
Options recalculated: No      (always No, same reason)
```

**Why "Stocks recalculated: No" is always shown as a static label, not a computed value:** the backend
`RecalculateCashRangeAsync` path is architecturally guaranteed to only ever touch `CashValue`/`TotalValue`
— it has no code path that can write `StocksValue`/`OptionsValue`. Displaying it as a hard-coded "No" is
a direct visual proof of that guarantee, not a database read that could theoretically lie.

> **Known simplification:** the original design language asked for phrasing like _"Cash recalculated from
> Sep 2, 2026"_ (naming the date of the backdated correction that _caused_ this row to be touched). The
> current schema only stores _when_ a row was last recalculated, not _which_ upstream edit triggered it —
> so the panel shows the recalculation timestamp and a generic reason ("Backdated cash correction.")
> instead. Tracking the true origin date would need a new persisted field; flagged, not silently added.

### 4.4 Mobile view

Below 900px viewport width, the table is replaced by a stacked card list (same data, same click-to-expand
behavior) — date/total/%/status badge/icon up top, Stocks/Options/Cash mini-breakdown below.

---

## 5. Cash Ledger tab

The direct audit view of the cash ledger — no SQL needed.

### 5.1 Account subtotals (top strip)

One subtotal per `AccountType` that has at least one ledger entry, plus a **Total Cash** figure on the
right. This must always equal the **Latest Cash** summary card at the top of the page — if it doesn't,
something is wrong (see Troubleshooting).

### 5.2 Table columns

| Column              | Meaning                                                                                                                                                                        |
| ------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Effective Date**  | `TransactionDate` if set, else `AddedAt`                                                                                                                                       |
| **Account**         | `AccountType` (e.g. `TFSA_D_TD`, `Corp_TD`)                                                                                                                                    |
| **Type**            | `CashFlowType` — `OpeningBalance`, `Deposit`, `Withdrawal`, `TradeProceeds`, `TradePurchase`, `Dividend`, `Interest`, `Fee`, `Tax`, `AdjustmentIncrease`, `AdjustmentDecrease` |
| **Amount**          | Signed amount, with a leading `+`/`-`, colored green/red                                                                                                                       |
| **External?**       | `Yes` only for `Deposit`/`Withdrawal` — everything else (trades, dividends, fees, adjustments) is `No` because it's an internal portfolio movement, not new/withdrawn money    |
| **Running Balance** | Cumulative sum for that specific account, computed **client-side** in chronological order (oldest→newest), independent of how the table is currently sorted for display        |
| **Created**         | `AddedAt`                                                                                                                                                                      |
| **Modified**        | `ModifiedAt` if this row was ever edited via **Edit Entry**, else `—`                                                                                                          |
| **Actions**         | Edit pencil — opens the same **Edit Entry** dialog used on the main Portfolio page                                                                                             |

### 5.3 `OpeningBalance` rows are permanently locked

Every row with **Type = OpeningBalance** has its Edit pencil **disabled** (greyed out, with a tooltip
"OpeningBalance rows are migration/admin-only") — for every user, including Admin. This is intentional
and matches the backend, which already rejects any attempt to add, edit, or delete an `OpeningBalance` row
through the normal API. If OpeningBalance editing is ever needed, it must be a **separate** maintenance
operation with its own stronger confirmation + audit trail — not bolted onto this page's Edit flow.

### 5.4 Mobile view

Same breakpoint as Snapshots — stacked cards showing type/amount/account/date/running balance/external
flag, with the same disabled-pencil behavior for `OpeningBalance`.

---

## 6. Admin / Debug Actions (Admin role only)

A visually separated, orange-dashed-border section at the bottom of the page, labeled **Admin / Debug
Actions** — a deliberate signal that these are maintenance operations, not everyday user actions.

**Every action that changes or recalculates data requires an explicit confirmation dialog first — nothing
executes on the first click.** Each dialog has an explicit **Cancel** and an action-specific confirm
button (never a generic "OK").

| Button                         | Confirmation dialog                                                                                                                                                                                                                                                          | What it actually does                                                                                                                                                                             | Endpoint                                                     |
| ------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------ |
| **Record Today Now**           | Title: _"Record today's portfolio snapshot?"_ — _"This will fully recompute today's Stocks, Options, Cash, and Total portfolio values using the current portfolio state and overwrite today's existing snapshot if one already exists. Continue?"_ → **Cancel** / **Record** | Full recompute of Stocks + Options + Cash for today, upserts today's row. Use this to seed today's snapshot if the 4:30 PM background job hasn't fired yet, or to force-refresh everything        | `POST /api/portfoliovaluehistory/record-now`                 |
| **Reconcile Today**            | _"Reconcile today's cash value?"_ — _"This will recalculate today's CashValue and TotalValue from the cash ledger while preserving the existing StocksValue and OptionsValue. Continue?"_ → **Cancel** / **Reconcile**                                                       | Cash-only recompute of _today's already-recorded_ row. Fails with a clear error if no snapshot exists yet today (tells you to use Record Today Now first — no silent fallback)                    | `POST /api/portfoliovaluehistory/reconcile-today`            |
| **Recalculate Cash From Date** | _"Recalculate historical cash values?"_ — _"This will recalculate CashValue and TotalValue from **`<the date you picked>`** through today. Historical StocksValue and OptionsValue will be preserved. Continue?"_ → **Cancel** / **Recalculate**                             | Cash-only recompute for every row from the picked date through today (inclusive) — the same logic that runs automatically after a backdated ledger edit, but manually triggered for a whole range | `POST /api/portfoliovaluehistory/recalculate-cash?fromDate=` |
| **View Ledger Start Date**     | _(no confirmation needed — read-only)_                                                                                                                                                                                                                                       | Displays the accounting boundary date: ledger data before this date is frozen legacy history; on/after it, cash is authoritatively reconstructed from the ledger                                  | `GET /api/cash/ledger-start-date`                            |

After any of the three write actions completes, a **snackbar** (bottom-of-screen toast) shows either a
success message or the server's error message — and the page data refreshes automatically so you see the
result immediately (summary cards, table rows, and the "Last Resealed" timestamp all update).

---

## 7. Backend architecture — the short version

- **`PortfolioValueHistories`** table: one row per (portfolio-wide) trading day. Key audit columns added
  for this feature: `Source` (enum: `EodAuto`, `ManualRecordNow`, `SameDayReseal`, `CashRecalculation`,
  `Migration`) and `LastRecalculatedAt`.
- **Lifecycle of one row:**
  1. `EodAuto` — the background service writes it once, automatically, between 4:30 PM–midnight ET.
  2. If a cash/portfolio mutation happens the _same day_ after that, a 90-second quiet-period timer fires
     a full recompute → `SameDayReseal`, status badge becomes **Resealed**.
  3. If a _backdated_ ledger correction is entered later (any day after), only `CashValue`/`TotalValue`
     get patched → `CashRecalculation`, status badge becomes **Cash Recalculated**.
  4. Admin's **Record Today Now** = `ManualRecordNow` (full recompute). **Reconcile Today** / **Recalculate
     Cash From Date** = `CashRecalculation` (cash-only, same safety guarantee as #3).
- **`GET /api/portfoliovaluehistory/range`** returns enriched DTOs (adds `ExternalCashFlow`,
  `SnapshotStatus`, `HasMismatch` on top of the raw stored columns) — defaults to the last 90 days if no
  `fromDate`/`toDate` is given.
- **`CashItems`** table gained `ModifiedAt`, stamped only by the Edit Entry flow (never by Add/Adjust,
  since those always insert a brand-new row).

---

## 8. Step-by-step test plan

### 8.1 Read-only browsing (any user)

1. Navigate to **Value History** in the top nav.
2. Confirm the 4 summary cards show non-zero, sensible values matching your actual portfolio.
3. Confirm the **Snapshots** tab is selected by default and shows rows sorted most-recent-first.
4. Pick a **From** date a few weeks back, leave **To** empty → confirm older rows disappear.
5. Pick **Snapshot Status = Original** → confirm only rows without a "Resealed"/"Cash Recalculated" badge remain.
6. Click **Reset Filters** → confirm the full list returns.
7. Click any row → confirm the detail panel opens showing the Stocks/Options/Cash breakdown that sums to
   the row's Total, plus the Snapshot Audit section. Click again to collapse.
8. Switch to the **Cash Ledger** tab → confirm the account subtotals sum to the same **Total Cash** shown
   in the summary card at the top.
9. Find any **OpeningBalance** row → confirm its Edit pencil is greyed out/disabled with a tooltip.
10. Find any non-OpeningBalance row → click Edit → confirm the existing Edit Entry dialog opens normally;
    make a small change and save → confirm the row's **Modified** column now shows a timestamp.
11. Resize the browser window below ~900px wide (or open on a phone) → confirm both tabs switch to a
    stacked card layout with the same data and the same expand/edit behavior.
12. If Demo Mode is enabled (toggle in the layout/top bar), confirm every dollar figure on this page is
    masked consistently with the rest of the app.

### 8.2 Admin actions (Admin role required)

13. Log in as a user with the **Admin** role. Confirm the **Admin / Debug Actions** section is visible
    (non-admin accounts should not see it at all).
14. Click **Record Today Now** → confirm the confirmation dialog appears with the exact title/message
    from §6 and **Cancel**/**Record** buttons. Click **Cancel** → confirm nothing happens (no snackbar, no
    data change).
15. Click **Record Today Now** again → this time click **Record** → confirm a success snackbar appears and
    the table/cards refresh (today's row's **Recorded**/**Recalculated** timestamp updates).
16. Click **Reconcile Today** → confirm the dialog text matches §6 exactly → confirm.
    - If today's snapshot already exists: confirm success, and confirm **Stocks**/**Options** for today's
      row are unchanged while **Cash** may update if the ledger changed.
    - To test the failure path: this only triggers if no row exists yet for today at all — hard to force
      manually, but confirm the error message reads _"No snapshot exists for today. Use Record Today Now
      first."_ if you do hit it (e.g. very early in the morning before the background job or any manual
      record has run).
17. Pick a date in **Recalculate cash from**, then click **Recalculate Cash From Date** → confirm the
    dialog message includes the exact date you picked (e.g. \*"...from Sep 2, 2026 through today..."`) →
    confirm → confirm success snackbar and that every row from that date forward keeps its original
    Stocks/Options values while Cash/Total may change.
18. Click **View Ledger Start Date** → confirm it displays a single date (the accounting boundary) without
    any confirmation dialog (it's read-only).

### 8.3 Data-integrity spot checks (optional, for deeper confidence)

19. Pick any row with status **Cash Recalculated** or **Resealed** → expand it → confirm "Stocks
    recalculated" and "Options recalculated" both read **No**, and manually verify (e.g. against a known
    backup or SQL query) that those two figures genuinely didn't change across the recalculation.
20. Pick a day where you know cash moved from a **stock sale** (not a deposit) → confirm that day's
    **External Flow** column reads **$0.00** even though **Cash** went up — this proves internal transfers
    never get misclassified as external flow.

---

## 9. Troubleshooting

| Symptom                                                     | Likely cause                                                                                                                                                                                                                                                                                            |
| ----------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Snapshots tab empty / summary cards show $0.00              | Backend not running, or a query translation error (this exact bug was found and fixed on 2026-09-06 — see the "EF Core translation" note in the implementation history; should not recur, but if it does, check the backend console log for an `InvalidOperationException` mentioning `string.Compare`) |
| Cash Ledger subtotal doesn't match the summary card         | Data was fetched before/after a Reconcile — click Reset Filters or reload the page; if it persists, it's worth checking the ledger for entries dated in the future (which intentionally don't count toward "today's" total yet)                                                                         |
| Admin section not visible                                   | Your account doesn't have the Admin role — this is by design                                                                                                                                                                                                                                            |
| Edit pencil disabled on a row that isn't OpeningBalance     | Should not happen — file a bug if seen                                                                                                                                                                                                                                                                  |
| "No snapshot exists for today. Use Record Today Now first." | Expected error from **Reconcile Today** when no row exists yet for today                                                                                                                                                                                                                                |

---

## 10. Out of scope (by design, not an oversight)

- No manual editing of `PortfolioValueHistory` rows from the UI — ever.
- Mismatch detection is a simple arithmetic check, not a live market-price recompute.
- No Add/Delete of cash entries from this page — use the existing Portfolio page dialogs; this page only
  reuses the **Edit** dialog.
- No bundling of a stock trade + its cash movement into one atomic action (tracked separately, explicitly
  deferred).
- No traceability of _which_ backdated entry triggered a given row's recalculation — only _when_ it happened.
