# PM-029 Gate 1 — functional delivery

**Status:** Safe Gate 1 changes are implemented and focused tests pass. PM-029 remains
open. This is not live-data validation or owner UAT; Gate 2 has not started.

## What changed

- **Sort recovery:** Trading Opportunities now has a “Restore default score sort” action.
  It resets only the active perspective to Swing Score DESC or Strategic Score DESC;
  saved filters and the other perspective's custom sort are preserved.
- **Listing names:** New snapshots prefer exact-symbol Portfolio names, then exact-symbol
  Watchlist quote names, before index/scanner/value-description fallbacks. Read-only
  latest/detail reads also project verified local Portfolio/Watchlist names onto legacy
  snapshots. This does not rewrite snapshots, scoring inputs, identity, or financial
  evidence; mismatched quote symbols are ignored, and an unverified underlying name is
  suppressed for wrappers.
- **FINAL ACTION safety:** Watchlist and universe entry decisions now pass through the
  authoritative allocation gate. Adds are non-actionable (“Add / Needs Review”) when
  valuation is unreliable or the consolidated portfolio denominator is missing or
  non-positive. Risk-reducing and informational decisions remain actionable when
  their evidence is valid; unavailable allocation cannot itself justify a
  concentration-driven action. No persisted decisions were recalculated.

Swing-v2 and Strategic scoring, ETF deferral, Market R resolution, and advisory Setup
Quality already use the existing engines and mappings; this change adds no scoring or
valuation model and does not activate the unapproved Setup Quality grades.

## Current evidence and blockers

The latest existing read-only verification is summarized in
[pm029-final-functional-implementation-review.md](./pm029-final-functional-implementation-review.md);
its data was not refreshed or re-queried for this change.

- Stored 224-row snapshot: **pm029-v1**; Swing has **5 numeric scores / 224** (219
  Insufficient, 2 Provisional, 3 Verified); Strategic has **204 numeric / 224** (20
  Insufficient, 80 Provisional, 124 Verified).
- Bounded current-source replay: NPI.TO Swing **59 Provisional**; NVDA Swing **6
  Provisional** and Strategic **27 Provisional**; SMH is **Not evaluated — ETF scoring
  deferred**. NPI.TO Strategic remains insufficient (`MissingEssential:T7`); no
  valuation evidence or thresholds were fabricated.
- The stored snapshot had **181/224 missing or ticker-only names**. The Portfolio vs
  Watchlist source split and the exact remaining legitimately unnamed count could not
  be queried: no SQL Server is configured in this environment. The new fallback is
  covered by tests, not by a live-database count.
- Existing actual samples had no verified dynamic stop, so Market R and Setup Quality
  remain unavailable rather than being fabricated. Setup Quality grade activation
  still requires policy approval.
- No complete current-session refresh was run. The stored Swing snapshot remains
  pm029-v1 until an authorized refresh; the bounded replay is not a replacement.

## Verification

- Focused .NET Gate 1 tests: **170 passed, 0 failed, 0 skipped** (scoring, replay,
  setup quality, FINAL ACTION allocation, opportunity read/refresh safety).
- .NET API build to isolated output: **passed**, 0 warnings/errors.
- Angular production build: **passed**. It reports existing SCSS size-budget warnings
  outside the changed files.
- Angular opportunity sort/view tests: **38 passed**.
- No live Playwright, scanner endpoint, migration, seed, protected refresh, or service
  restart was performed. Tests used isolated/in-memory data; no production database
  was written. An existing API process held its normal build outputs, so validation
  used an isolated output directory.

## Owner UAT checklist

After the updated UI/API are available, open **Trading Opportunities**:

1. Select **Strategic**; confirm highest numeric Strategic Score first, unavailable
   scores last. Change the sort, restore default, and confirm only that perspective
   resets.
2. Select **Swing**; confirm the equivalent Swing ordering and independent preference.
3. Check symbols with local Portfolio/Watchlist names in the two-line Symbol cell on
   desktop and handset; confirm wrappers do not show an unverified underlying name.
4. Check the stored snapshot version and score status. Do not treat pm029-v1 rows as
   Swing-v2 results.
5. For each row, compare Market R to the displayed Entry/Stop/Target evidence; where
   setup evidence is absent, confirm N/A and the explanatory details.
6. Confirm Setup Quality explains unavailable evidence and does not display an
   unapproved grade.
7. Confirm ETFs/funds say “Not evaluated — ETF scoring deferred.”
8. Confirm new-position FINAL ACTION is not actionable when allocation evidence is
   unavailable; confirm informational and valid risk-reducing actions remain visible.
9. Check Strategic missing-evidence explanations, including NPI.TO; distinguish
   Insufficient from a genuinely low numeric score.

### Approval boundary

No automatic restart or refresh was performed. The current snapshot is immutable and
remains pm029-v1. An authorized refresh writes OpportunitySnapshots (and prunes rows
beyond the five retained snapshots), OpportunityRefreshState, and potentially
ValueScreenerSnapshots with `Origin = Universe`; this controlled refresh needs approval
before execution. Confirm startup side effects before restarting the existing API.

Stop here for manual Gate 1 UAT. Do not activate Portfolio Fit, run migrations, or
begin Gate 2 before owner approval.
