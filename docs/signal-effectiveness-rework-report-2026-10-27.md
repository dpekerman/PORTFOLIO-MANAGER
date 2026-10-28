# Signal Effectiveness Rework — Implementation Report

Date: 2026-10-27 · Branch: `fix/final-action-cls-brk` · Status: **implemented, not committed or pushed**

## 1. What changed for the user

The Signal Tracker's "Signal effectiveness" section is now independent of the table above it.

- **Window:** a rolling cohort selected by **original signal date**: 30D, **90D (default)**, 180D or All history. Outcomes always use all available price history after each signal, so the window never truncates them.
- **Independent of table filters:** ticker, From/To, Status and My Action never reach it (the endpoint has no such parameters). Signal Type, Investment Role, Source and "Include initial baseline" do apply, and the page says so.
- **Graph and table:** a grouped bar chart of the average return after the signal (5D, 20D, 1M) per signal type, drawn in the same hand-built style as the Portfolio value history chart (no new dependency), followed by a compact responsive table with sample counts (n), "Insufficient history" states, overlap flags and tooltips. Phones show the table as cards and the chart with short labels.
- **Direction:** raw stock returns and direction-adjusted outcomes are kept separate. Sell-direction signals (Trim/Exit/Cut Loss) are plotted direction-adjusted (a later fall counts as positive) with the raw return shown beside them. Types with no defined direction show raw returns only and "Direction not defined". Neither is realized portfolio profit.
- **Coverage is always visible:** signals in the window, signals with price history, and a warning when price history is missing or the statistics are incomplete.

## 2. Methodology (as implemented)

| Topic | Rule |
|---|---|
| Eligible signals | Audited (`PerformanceAudited`), not initial baseline unless the checkbox is ticked, dated on or before today. Closed recommendations (Invalidated, Superseded, Expired, Dismissed, Acted) **count**. Awaiting Data and failed evidence never create events. |
| Horizons | Unchanged from before: 5D = 5th session after the signal date, 20D = 20th, 1M = first session on or after one calendar month later (about 21–23 sessions; related to 20D, not identical). Only closes after the signal date are used. A period that has not completed is excluded, never counted as zero. |
| Favourable 20D % | Direction-aware (up after Buy, down after Sell); not available for Neutral types. |
| Actioned | Recorded My Action (Actioned or Partial). Lifecycle status is not used. |
| Overlap | **Informational only.** An event is flagged when an earlier same-symbol, same-direction event exists within the previous 20 trading sessions of that symbol's exchange calendar (events ordered by date, then id; looks at all audited history, so the window boundary adds no bias). Nothing is excluded and no denominator changes. Statistically, overlapping signals are not independent observations. |
| Identity | One row per recommendation episode (`EpisodeKey`); revalidation never creates a second row. No identity rule was invented. |
| Coverage | Events are read in keyset batches of 1,000 and each symbol's price history is fetched once per request (cached 15 minutes). No 5,000-row cap. If a safety ceiling of 250,000 events were hit, the result is flagged incomplete. Missing price history is reported, not hidden. |

## 3. Changed files

Backend
- `Models/SignalAuditModels.cs`: effectiveness query, result, row and coverage DTOs.
- `Services/SignalsAuditService.cs`: `GetEffectivenessAsync`, overlap flagging, `IncludeEffectiveness` opt-out for the summary.
- `Controllers/SignalsAuditController.cs`: `GET /api/signals-audit/effectiveness`.
- `PortfolioManager.Tests/SignalsAuditServiceTests.cs`: 16 new tests.

Frontend
- `core/models/portfolio.models.ts`, `core/services/signals-audit-api.service.ts` (+ new `.spec.ts`), `core/store/signals-audit.store.ts` (+ spec).
- `features/signal-tracker/`: page `.ts`/`.html`/`.scss`, new `signal-effectiveness-chart.ts` (+ spec).
- `shared/final-action-details-dialog/final-action-labels.ts`: the reworded "did not re-confirm" message (earlier request).
- `e2e/`: `signal-tracker.spec.ts`, `action-center.spec.ts`, `fixtures/signals-audit.ts`, `support/mock-api.ts`.

