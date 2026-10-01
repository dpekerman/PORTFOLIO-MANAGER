# Fix B — Auto-Link Cash to Portfolio Buy/Sell — Decision Plan (DRAFT, not implemented)

**Status:** Planning only. Nothing in this document has been built. Review, edit, add scenarios,
then hand back for implementation.

**Why this exists:** Fix A (applied 2026-09-26) corrected one historical data mistake (a misdated
cash ledger row) and manually added one missing cash offset for a stock purchase. It does not
stop this from happening again — `PortfolioService` and `CashService` are still completely
independent. Every future buy/sell still requires a separate, manual Cash Ledger entry or the
dashboard totals will double-count money. Fix B is the proposal to make that automatic.

---

## 1. Core question to answer first

Should buying/selling a stock or option **automatically** create a matching Cash Ledger entry,
or should the app instead just **warn/remind** the user to add one manually?

- **Auto-create** removes the manual step entirely but requires the app to guess the right cash
  account and handle every edge case below correctly, every time, with no human review.
- **Warn/remind only** (e.g. a confirmation dialog: "This will need a $21,297 cash withdrawal from
  Corp_TD — add it now?") keeps a human in the loop and is much simpler/safer to implement, at the
  cost of still relying on someone to click "yes."

**➡ Your answer:** _(fill in)_

---

## 2. Scenarios to decide on (please annotate each with a decision, or "Liz to decide")

### 2.1 Account matching

- A `PortfolioItem`/`OptionItem` has an `AccountType` (e.g. `Corp_TD`, `TFSA_D_TD`, `Margin_L_TD`).
  Should the auto cash entry always draw from the **same** `AccountType`'s cash?
- What if that account doesn't exist yet in `CashItems`, or its `AccountType` string doesn't
  exactly match any existing cash row (typo-prone, no FK today)?
- **Decision:** _(fill in)_

### 2.2 Insufficient cash / negative balances

- If the matching account's cash balance would go negative after the deduction (e.g. buying on
  margin, or before a deposit is recorded), should the app:
  a) allow it silently (negative cash = margin used, tracked as-is),
  b) allow it but show a warning,
  c) block the trade entry until cash is added?
- **Decision:** _(fill in)_

### 2.3 Manual positions (`IsManual = true`)

- Manual positions use `ManualMarketValue`, not a live quote, and may represent assets not
  actually funded from this ledger (e.g. RSUs, employer stock, something tracked for visibility
  only). Should manual positions be **excluded** from auto cash-linking by default?
- **Decision:** _(fill in)_

### 2.4 Selling / closing a position (`TransactionType = CLOSE`)

- Should closing a position auto-**add** proceeds back to cash (shares × `ClosingPrice`)?
- Do we already have a real example of this working correctly? (Found in DB: rows 40-42 in
  `CashItems`, `CashFlowType = TradeProceeds`, appear to be manually entered today for past sells —
  worth checking with real data whether existing sells were ever cash-linked at all.)
- **Decision:** _(fill in)_

### 2.5 Options trades

- `OptionItems` (`MarketPrice × NumberOfContracts × 100`) — same question as 2.1/2.4 but for
  options open/close. Options premium collected (selling to open) vs. paid (buying to open) has
  opposite cash direction from a simple stock buy — needs its own sign rule.
- **Decision:** _(fill in)_

### 2.6 Partial fills / averaging into a position

- If a `PortfolioItem` is later **edited** (e.g. shares increased via an average-cost update rather
  than a new row), does the app correctly detect "shares went up, so cash should go down by the
  incremental cost," or does it only handle brand-new `OPEN` rows?
- **Decision:** _(fill in)_

### 2.7 Backdated entries

- If a user enters a trade with `OpenDate` in the past (catching up on data entry days/weeks
  later), should the auto cash entry be dated to match `OpenDate` (triggering the same historical
  `RecalculateCashRangeAsync` ripple we just did manually in Fix A), or always dated "today"?
- **Decision:** _(fill in)_

### 2.8 Edits and deletes

- If a portfolio buy is later edited (wrong price/shares corrected) or deleted entirely, should
  the linked cash entry auto-update/auto-delete with it? This requires a way to associate one
  `CashItem` with the `PortfolioItem`/`OptionItem` that created it (no such link exists in the
  schema today — would need a new nullable FK column).
- **Decision:** _(fill in)_

### 2.9 Multi-currency

- Are any symbols/accounts in a different currency than their cash account (e.g. a USD stock held
  against a CAD cash account)? If so, what FX rate/source should convert the cash deduction?
- **Decision:** _(fill in)_

### 2.10 Bulk/import operations

- Migration/import endpoints (`CashService.RestoreAsync`, any bulk portfolio import) bypass the
  normal add/update flow. Should auto-linking apply there too, or should bulk imports stay
  cash-agnostic (current behavior) with a manual reconciliation step afterward?
- **Decision:** _(fill in)_

### 2.11 Multiple users / shared accounts

- Some `CashItems`/`PortfolioItems` rows have `UserId = NULL` (shared/legacy) while others are
  scoped to a specific user (e.g. `a250c394-...`). If two users can see the same `Corp_TD` account,
  whose action creates the auto cash entry, and does it matter?
- **Decision:** _(fill in)_

### 2.12 Retroactive fix for existing data

- Should Fix B include a one-time "reconciliation report" comparing every historical
  `PortfolioItems` OPEN/CLOSE row against `CashItems` to find other already-missing offsets (like
  today's MCD.TO/BOFA.TO), or is Fix A's manual correction considered a closed, one-off case?
- **Decision:** _(fill in)_

---

## 3. Wife's additional scenarios

_(space reserved — add any real trading habits/edge cases not covered above, e.g. dividend
reinvestment, transfers between her own accounts, joint vs. individual accounts, etc.)_

-
-
- ***

## 4. Out of scope for this plan (explicitly not addressed here)

- Any UI/UX design for how a warning dialog or auto-entry confirmation would look.
- Actual code implementation, migrations, or tests — this is a decisions-only document.
- Whether to also add a periodic automated "mismatch detector" background job (separate idea, not
  required for Fix B itself, but could reuse the same reconciliation logic from §2.12).

---

## 5. Next step

Once every "Decision" line above is filled in (by you and Liz), hand this file back and it becomes
the spec for implementing Fix B.
