# PM-029 Phase 2A — Strategic Fundamentals Investigation

**Disposition:** Investigation and isolated regression testing are complete. No production code, scoring policy, scanner behavior, API contract, database record, migration, seed, or frontend file was changed. PM-029 remains **OPEN**. Do not begin Phase 2B until the findings and proposed approval gates below are reviewed.

## Executive findings

- The saved Opportunity snapshot contains 224 securities. **204 have a Strategic score** (124 Verified, 80 Provisional); 20 are Insufficient.
- **17** rows fail essential T7 because fewer than two eligible valuation factors are available. **Four** fail essential T1 because SMA200 is unavailable; one row has both failures.
- None of the 224 selected fundamental records is missing, legacy, or older than seven days at snapshot time. An alternative saved origin record did not restore T7 eligibility for any of the 17 T7 failures.
- The saved CCA.TO row **does not fail T7**: it has two eligible factors, T7 is Partial at 25/25 points, and the Strategic score is 54 Provisional at 90% completeness. Its F1 is unavailable, but F2 and F3 are available.
- The evidence does not justify relaxing T7, the market-cap fallback exclusion, or the essential T1/T7 rules. No no-policy-change score recoveries can be established from the saved data.

## 1. Baseline and data access

- Branch: `feature/pm-029-final-review`
- Starting HEAD: `3d4e7019ae5877eb4d925c5b10ddf50309cb5a22`
- The actual starting index contained **41 staged paths** and no unstaged changes. This differs from the earlier expectation of 18 staged files. I preserved the actual index without staging or unstaging anything.
- Starting staged-diff SHA-256 (raw bytes from `git diff --cached --binary`): `e64aabfc810aba6158da2d0379ca4a3f587717ee1842ca02b1ea2deccf5149fd`. It must remain unchanged after adding this report.
- Read-only local SQL access was available through the existing `ConnectionStrings:DefaultConnection` configuration in `backend/PortfolioManager.Api/appsettings.json`. The connection used read-only application intent and SELECT-only statements. No connection values or credentials were emitted.
- Opportunity snapshot **6** is `pm029-v1`, `Succeeded`, with 224 records and `DataAsOfSession=2026-11-13`. Its computed timestamp is `2026-11-14T22:49:21.7962299Z`.
- Its JSON hash matches the Phase 1B report: `EAB3E5CC54EB2F16398D15E491AFFBDC3361B348D8A76E073E33612B4C913BB3` when hashed as UTF-16LE. The UTF-8 byte hash differs because the encoding differs; this is not evidence that the saved snapshot changed.
- No Opportunity refresh, Value Screener run, Yahoo/provider call, or database write was initiated. A safe export was not needed because the authorized local read-only snapshot was accessible.

The latest saved Value Screener inputs at the snapshot time were:

| Origin | Snapshot | Run time UTC | Records |
|---|---:|---|---:|
| Universe | 252 | 2026-11-14 17:25:39Z | 26 |
| Portfolio | 253 | 2026-11-14 22:01:28Z | 101 |
| Watchlist | 255 | 2026-11-14 22:03:43Z | 165 |

The application’s existing precedence is Universe < Watchlist < Portfolio. The selected records for the 224 Opportunity rows were 24 Universe, 130 Watchlist, and 70 Portfolio; all matched the origin and analyzed-at evidence stored in the Opportunity snapshot.

## 2. Strategic scoring audit

The existing Strategic-v1 score has a 100-point maximum and uses available-evidence normalization. T1 and T7 are essential; the score is Insufficient if either is missing or completeness is below 70%.

| Component | Maximum | Role in availability |
|---|---:|---|
| T1 Long-Term Trend | 20 | Essential; requires SMA200 |
| T2 Relative Strength | 10 | Nonessential; requires mapped 60-day sector-relative evidence |
| T3 Momentum | 5 | Nonessential |
| T4 Volume | 5 | Nonessential |
| T5 Entry Timing | 10 | Nonessential |
| T6 Downside Risk | 10 | Nonessential |
| T7 Valuation | 25 | Essential; requires at least two of F1/F2/F3 |
| T8 Business Quality | 15 | Nonessential; requires at least four F4 signals |