Compatibility: `GetSummaryAsync` is unchanged by default (the 5 existing summary assertions still pass). The summary calls from the page now send `includeEffectiveness=false`. No Dashboard, export or other API consumer reads that property.

## 4. Test results

| Suite | Result |
|---|---|
| Backend | 808 passed, 0 failed, 3 skipped (the opt-in SQL tests, which passed separately earlier on a disposable LocalDB) |
| Angular `tsc`, dev build and **production build** | Clean; production budget warnings are pre-existing and none concern the tracker |
| Angular unit | 399 passed (50 files) |
| Playwright, desktop, tablet, S21, S23 | **655 passed, 0 failed**, 89 skipped (viewport-conditional, unrelated). 23 tests cover the tracker per project, including the chart, selector, filter independence, direction-adjusted exits, insufficient-history and coverage states, and no horizontal overflow on phones |

New backend tests cover: window by signal date without truncating outcomes; Entry followed by a decline; Exit followed by a decline (raw negative, adjusted positive, favourable); Neutral type; Invalidated, Superseded and Expired counted; unaudited and baseline excluded (and baseline included on request); missing price history reported and not counted as zero; Actioned = recorded action; context filters apply and the query has no ticker/date/status/My Action; overlap informational (none excluded), window-boundary overlap, same-day deterministic ordering; **5,400 events aggregated completely in batches**; no-history; unknown window rejected and other users' signals never read; summary compatibility; read-only (no event modified).

## 5. Historical data protection

- No migration, schema change, event mutation, scan, publish or EOD trigger. The new code path only reads `SignalAuditEvents` and stored price history; a test asserts no event changes.
- No real-database write was made for this feature. Earlier in this session, the orphan automation run 128 was closed on your approval (see the business review report).

## 6. Coverage on the real data, and remaining limitations

1. **Live data is too young to populate the metrics yet.** A read-only count shows 8 eligible signals, all dated 2026-10-23 to 2026-10-27 (4 Active, 2 Dismissed, 1 Acted, 1 Invalidated; 5 Buy, 3 Sell direction). The oldest is 2–3 sessions old, so no 5D, 20D or 1M period has completed. The section will show the chart empty-state wording and "Insufficient history" until signals age: first 5D values appear about 5 trading sessions after the first signals, 20D about a month. This is correct behaviour, not a defect.
2. **The running API must be restarted** before the new section works against live data. The API process started before this change has no `/effectiveness` route, so the page shows "Failed to load signal effectiveness" until you restart it. I did not restart your process, and I did not start a second API instance, because that would run the EOD and alert background services against the live database.
3. I could not verify the live numbers end to end for the same reason (no live API on the new build); the logic is covered by the deterministic tests and the mocked Playwright suite.
4. Price history comes from the existing Yahoo provider (15-minute cache); a first All-history request fetches each distinct symbol once.
5. Overlap flags treat the same symbol and direction across signal types as related. Statistical dependence is reported, not corrected.
6. Longer term, benchmark or buy-and-hold comparison would answer whether the recommendations add value; the app has no benchmark series wired in, so it is out of scope here.

## 7. Manual UAT steps

1. Restart the API (`cd backend\PortfolioManager.Api; dotnet run --launch-profile http`) outside the EOD window; keep `ng serve` running; hard-refresh the browser.
2. Open Signal Tracker. The table shows the newest signal day (From and To set to it) while the effectiveness section shows its own window (90D selected, window dates displayed).
3. Expect "Insufficient history" on current data (see §6.1). Confirm the coverage line reads about 8 signals and the chart empty-state note appears.
4. Click 30D / 180D / All history: only the effectiveness section reloads; the table does not.
5. Change the table's Ticker or From/To: the effectiveness section does not change. Change Investment Role or Source: it does.
6. Check the help button "How these figures are calculated", hover the chart bars for values and n, and tab to a signal type in the chart (screen-reader summary).
7. On a phone-sized window, confirm the chart fits and the table becomes cards.

Screenshots (mocked data, all four device projects) are in the session files folder: `final-eff-desktop-chrome.png`, `final-eff-tablet.png`, `final-eff-mobile-s21.png`, `final-eff-mobile-s23.png`.
