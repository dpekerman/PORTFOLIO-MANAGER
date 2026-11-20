# PM-029 Gate 1 — controlled real-data integration

**Status: refresh completed; PM-029 stays OPEN until your manual UAT.** Snapshot 9 (`pm029-v2`,
session 2026-11-13, Succeeded, computed 2026-11-16 01:38 UTC) is now the latest. Sections A–F below
describe the state before the refresh; section G has the measured post-refresh results.

## A. Actual environment

| Item | Verified value |
|---|---|
| Branch / HEAD | `feature/pm029-gate1-functional-delivery` @ `868f2de` (develop), uncommitted changes, nothing staged/pushed |
| SQL Server / DB | local default instance `MSSQLSERVER` (`HOME-LAPTOP`) / `PortfolioManagerLocal`, Windows auth (`appsettings.json`). The earlier "no server" result came from the VS Code MSSQL tool having no saved connection, not from SQL Server being unavailable |
| Running API | `PortfolioManager.Api.exe` PID 37060, port 5000, started 19:49, built 17:11. Its binary already contains `pm029-v2`, but it predates the fixes in this branch |
| Migrations | 54 on disk = applied; latest `20261114154319_AddPm029TradingOpportunities`. No pending migration, so startup `MigrateAsync()` is a no-op |
| Latest snapshot | Id 8, `pm029-v1`, session 2026-11-13, computed 2026-11-15 10:38 UTC, Succeeded; retained Ids 4–8 |
| Refresh state | last attempt 2026-11-15 12:33 UTC `Failed` ("Refresh was cancelled"), last successful = Id 8 |
| Backup | daily full backup 2026-11-15 14:30 ET, `D:\PORTFOLIO-MANAGER-SQL-BACKUP-ALL\DAYLY_BACKUP\2026-11-15\` |

Stored snapshot 8 baseline (matches the historical report exactly): 224 listings; Swing numeric
**5** (Insufficient 219 / Provisional 2 / Verified 3); Strategic numeric **204** (Insufficient 20 /
Provisional 80 / Verified 124); Portfolio Fit unavailable **224**; missing or ticker-only names
**181**; setup `ValidCandidate` 5; no ETF deferral rows (v1). Current code is `pm029-v2`
(`OpportunityScoringConfig.Version`).

## B. Real scoring evidence

- **Why Swing is 5/224 in the stored snapshot:** 219 rows carry `MissingEssential:S6`. That is the
  retired v1 stop/setup factor. The current engine has only S1–S5 and does not read Manual R, saved
  stops, Market R, Portfolio Fit, or ownership. Re-scoring is therefore expected to raise coverage
  substantially, but this has not been measured.
- **Not measured: full-universe Swing-v2 / Strategic coverage.** No stored same-session raw scan
  exists for the 224 listings (`RsiScanSnapshots` holds one 29-symbol scan; the opportunity snapshot
  stores derived indicators only). A faithful replay needs about 224 Yahoo chart/benchmark calls,
  which is beyond the earlier bounded capture. The proposed refresh in section E is the evaluation
  itself (see E, option 2).
- **Bounded actual-market evidence (earlier verified, not re-run today):** NPI.TO Swing 59
  Provisional; NVDA Swing 6 Provisional, Strategic 27 Provisional; SMH `Not evaluated — ETF scoring
  deferred`. NPI.TO Strategic stays Insufficient (`MissingEssential:T7`): no verified valuation
  evidence, and none is estimated. ETF handling needs a confirmed instrument type; unknown is not
  treated as ETF.
- **Strategic / valuation freshness:** Universe fundamentals (26 TSX60-only symbols) were analyzed
  2026-11-14 17:25 UTC, all within the 7-day limit.

## C. Application fixes (this branch)

| Area | Result |
|---|---|
| Company names | Read-only projection now uses exact-symbol Portfolio, then Watchlist, then **TSX60 `IndexConstituents`** names (new in this pass). Measured on stored snapshot 8: **224/224 resolve** — Portfolio 46, Watchlist 152, other existing (index/stored) 26. Before: 43/224. Remaining missing: **0**. A wrapper row with no verified exact-symbol name shows its ticker, never an underlying name. Snapshot JSON is not rewritten |
| Sorting | "Restore default score sort" resets only the active perspective. Numeric score-descending with N/A last and symbol tie-break is covered by existing tests |
| Market R | No integration defect found (see below). Reason text clarified: "Scanner stop missing" → "No active signal stop (none is invented)" |
| Setup Quality | Calculator unchanged. Real rows have no stop, so it is `Unavailable`; grading policy stays unapproved/inactive |
| FINAL ACTION | New Watchlist/universe entries are non-actionable (`Add / Needs Review`) when allocation or the portfolio denominator is unavailable. Owned adds already were |

**Market R classification (stored snapshot 8, all 224):**

| Cause | Rows |
|---|---|
| Valid candidate — Market R computed (BTCC.TO, ETHH-B.TO, LAC.TO, PBR, TSU.TO) | 5 |
| No active staged-signal stop — Watchlist/Portfolio | 184 |
| No stop — TSX60-only (read-only scan skips staged enrichment by design) | 26 |
| Staged stop at/above entry (price already through the stop) | 7 |
| Stop outside the allowed ATR/risk volatility band (NVDA, NVDA.TO) | 2 |

`DynamicStopLoss` exists only for a tracked staged oversold/overbought signal
(`ExtremeLow − 1.5×ATR`, `StagedSignalService`). All 210 missing-stop rows are therefore the approved
"never fabricate a stop" rule, not a mapping defect. **Product decision needed:** if Market R should
exist for more than staged-signal symbols, a new approved stop methodology is required; none was
invented here.

## D. Remaining blockers (demonstrated)

1. The UI shows legacy `pm029-v1` until the approved refresh runs.
2. A restart is required to load these fixes. In Development the RSI alert service runs its
   persisting scan about 15 s after startup regardless of market hours (writes `RsiScanSnapshots`,
   staged signals, channel data, EOD signals, and may send alert email). Approval is needed.
3. Market R stays sparse (14/224 have any stop) without a new stop policy.
4. Setup Quality grading and Portfolio Fit remain unapproved (Gate 2).

## E. Controlled-refresh approval request

**Inventory of writes (traced, including indirect paths).** `RefreshAsync`:
`FinalActions.EvaluatePreviewAsync` and `ReadOnly` scans write nothing (no staged signals, channels,
or mapping writes). Writes are only:

| Target | Effect |
|---|---|
| `OpportunitySnapshots` | +1 row; keeps 5 per user, so **Id 4 is deleted** (computed 2026-11-14 17:46 UTC; Ids 5–8 stay) |
| `OpportunityRefreshStates` | one row updated (Running → Succeeded/Partial/Failed, `LastSuccessfulSnapshotId`) |
| `ValueScreenerSnapshots` (`Origin=Universe`) | **none expected**: all 26 universe-only results are < 7 days old. If due, +1 row and Universe retention prune to 5 (only 1 exists, nothing at risk) |
| Scanner, dashboard, Portfolio, Watchlist, holdings, transactions, cash, options | not written |

Failure behavior: the new snapshot is saved first and pruned after, so a failed refresh never removes
the last good row. A `Partial` result is still the latest snapshot.

**Proposed operation (please approve each step or reject):**

1. **Recovery copy:** `BACKUP DATABASE PortfolioManagerLocal ... WITH COPY_ONLY` to
   `D:\PORTFOLIO-MANAGER-SQL-BACKUP-ALL\PM029_PRE_V2_REFRESH\`, plus a JSON export of
   `OpportunitySnapshots` Id 4 and the `OpportunityRefreshStates` row. (Writes files only.)
2. **Restart:** stop PID 37060 and run `dotnet run --launch-profile http`
   (`backend\PortfolioManager.Api`, port 5000). Side effects: no-op migration check; the
   development RSI alert scan after ~15 s; the EOD value service after 45 s; the missed-run watchdog
   after 45 s (email only). Today is Sunday, so the 5 PM weekday Value Screener job will not fire.
3. **Refresh:** in the UI click **Refresh analysis** (`POST /api/opportunities/refresh`). It acquires
   about 224 symbols from Yahoo with the existing throttling (3 per batch, 1.5 s pause) plus sector
   benchmark history, expected to take a few minutes.
4. **Expected output:** snapshot `pm029-v2`, session 2026-11-13, Status Succeeded or Partial.

**Option 1 (recommended):** steps 1–4. **Option 2:** skip the restart and refresh on the running
binary. It scores v2 but lacks the FINAL ACTION entry gate and name projection. Not recommended.

**Rollback:** delete the new `OpportunitySnapshots` row, re-insert the exported Id 4, and restore
the saved `OpportunityRefreshStates` row. Full restore is possible from the COPY_ONLY backup to a
side database.

**Verification (read-only SQL after the refresh):**

```sql
SELECT Id, ScoringVersion, DataAsOfSession, ComputedAtUtc, Status, LEN(SnapshotJson) FROM OpportunitySnapshots ORDER BY Id;
SELECT * FROM OpportunityRefreshStates;
SELECT Origin, COUNT(*), MIN(Id) FROM ValueScreenerSnapshots GROUP BY Origin;
```

Then recompute the coverage tables from the new snapshot JSON: Swing/Strategic numeric and status
counts, name coverage, ETF `deferred` count, setup kinds, `fit`, and FINAL ACTION actionable entries.

## F. Manual UAT after the refresh

1. Trading Opportunities → banner shows `pm029-v2`, session 2026-11-13, no legacy warning.
2. **Swing** tab: numeric Swing Score is high→low, N/A last; compare numeric count with the SQL result.
3. **Strategic** tab: same ordering. Change a sort, click **Restore default score sort**, and confirm
   the other tab's sort is unchanged.
4. Symbol column: ticker with the company/fund name below it for all rows, including the 26 TSX60-only
   symbols (CCO.TO, FTS.TO, TRI.TO).
5. SMH and other confirmed ETFs: "Not evaluated — ETF scoring deferred".
6. NPI.TO: Swing numeric; Strategic Insufficient with missing T7. NVDA: Swing and Strategic numeric.
7. Market R: BTCC.TO, ETHH-B.TO, LAC.TO, PBR, TSU.TO show values; NVDA shows N/A with "Stop outside
   volatility band"; a TSX60-only row shows "No active signal stop".
8. Setup Quality shows Unavailable with a reason, never an unapproved grade.
9. FINAL ACTION: watchlist/universe entries are not actionable while allocation is unverifiable
   (mixed CAD/USD), while exit/trim and informational actions remain.

## G. Post-refresh results (measured on snapshot 9, SELECT-only)

An earlier attempt at 01:32 UTC was recorded as "Refresh was cancelled" (as was one at 12:33 UTC):
the API was replaced mid-scan, and the controller also tied the multi-minute refresh to the browser
request. **Fixed:** `OpportunitiesController.Refresh` now uses the application-stopping token, so a
closed tab, reload or proxy timeout no longer cancels it (the single-flight 409 is kept; two tests
added). The API currently running predates this fix; restart it at your convenience.

| Measure | v1 (snapshot 8) | **v2 (snapshot 9)** |
|---|---|---|
| Swing numeric | 5 / 224 | **199 / 224** (Verified 126, Provisional 73, Not evaluated 25, Insufficient 0) |
| Strategic numeric | 204 / 224 | **192 / 224** (Verified 124, Provisional 68, Insufficient 7, Not evaluated 25) |
| Swing ≥ 70 / ≥ 50 | n/a | 58 / 104 (max 89, median 53) |
| Company names missing | 181 | **0** |
| Stale or non-Ok rows | – | 0 (all rows use session 2026-11-13) |
| Market R computed | 5 | 5 (BTCC.TO 7.7, ETHH-B.TO 9.0, LAC.TO 24.0, PBR 4.1, TSU.TO 6.6) |
| Actionable FINAL ACTION entries | – | **0** (e.g. ETHH-B.TO, KHC = `Add / Needs Review`) |
| Portfolio Fit | Unavailable 224 | Unavailable 224 (Gate 2) |

- **Retention:** snapshot 4 was pruned as planned; 5–9 remain. `ValueScreenerSnapshots` untouched.
  Recovery files: `D:\PORTFOLIO-MANAGER-SQL-BACKUP-ALL\PM029_PRE_V2_REFRESH\`.
- **Spot checks:** Swing is independent of stops (AAPL 86 with no setup). NPI.TO Swing 76 Provisional,
  Strategic Insufficient (T7, 4 rows share this reason). NVDA Swing 6, Strategic 24.
- **Why 73 Provisional:** 15 rows have an unmapped sector benchmark (ATD.TO, BCE.TO, BYD.TO, CCA.TO,
  CCL-B.TO, CPH.TO, CTC-A.TO, DR.TO, GIL.TO, MG.TO, MTY.TO, QSR.TO, RCI-B.TO, T.TO, WELL.TO); most
  others lack only the 2-point Fibonacci-zone item (97.5% complete).
- **Strategic Insufficient (7):** CDE.TO, SKHY, SPCX (no SMA200 history), NPI.TO, PSLV.TO, SPPP.TO,
  XNDU.TO (no verified valuation factors). None were estimated.

### Decisions for you
1. **CDRs flagged as ETF (10 rows):** Yahoo labels CAD-hedged single-stock CDRs as ETF, so they are
   deferred with the real ETFs: BOFA.TO, HD.TO, IBM.TO, MCD.TO, MRK.TO, NFLX.TO, NKE.TO, NOVO.TO,
   SPGI.TO, UBER.TO. The other 15 are true ETFs/funds. Whether CDRs should get a Swing score
   (technical only) is a policy change, so I did not alter it.
2. **Sector mapping:** the 15 unmapped symbols need a reference-data mapping (Admin screen), not code.
3. **Market R coverage** (stop methodology) and **Setup Quality grading**: unchanged, see section C.
4. Your saved view has "show unavailable" on, which is why N/A rows appeared under "Attractive (70+)"
   in the old screenshots. Turn it off in Filters for a score-only list.

### Follow-up on decisions 1 and 2 (owner questions)

**1. CDRs flagged as ETF.** The ETF flag is the instrument type Yahoo returns for the CDR listing itself
(these CIBC receipt units are typed like funds). The Portfolio and Signal Tracker avoid this by analysing
the *underlying* through `SecurityAnalysisMappings`; Opportunities already uses the same resolver, but
only 7 mappings exist (global: MU.TO, DIS.TO, SPGI.TO; yours: NVDA.TO, PG, DIS.TO, BRK.TO). By design an
underlying is accepted only from stored reference data or a validated user mapping, never guessed, and
auto-detection only *asks* (and only for names containing "CDR"/"CAD HEDGED"). Mapped CDRs already
score fully (NVDA.TO, DIS.TO, BRK.TO have Swing and Strategic from the US underlying).
- **Code defect fixed:** a *mapped* CDR was still deferred as ETF because the listing's type beat the
  underlying's (SPGI.TO). Rows analysed through an underlying are now classified by the underlying.
- **Still needed (data, not code):** user mappings for the 9 unmapped CDRs. Proposed (please confirm):
  BOFA.TO→BAC, HD.TO→HD, IBM.TO→IBM, MCD.TO→MCD, MRK.TO→MRK, NFLX.TO→NFLX, NKE.TO→NKE, NOVO.TO→NVO,
  UBER.TO→UBER. Scoring then needs only the underlying's scan, sector and fundamentals, which the
  refresh already acquires for mapped rows.

**2. Unmapped sectors.** Sector Heat compares each stock with a sector ETF from `SectorBenchmarkMappings`
(Sector / Theme Heat column). TSX has only 8 sector ETFs; the table has no TSX entry for **Consumer
Discretionary/Cyclical, Communication Services or Health Care**, while the 11 US SPDR sectors exist.
That left 21 Unmapped rows (all TSX-listed: ATD.TO, BYD.TO, CTC-A.TO, GIL.TO, MG.TO, MTY.TO, QSR.TO,
CCL-B.TO, BCE.TO, T.TO, RCI-B.TO, CCA.TO, CPH.TO, DR.TO, WELL.TO and six CDRs).
- **Implemented as you directed:** when no TSX benchmark exists, the US sector ETF (XLY, XLC, XLV) is the
  proxy. It is converted to CAD with the daily USD/CAD series (Yahoo `CAD=X`) on the stock's own
  sessions, so currency moves do not distort the 20/60-day comparison. The evidence panel labels it
  "US proxy, converted to CAD". If the FX series is unavailable the row stays Unmapped and the refresh
  reports `CAD=X` in its warning; nothing is invented.
- **Takes effect only on the next refresh** (needs the restarted API, then Refresh analysis).

## H. Second refresh — snapshot 10 (verified, SELECT-only)

Snapshot 10, `pm029-v2`, Succeeded, 2026-11-16 02:11 UTC, no warnings (the `CAD=X` series loaded). Snapshot 5
was pruned as planned; 6–10 remain. The 9 CDR mappings (rows 5–13) are in `SecurityAnalysisMappings`.

| Measure | Snapshot 9 | **Snapshot 10** |
|---|---|---|
| Swing numeric | 199 | **209 / 224** (Verified 143, Provisional 66, Not evaluated 15) |
| Strategic numeric | 192 | **201 / 224** (Verified 135, Provisional 66, Insufficient 8, Not evaluated 15) |
| ETF-deferred | 25 | **15** (all true ETFs/funds) |
| Sector Heat | Matched 190, Unmapped 21, Unclassified 13 | **Matched 211, Unmapped 0, Unclassified 13** (all 13 are ETFs) |
| Swing ≥ 70 | 58 | 62 (top: PPL.TO 89, ARE.TO 86, MTL.TO 86, AAPL 86, TOU.TO 85) |
| Actionable FINAL ACTION entries | 0 | 0 |

- **CDRs now scored from the underlying:** BOFA.TO (BAC) Swing 29, HD.TO 78, IBM.TO 79, MCD.TO 65, MRK.TO 63,
  NFLX.TO 18, NKE.TO 38, NOVO.TO (NVO) 65, UBER.TO 63, SPGI.TO 69. Strategic is Provisional for nine of
  them; MCD.TO is Insufficient (no verified valuation factor T7).
- **US sector proxy in use for all 15 TSX stocks that lacked a TSX sector ETF** (XLY, XLC, XLV; for example
  ATD.TO −8.0 pp, BCE.TO +7.5 pp, DR.TO +8.7 pp over 20 days).
- **Strategic Insufficient (8):** CDE.TO, SKHY, SPCX, XNDU.TO (no SMA200 history, T1); MCD.TO, NPI.TO,
  PSLV.TO, SPPP.TO, XNDU.TO (no verified valuation, T7). None were estimated.
- **Remaining:** Market R/Setup Quality sparsity (no stop methodology) and Portfolio Fit (Gate 2).

## I. Setup Quality policy approved and activated (owner, 2026-11-15)

`SetupQualityCalculator.Evaluate` is now called with `classificationApproved: true` by the refresh
(`OpportunityService`). Grades Good / Caution / Poor are advisory: they never change Swing or Strategic
scores, FINAL ACTION or stops, and still require verified structural evidence (entry, stop, ATR, confirmed
support/resistance, target, original OHLC, no session gaps), otherwise the row stays Unavailable with a reason.
On snapshot 10's evidence, BTCC.TO, ETHH-B.TO and TSU.TO have all inputs and would grade; LAC.TO (no confirmed
support) and PBR (session gap) would not; the other 219 rows have no stop. The grades appear after the next
refresh. Test added: every refreshed row reports the policy as approved.

Setup Quality and Market R are computed for every row on both tabs. The Strategic tab previously hid the two
columns by default; **Market R and SETUP QUALITY are now default columns on Strategic as well** (Manual R stays
Swing-only). A previously saved column layout keeps its own choice; add them from the Columns menu.

## J. Why Setup Quality is Unavailable (snapshot 11 analysis)

Snapshot 11: Caution 3 (BTCC.TO, ETHH-B.TO, TSU.TO), Poor 3, Unavailable 218. Reasons across rows:
no stop 217 (no active staged signal; approved rule, left as is), structural session gap 87 (all US rows),
confirmed support unavailable 93, confirmed resistance unavailable 97 (mostly a knock-on of the gap), price
currency mismatch 3 (DE.V, DPRO.CN, TOI.V), stop at/above entry 7, stop outside volatility band 2.
- **Defect found and fixed:** the rule-based NYSE calendar did not know the one-off closure on **2025-01-09**
  (National Day of Mourning). Yahoo history for AAPL has no bar that day (checked: the only weekday gap the
  calendar did not already explain), so every US symbol's 2-year history looked like it had a missing session
  and structural support/resistance was not computed. `TradingCalendar` now lists 2025-01-09 as an NYSE closure
  (TSX unaffected); test added. No session is invented and no recurring rule changed.
- **Still genuinely Unavailable:** rows without a stop need a stop to be graded; that is the approved
  signal-stop rule. After the next refresh, US rows with a stop (for example PBR) can be graded if confirmed
  support and resistance exist.
- **Not a defect:** resistance is absent when price is at a new high; support is absent when no swing low
  remains unbroken (LAC.TO). The three venture/CSE listings have a price-currency mismatch in the provider data.

**Verified on snapshot 13 (after the fix):** structural-session-gap rows 87 → **0**; graded rows 6 → **9**
(Caution: BTCC.TO, ETHH-B.TO, PBR, TSU.TO; Poor: DR.TO, NVDA, NVDA.TO, STN.TO, WCN.TO). Every remaining
Unavailable row is stop-dependent (no active signal stop 217, stop above entry 7, outside band 2) except
LAC.TO (no confirmed support), 14 rows without overhead resistance, 3 currency mismatches.

## K. Strategic setup forked from the Swing signal stop (owner request, 2026-11-15)

Swing keeps the staged-signal stop and target (unchanged). Strategic no longer depends on a signal stop:
`SetupQualityCalculator.EvaluateStrategic` uses the nearest confirmed, unbroken swing **support** below price
as the invalidation level and the nearest confirmed, unbroken **resistance** above price as the target, from
the same verified structural history. Market R (Strategic) = (resistance − price) / (price − support). Grades
reuse the Swing thresholds: Poor below the actionable reward:risk, Caution when support is within one ATR or
R is below 2, otherwise Good. Without both confirmed levels or with unverified structure the row is Unavailable
with the reason; nothing is invented. Policy id `pm029-strategic-structure-v1`, advisory only; it never changes
scores, FINAL ACTION or stops. New row field `strategicSetupQuality`; the Strategic grid, Setup Quality filter,
sort, export and detail panel read it (the Swing views read the existing `setupQuality`). It appears after the
next refresh. Tests: 3 grading cases, an unavailable case, service wiring, and filter/sort/export accessors.

## Tests run

- Backend: full suite 1386 passed before this follow-up; after it, focused Pm029/Opportunity/FinalAction/
  Replay/SetupQuality suites **313 passed** (new: CAD conversion, US-proxy refresh, FX-missing, instrument
  type). Full Angular suite **709 passed** (no frontend change).
- Backend focused suites (Pm029, FinalAction, SwingReplay, SetupQuality, PortfolioEvidence, Opportunity,
  ETF, Strategic, Swing): **616 passed, 0 failed, 0 skipped** (isolated output; live API untouched).
- Angular opportunity specs (logic, grid/components, store): **47 passed**. Production `ng build`: passed.
- No live Playwright, no scanner/EOD job, no database write.
