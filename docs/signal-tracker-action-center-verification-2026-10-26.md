# Signal Tracker + Action Center: verification runbook (2026-10-26)

## Why Action Center shows 0 / "Validation pending (6)" right now
Expected before tonight's EOD. Action Center lists only recommendations that are `Status = Active`, open, and have `LastValidatedAtUtc`. Only a **complete** EOD pass sets that stamp.
Complete means `IsEodPass`, refresh and RSI both `Succeeded`, and the portfolio snapshot taken after the run started. Manual "capture" never validates.
All 7 tracker rows are Friday 2026-10-23 `Initial baseline` rows. The tracker shows 7 and Action Center says 6 because CP.TO is `Acted` (not Active), so it is not an open decision.

## A. Go / no-go gates (before 5 PM ET)
- Backend running on :5000 and reachable from the UI. Machine will not sleep (the run takes a power request, but the PC must be on).
- Scheduled task exists (Config > Automation > task status) and "Automation enabled" is on.
- An Admin user exists (the run's owner). Yahoo is reachable (Config > Automation > Test Wake succeeds).
- `dotnet ef database update` is applied.
- Do not restart the backend during the run window.

## B. After the EOD run
1. Run `scripts/verify-signal-tracker-action-center.sql`:
   - Q1: latest run `Success`, Refresh + RSI `Succeeded`, no `ErrorStep`.
   - Q2: `LastEodEvaluationComplete = true` and `LastCompleteEvaluationUtc` is tonight.
   - Q3/Q4: open FA rows have `LastValidatedAtUtc` = tonight (`NeverValidated = 0` for Active rows).
   - Q5, Q6, Q7: **0 rows** each.
2. UI reconciliation: Dashboard summary = Action Center chips (Entry/Add, Trim/Exit, Risk) = the Active, validated, actionable rows in Signal Tracker (Status filter Active). Banner shows no "Validation pending".
3. Row spot-check (each shown row):
   - Ticker/role/source match the portfolio or watchlist.
   - Signal price and date match the EOD close.
   - Today vs Signal % = (latest close - signal price) / signal price.
   - Currency is correct (CAD for .TO, USD for US tickers).
4. Rows that vanished after EOD must have `Status` Invalidated/Superseded/Expired with a `StatusReason` (Q8). That is the correct "condition no longer holds" behaviour, not data loss.

## C. If the banner is not "Validated" afterwards
| State | Meaning | Action |
|---|---|---|
| Pending | Rows not yet confirmed by a complete pass | Check Q1; if the run failed or was partial, Config > Automation > Run Now (same path, respects windows), then recheck |
| Unavailable | Latest pass incomplete/failed, or last good pass older than 1 weekday | Fix the cause shown in Q1 `ErrorStep`, rerun |
- Never use manual capture to "force" validation.
- Pending/Unavailable with 0 items is a fail-safe, not an all-clear.

## D. Verified today (pre-flight)
- Backend: full suite 421 passed, 0 failed (3 skipped SQL Server tests). Built to a temp artifacts path because the live API (PID 23400) locks `bin\`.
- Frontend: open-decisions store, portfolio-actions-widget, and signal-tracker specs: 24 passed.
- Code review: Action Center and dashboard summary share `OpenDecisionsService`, so counts reconcile by construction. Lifecycle only changes on complete passes. Incomplete or unknown-symbol passes keep state.
- Playwright e2e was not run because port 4200 is held by the live dev server and the config does not reuse it. Stop the dev server first (or run in CI).