T7’s factors are F1 Earnings Yield (3), F2 FCF Yield (2), and F3 P/B (1), with a P/FFO proxy routed for REITs. When at least two are available, T7 rescales their awarded points across the full 25-point component. An estimated market cap excludes F2; for REITs it also excludes the F3 proxy. T8 only accepts F5 when its basis is ROIC or ROE; the Proxy basis is not accepted.

Relevant implementation sources: [OpportunityScoringEngine.cs](../backend/PortfolioManager.Api/Services/Opportunities/OpportunityScoringEngine.cs), [OpportunityScoringConfig.cs](../backend/PortfolioManager.Api/Services/Opportunities/OpportunityScoringConfig.cs), and [ValueScreenerService.cs](../backend/PortfolioManager.Api/Services/ValueScreenerService.cs).

## 3. Snapshot coverage and exact root causes

### Strategic status and bands

| Status | Count |
|---|---:|
| Verified | 124 |
| Provisional | 80 |
| Insufficient | 20 |
| Unavailable | 0 |
| **Scored** | **204** |

| Band | Count |
|---|---:|
| Strong | 2 |
| Attractive | 24 |
| Moderate | 35 |
| Caution | 45 |
| Weak | 98 |
| None / no headline score | 20 |

| Completeness | Count |
|---|---:|
| 100% | 124 |
| 85% to <100% | 69 |
| 70% to <85% | 15 |
| <70% | 16 |

### Component status

| Component | Observed | Partial | Missing |
|---|---:|---:|---:|
| T7 Valuation | 149 | 58 | 17 |
| T8 Business Quality | 194 | 2 | 28 |

T1 was missing for 4 rows, all with `Sma200Unavailable`. T7 was missing for 17 rows, all with `FundamentalsInsufficientFactors`. The T1/T7 intersection is 1 row, so the 20 Insufficient results comprise 16 T7-only, 3 T1-only, and 1 with both missing. Sixteen rows also carried `LowCompleteness`; reason counts overlap and should not be added as mutually exclusive totals.

Other overlapping Strategic reason counts were:

| Reason | Rows |
|---|---:|
| `FundamentalsInsufficientFactors` | 17 |
| `MissingEssential:T7` | 17 |
| `MissingEssential:T1` | 4 |
| `Sma200Unavailable` | 4 |
| `LowCompleteness` | 16 |
| `PiotroskiInsufficientSignals` | 28 |
| `SectorHeat60D:Unmapped` | 21 |
| `SectorHeat60D:Unclassified` | 13 |

There were **zero** `FundamentalsMissing`, `FundamentalsLegacy`, or `FundamentalsStale` reasons in this snapshot.

### T7 factor availability

Across the 224 selected records, raw factor flags were:

| Factor | Available | Unavailable |
|---|---:|---:|
| F1 | 187 | 37 |
| F2 | 184 | 40 |
| F3 | 206 | 18 |

After applying the existing market-cap fallback exclusions, the effective F1/F2/F3 combinations were:

| Eligible factors | Count |
|---|---:|
| F1, F2, F3 | 149 |
| F1, F3 | 26 |
| F2, F3 | 30 |
| F1, F2 | 2 |
| F1 only | 10 |
| F2 only | 1 |
| F3 only | 1 |
| None | 5 |

The last four combinations account for the 17 T7 failures. **Fourteen of those 17** also had `MarketCapFallbackUsed=true`; the fallback guard is operating as designed and must not be bypassed to manufacture a second factor. The failing symbols were BTCC.TO, CCOE.TO, ETHH-B.TO, GDX, HUN.TO, MCD.TO, NPI.TO, PSLV.TO, SLV, SMH, SPPP.TO, TAN, TDOC.TO, URA, WEAT, XCLN.TO, and XNDU.TO.

## 4. Fundamental data findings

### What the saved records prove

- All 224 rows selected a fundamental record, and all had `FundamentalsDataAvailable=true`.
- All 224 records were analyzed within one day of the Opportunity snapshot; none exceeded the seven-day gate.
- All 224 had explicit F1 and F4 availability flags; none was legacy.
- Replaying the latest saved record from another origin did not yield two eligible T7 factors for any of the 17 T7 failures.
- Therefore, **zero Strategic scores are demonstrably recoverable from the saved data by correcting origin selection, missing-record handling, legacy handling, or freshness handling while keeping the current scoring rules**. New verified source data could change future results, but this snapshot cannot establish that it would.

