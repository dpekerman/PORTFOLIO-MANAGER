# EOD Automation Pipeline — Readiness Review & Reference Guide

**Date:** 2026-09-08
**Branch:** `develop` (all files still uncommitted, per standing instruction — review manually before `git add`)
**Purpose:** Answer, in one place: (1) is every planned UI/UX item actually done, (2) exactly how the Automation screen and the engine behind it work, (3) is this genuinely safe to rely on unattended while traveling Oct 11–23, and (4) what — if anything — is still not verified before we check this in.

---

## 1. TL;DR verdict

**Code: complete and builds clean (frontend + backend).** All 6 items from today's TODO list are implemented and verified present in the current files (see §2).

**Unattended-travel readiness: NOT yet proven — 3 real gaps found and fixed today, but none of them have been exercised end-to-end on this exact machine yet.** See §7 for the concrete findings (this machine's sleep state, wake timers, and publish status) and §8 for the mandatory pre-travel test plan. Do not treat this as "done" for the travel use case until you've completed §8.

Do **not** check this in as "finished" without reading §9 (honest gap list) first — you asked me not to rubber-stamp it, and I'm not going to.

---

## 2. Did we complete all 6 TODOs?

| #   | Item                                              | Status  | Where                                                                                                              |
| --- | ------------------------------------------------- | ------- | ------------------------------------------------------------------------------------------------------------------ |
| 1   | Format Next run date and add clear hint           | ✅ Done | `config-page.component.html` — `nextRunTime \| date: 'EEE MMM d, h:mm a'` + new "Active window" banner             |
| 2   | Material time picker for Wake time                | ✅ Done | `mat-timepicker` `interval="15m"`, same pattern as EOD Window tab                                                  |
| 3   | Material time picker for Keep-awake override      | ✅ Done | `mat-timepicker` `interval="15m"` + clear (✕) button since it's optional                                           |
| 4   | Error snackbar for automation run failures        | ✅ Done | `automationState.error()` was being set but never rendered anywhere — wired to `MatSnackBar` (app-wide convention) |
| 5   | Adjust polling interval for immediate feedback    | ✅ Done | `interval(3000)` → `timer(0, 3000)` — first status check now fires immediately, not after a blind 3s gap           |
| 6   | Cancel Automation button with confirmation dialog | ✅ Done | See §3 — it exists, but is **conditionally rendered**, which is likely why it looked missing in your screenshot    |

The screenshot's TODO panel showing 5/6 unchecked does not reflect actual code state — I re-verified by grepping the live files just now (all present). That panel appears to be a stale/separate tracker.

**New work added in this session (not on the original TODO list, but directly relevant to your requests):**

- Detailed step-by-step "how it works" explanation panel
- Per-run detailed timeline (timestamps for every step, not just a one-line summary)
- Recent-runs history list (previously fetched but never rendered anywhere)
- **Fixed: Windows wake timers were disabled on this machine** (blocking issue for the travel scenario — see §7)
- **Fixed: backend had never been published** — required for the trigger script's auto-restart fallback (see §7)
- Setup script now programmatically enables wake timers instead of just printing a reminder

---

## 3. How the screen works (field by field)

| Element                                      | Meaning                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| -------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Task installed — Ready / Next run**        | Live status of the Windows Scheduled Task itself (separate from whether automation is _enabled_). "Next run" is when Windows will next fire the trigger.                                                                                                                                                                                                                                                                                                       |
| **Enable / Repair Automation**               | Registers or re-registers the Scheduled Task (one UAC prompt). Re-run this any time you change the Wake time, or after today's fix (see §7).                                                                                                                                                                                                                                                                                                                   |
| **Automation enabled** (toggle)              | Gates only the **Scheduled**-Task-driven path (`/api/automation/trigger`). Does **not** gate "Run Automation Now" or "Test Wake" — those always work for an admin, by design.                                                                                                                                                                                                                                                                                  |
| **Wake / start time (ET)**                   | When the Scheduled Task fires daily. Default 15:15 ET (15 min before the RSI EOD Window opens at 15:30).                                                                                                                                                                                                                                                                                                                                                       |
| **Keep-awake-until override (ET, optional)** | Leave blank to auto-compute: `max(EOD Window End, Value Screener time) + Completion grace`. Only set this if you want to force an earlier/later stop than the computed default.                                                                                                                                                                                                                                                                                |
| **Completion grace (minutes)**               | Buffer added after the last business event, in case Snapshot/Value Screener run a few minutes late.                                                                                                                                                                                                                                                                                                                                                            |
| **Max poll minutes**                         | Safety cap, counted from Wake time — stops watching even if Keep-awake-until is later. **See §7 for why this currently cuts off before Value Screener's default time.**                                                                                                                                                                                                                                                                                        |
| **Active window banner**                     | Live-computed plain-English summary of the two settings above, so the effective schedule is never ambiguous.                                                                                                                                                                                                                                                                                                                                                   |
| **"What happens automatically, in order"**   | Static explanation of the 7 engine steps (see §4) — always visible, not dependent on a run happening.                                                                                                                                                                                                                                                                                                                                                          |
| **Last run (detailed timeline)**             | Appears once any run has ever happened. One row per engine step, with the real timestamp once that step actually happened, in this machine's local time.                                                                                                                                                                                                                                                                                                       |
| **Recent runs**                              | Up to the last 50 runs (paginated on the backend), so you can review every day you were away in one place.                                                                                                                                                                                                                                                                                                                                                     |
| **Test Wake**                                | Non-destructive: DB connectivity + one live quote call only. Never touches RSI/signals/snapshot/cash/transactions. Use this to test wake-from-sleep safely, as many times as you want.                                                                                                                                                                                                                                                                         |
| **Run Automation Now**                       | The **real** production path — same as the Scheduled trigger. Respects all business-time gates (won't force RSI/Snapshot to run early).                                                                                                                                                                                                                                                                                                                        |
| **Stop button**                              | **Only visible while a run is actually in progress** (a blue banner appears at the top of the section body the instant `runInFlight` becomes true). If you don't see it, either no run is currently active, or the click failed before `runInFlight` was set (now surfaced via the error snackbar — see #4 above). Clicking it asks for confirmation, then records the run as `Cancelled` (not a misleading `Success`/`Failed`) once the orchestrator unwinds. |
| **Rotate Secret**                            | Regenerates the local DPAPI-protected trigger secret. Only needed if you suspect it's compromised — routine use never requires this.                                                                                                                                                                                                                                                                                                                           |

---

## 4. How the engine works — full step-by-step

This is now shown verbatim in the UI (the new "What happens automatically" panel), reproduced here for reference:

1. **Wake.** Windows wakes the machine (or the Scheduled Task fires immediately if already awake) at the Wake/start time.
2. **Ensure backend is running.** The Task's action (`run-eod-automation-trigger.ps1`) health-checks `GET /api/health`; if unreachable, it starts the **published** Release build as a detached, hidden process (bounded 60s retry) — this does **not** depend on a terminal or IDE window staying open.
3. **Trading-day guard.** The orchestrator checks the latest `^GSPC` daily bar's date against today (Eastern). Weekends/market holidays record `SkippedNonTradingDay` and stop here — nothing else runs.
4. **Acquire keep-awake lock.** A Windows power request (`PowerCreateRequest`/`SystemRequired`) prevents the machine from falling back asleep mid-run. Never forces the display on.
5. **Refresh market data once.** Calls the same, unmodified `DataRefreshService.RefreshAllAsync()` used elsewhere in the app — refreshes prices for your portfolio + watchlist symbols.
6. **Observe (never duplicate) the three independent background services**, polling every 60 seconds until each is confirmed:
   - **RSI / EOD Signals** — eligible once the RSI EOD Window opens (default 15:30 ET); records how many signals were persisted today.
   - **Portfolio Snapshot** — eligible after 16:30 ET; records the source (Live/Resealed/etc.) once a row appears for today.
   - **Value Screener** — informational only; records its last-run timestamp if it matches today. Never affects overall Success/Failed.
7. **Stop & release.** Polling stops at Keep-awake-until (or Max poll minutes after wake, whichever is first); the keep-awake lock is released; the final `OverallStatus` (`Success`/`PartialSuccess`/`Failed`/`Cancelled`/`SkippedNonTradingDay`) is written.

**What can make `OverallStatus` non-Success:**

- `Failed` — market data refresh failed, **or** Snapshot's window was reached but no row ever appeared by the deadline (a real problem — one snapshot/day is always expected).
- `PartialSuccess` — RSI's window was reached but never got observed as complete before the deadline (informational — zero new signals is still a valid, non-error outcome, but the observation itself was cut short).
- `Cancelled` — you clicked Stop.
- `SkippedNonTradingDay` — correctly not a trading day. This is what you saw in the screenshot ("Last run: SkippedNonTradingDay") for Sept 8's `ManualRunNow` click — **wait, Sept 8 2026 is a Tuesday, a real trading day.** If you see `SkippedNonTradingDay` on an actual trading day, that is a real bug to investigate (most likely `^GSPC`'s latest daily bar hadn't updated yet for "today" at the moment you clicked, if clicked very early/late). Worth a quick look if it recurs.

---

## 5. Testing performed today

- Frontend: `npx ng build` — clean, only pre-existing SCSS budget warnings on files unrelated to this change.
- Backend: compiles clean (verified via `dotnet build` on the Tests project; the API project's own build only fails on the expected file-lock from your running dev instance, zero actual compile errors).
- PowerShell: `setup-eod-automation-task.ps1` re-parsed with `[System.Management.Automation.Language.Parser]::ParseFile` — no syntax errors after the wake-timer fix.
- **Live, direct verification on this machine** (not simulated):
  - `Get-ScheduledTask` — task genuinely registered, `State: Ready`.
  - `powercfg /a` — this machine's actual supported sleep state (see §7).
  - `powercfg /q ... RTCWAKE` — wake timers were **Disabled**, now set to **Enabled** (AC + battery), verified by re-querying after the change.
  - `dotnet publish -c Release -o publish` — run just now; `publish\PortfolioManager.Api.dll` confirmed present.

**Not tested today:** an actual sleep → scheduled wake → automatic run → result-verified cycle. This is the single most important remaining test — see §8.

---

## 6. Answering your specific questions

**"Should I put the computer to sleep around 15:00 and open it back around 17:15 to see results?"**
Yes — that is exactly the intended design, and it's now technically possible (wake timers were off; fixed today). But **do this as a supervised dry run first**, not for the first time while unattended in Croatia. See §8 for the exact test procedure.

**"It's not clear when the task will run"** — fixed: the "Active window" banner now spells out the exact computed start/end times in plain language, and "Next run" is human-formatted.

**Timing question you should know about:** with the defaults (Wake 15:15, Max poll minutes 90), the poll loop's actual cutoff is **16:45** (15:15 + 90 min) — earlier than the computed Keep-awake-until of **17:15** (`max(16:30, 17:00) + 15`). Since 16:45 is _before_ Value Screener's default 17:00 run, the automation log will almost always show Value Screener as `NotObserved` even on a perfectly healthy day — harmless (informational field only, doesn't affect `OverallStatus`), but if you want it accurately reported, raise **Max poll minutes** to ~125.

---

## 7. Critical findings for the travel scenario (verified directly on this machine)

I ran real diagnostics rather than giving generic advice. Three concrete issues found, all now fixed:

### Finding 1 — This machine only supports Modern Standby, not classic S3 sleep or Hibernate

```
powercfg /a
  Available:     Standby (S0 Low Power Idle) Network Connected
  Not available: S1, S2, S3 (blocked by firmware/S0 support), Hibernate (not enabled), Hybrid Sleep
```

**Implication:** "Sleep" on this laptop is Modern Standby, which keeps networking alive during standby (good — Yahoo Finance calls will work) but draws continuous small power (unlike S3's near-zero draw) and has **no Hibernate fallback** if the battery runs critically low while asleep. **Practical rule for the trip: keep the laptop plugged into AC power whenever you expect it to wake unattended.** Don't rely on battery alone for a multi-day unattended cycle.

### Finding 2 — Wake Timers were disabled (would have silently prevented every scheduled wake)

```
powercfg /q SCHEME_CURRENT SUB_SLEEP RTCWAKE
  Before: AC=Disable, DC=Disable   ← Scheduled Task's -WakeToRun would never actually wake the machine
  After:  AC=Enable,  DC=Enable    ← fixed directly on this machine, verified by re-query
```

`setup-eod-automation-task.ps1` previously only _printed_ a reminder to check this manually — it's easy to miss, and clearly was missed. The script now sets this automatically (via `powercfg /setacvalueindex` / `/setdcvalueindex`) every time "Enable/Repair Automation" is run, so a future OS reinstall or power-plan reset won't silently reintroduce this.

### Finding 3 — Backend had never been published (needed for auto-restart if it's not already running)

`publish\PortfolioManager.Api.dll` did not exist. Fixed just now via `dotnet publish -c Release -o publish`. This matters because the trigger script's fallback path (start the backend if `/api/health` doesn't respond) depends on this exact file. **If you only ever run the backend via `dotnet run` in a terminal, that process does not survive a full log-out or reboot — only Modern Standby, where it stays resident.** For a 12-day unattended trip, prefer running the **published** build directly (or just let the trigger script manage it) over keeping a dev terminal open the whole time.

---

## 8. Mandatory pre-travel test plan (do this before Oct 11 — not after)

1. **Re-run "Repair Automation"** once now, so the Scheduled Task picks up today's wake-timer fix (it re-registers the task and re-applies the `powercfg` settings).
2. **Stop your `dotnet run` terminal.** Instead, start the backend from the published build once (`dotnet publish\PortfolioManager.Api.dll` from that folder, or just let step 3 do it for you).
3. **Supervised dry run, on a normal workday, while you're home:**
   - Manually put the laptop to sleep (Start → Sleep, or close the lid if that's configured to sleep) a few minutes before the Wake time.
   - Leave it plugged in.
   - Come back at/after Keep-awake-until and confirm: (a) the screen shows a new `Scheduled`-triggered entry in Recent runs, (b) `OverallStatus` is `Success` or a sensible `PartialSuccess`, (c) check Event Viewer → Windows Logs → Application → Source `PortfolioManagerApi` for any warnings.
4. **Repeat step 3 for at least 2–3 consecutive trading days.** One clean run is a good sign; three in a row is real confidence.
5. **Confirm the "SkippedNonTradingDay on a real trading day" question from §4** doesn't recur during this test window.
6. Only after 2–3 clean unattended cycles: consider it safe to leave unattended for the full 12-day trip.

**During the trip itself:**

- Keep the laptop plugged in.
- Do not log out (only let it sleep) — "Run only when user is logged on" means a full logout silently disables everything, with no error anywhere.
- If you have any remote-access setup for this machine (check `docs/local-network-mobile-access.md` if that covers remote access, not just local network), periodically check the Recent Runs list — that's now your single source of truth for "did today actually run."

---

## 9. Honest gap list — what's still not verified

- **No real sleep → wake → run → verify cycle has ever been executed on this machine.** Everything above is individually verified (task registered, wake timers on, publish exists) but never exercised together, end-to-end, unattended.
- **Lid-close behavior was not confirmed** — `powercfg /q SCHEME_CURRENT SUB_BUTTONS LIDACTION` returned no setting on this device (likely hidden/not exposed on this SKU). If you close the lid to "sleep" rather than using Start → Sleep, verify separately that lid-close doesn't do something else (e.g., "Do nothing," which would mean it never actually sleeps, or a shutdown-like state that a wake timer can't wake from).
- **The `SkippedNonTradingDay` result seen for a `ManualRunNow` click on Sept 8** (a real Tuesday) was not root-caused today — flagged in §4, worth a quick check if it recurs.
- **Cancel/Stop has unit-test coverage on the backend but has not been manually exercised against a real in-flight `Run Automation Now` from the browser this session.**
- **Multi-day unattended reliability** (does the machine actually keep the health check + trigger cycle working every single day without any manual nudge) is inherently something only a real multi-day trial can prove — no amount of code review substitutes for this.

## 10. Final verdict

**Code:** ready to check in — it's complete, builds clean, and every requested UI/UX item is implemented and verified in the actual files.

**Feature readiness for the Oct 11–23 trip:** **not yet** — do §8 first. The three fixes made today (wake timers, publish, script hardening) were necessary but not sufficient; only a real supervised dry-run cycle proves the whole chain works on this specific machine. Please don't rely on this for the trip until you've seen at least 2–3 clean unattended cycles with the laptop actually asleep and waking on its own.