### Data provenance gaps

- `FundamentalsUsable` checks `FundamentalsDataAvailable`, requires non-null F1/F4 flags, and measures freshness from `AnalyzedAt` against seven days. It does not have a statement-period date to test.
- `FundamentalsDataAvailable=true` is set whenever a non-null Yahoo quote-summary result is mapped; it is not a completeness signal. The per-factor availability flags are the meaningful evidence for T7/T8 coverage.
- Yahoo statement DTOs include `EndDate`, but `YahooFinanceService.MapFundamentalsSnapshot` selects the first statement entry and does not carry its period into `FundamentalsSnapshot` or the persisted `ValueScreenerResult`. The saved record therefore cannot prove how recent the underlying financial statements are.
- The saved result contains factor availability, scores, and some basis labels, but not raw EV, EBIT, trailing P/E, FCF, OCF, market cap, or per-factor missing explanations. A false factor flag often cannot be traced to the exact input from the snapshot alone.
- Yahoo’s quote response includes `QuoteType`, but `MapQuoteItem` uses it only for sector fallback; `StockQuote`, `ValueScreenerResult`, and the Opportunity row do not persist an instrument type. An exact full-universe equity/CDR/ETF classification is therefore unavailable.
- The provider’s F4 code is described as Piotroski, but several implemented signals are one-period proxies (for example positive ROA rather than change in ROA, leverage thresholds rather than declining leverage, and revenue growth as a dilution proxy). The persisted 224-row result has 196 rows with 4–6 available signals: 11 with 4, 8 with 5, and 177 with 6; 28 have 0. It should be described as a Piotroski-style proxy, not a verified textbook Piotroski F-score.
- The F2 implementation can fall back to OCF and the Yahoo mapper can derive FCF from OCF less CapEx. In this snapshot, 184 rows used an `FCF` basis, none used `OCF`, and 40 had F2 unavailable. The OCF fallback did not affect this replay.
- For F5, the selected records showed 194 `ROE`, 14 `Proxy`, 0 `ROIC`, and 16 with no basis. The current T8 gate excludes the 14 Proxy-basis values.

### Representative evidence by instrument signal

These are saved-data signals, not a complete authoritative type classification:

| Example | Saved evidence | T7 result |
|---|---|---|
| ABX.TO | TSX60 member; ordinary analysis ticker | Observed; F1/F2/F3 available |
| NVDA.TO | Trading ticker resolves to analysis ticker NVDA; consistent with a wrapper/CDR-style mapping | Observed; F1/F2/F3 available |
| BTCC.TO | Value Screener sector label `ETFs & Funds` | Missing; no eligible T7 factors and market-cap fallback |

There are 60 TSX60 source members, 15 records with the Value Screener sector label `ETFs & Funds`, and 5 rows with `usesUnderlying=true` (4 use a different analysis ticker). These signals do not overlap in this snapshot. The remaining 144 cannot be reliably classified as equity, CDR, or ETF/fund from the persisted fields; the wrapper flag is not an authoritative instrument type.

## 5. CCA.TO — correction to the reported T7 failure

The saved CCA.TO row contradicts the premise that it fails T7:

- Strategic status: **Provisional**, score **54**, completeness **90%**.
- Row status: `Ok`; analysis ticker `CCA.TO`; no underlying mapping.
- Fundamental origin: Portfolio; analyzed at `2026-11-14T22:01:28.6271332Z`.
- T7 is **Partial, 25/25 points**. F1 is unavailable; F2 is available at 2/2 on an `FCF` basis; F3 is available at 1/1. It meets the existing two-factor minimum, and market-cap fallback is false.
- T1 is observed at 0/20; this is observed trend evidence, not missing data. T2 is missing due `SectorHeat60D:Unmapped`. T8 is observed at 5.625/15.
- Its Value Screener record has `FundamentalsDataAvailable=true`, F4 available with 6 signals, and F5 on an ROE basis.

The saved result does not preserve the raw EV, EBIT, or trailing P/E inputs, so it cannot identify which input specifically made F1 unavailable. Under the current implementation, F1 is unavailable unless positive EV and EBIT are both present or a positive trailing P/E is present. No T7 rule change is indicated for CCA.TO.

## 6. Safe-recovery simulation and Phase 2B proposal

The existing two-of-three T7 rule already rescales available factors. All selected records were fresh and explicit, and no alternate-origin record recovered the 17 T7 failures. Four additional rows lack essential SMA200 evidence. Consequently, the saved data supports **no score recovery without either new verified fundamentals/technical evidence or a scoring-rule change**.

Recommended Phase 2B scope for approval:

1. Keep T7’s two-factor minimum and market-cap fallback exclusions unchanged. Do not replace missing factors with assumed values.
2. Before any authorized refresh/replay, persist statement period-end and provider retrieval timestamps, and record factor-level missing reasons/raw provenance. This would distinguish stale statements from a recently processed snapshot and explain F1/F2/F3 unavailability.
3. Persist provider `QuoteType` or an equivalent explicit instrument-class field so equity, CDR/wrapper, and ETF/fund cohorts can be measured rather than inferred from sector labels.
4. Replay the 17 T7 failures only after the necessary evidence is available. Report recoveries using the unchanged scoring gates; do not activate any scoring change in that replay.
5. Treat changes to OCF-as-FCF fallback semantics, Piotroski proxy scoring/labeling, instrument-specific scoring, the seven-day freshness rule, thresholds, or essential-component rules as separate scoring-policy decisions requiring approval.
6. Do not refresh Opportunity/Value Screener snapshots or change public API/UI contracts as part of this Phase 2A report. Any persistence/API contract change requires explicit Phase 2B authorization.

## 7. Phase 0C safeguards and write boundaries

- This phase performed SELECT-only snapshot and schema reads. No Opportunity or Value Screener refresh ran; no scanner/staged-signal, mapping, FINAL ACTION, portfolio, accounting, or cache write was initiated.
- The existing conditional Universe Value Screener refresh remains in `OpportunityService`: on an authorized Opportunity refresh it can screen missing/stale Universe results and save a Universe snapshot. This Phase 2A did not invoke or alter that path.
- No test wrote to the local application database. The test suite used its existing isolated fixtures.
- Phase 0C Preview/Persist/ReadOnly code and FINAL ACTION behavior were not changed.
- SQL Server concurrency and relational write-isolation behavior remain unverified in this phase. The four SQL Server-dependent tests were skipped; read-only access to the application database does not substitute for an isolated SQL Server integration test.

## 8. Regression tests

The normal backend build output was locked by an already-running `PortfolioManager.Api` process. I did not stop that process. Instead, I rebuilt and ran tests in separate temporary output folders to validate the current staged source:

| Run | Result |
|---|---|
| Targeted PM-029 scoring, Opportunity, FINAL ACTION, preview, and security-resolver tests | **117 passed, 0 failed, 0 skipped** |
| Full backend suite | **1,257 passed, 0 failed, 4 skipped** (1,261 total) |

The four skips are SQL Server-dependent tests (three account-type serialization/upgrade tests and one isolated SQL test). The fresh isolated build completed with existing test-project warnings (async method without `await`, nullability mismatch, and xUnit analyzer guidance). No Angular or Playwright tests were run because no frontend files changed.

## 9. Remaining visual follow-ups

These Phase 1B screenshot observations remain open and were not changed in this data-only investigation:

1. Correct synthetic Saturday session metadata to Friday, 2026-11-13.
2. Visually distinguish synthetic “CCA Test Corp” from the real CCA.TO row.
3. Review the pending Sector Heat color redesign.
4. Provide independent Swing/Strategic column profiles.
5. Reduce crowding in mobile tabs and the Columns header.
6. Make tablet horizontal-scroll affordance clearer.

## 10. Decision and approval gates

**Recommendation:** Do not change the Strategic score formula or T7/T1 availability gates based on this snapshot. CCA.TO already passes T7, 207 rows pass T7, and all 17 T7 failures have fewer than two eligible factors even after comparing saved origins. The evidence points to missing factor inputs and a need for better fundamental/instrument provenance—not a safe way to lower the current gate.

Before Phase 2B, approve or reject the proposed provenance and instrument-class evidence fields, then authorize any isolated replay or provider refresh separately. The following remain unauthorized by this report: production scoring changes, provider refreshes, API/UI contract changes, migrations/seeds, shared accounting/FX changes, PM-035 changes, and writes to protected financial or FINAL ACTION state.

PM-029 remains **OPEN**; no automatic continuation or closure is recommended.
