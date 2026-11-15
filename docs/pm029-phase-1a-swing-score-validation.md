# PM-029 Phase 1A — Swing Opportunity Score Validation

**Disposition:** Read-only replay and synthetic formula validation are complete. No production scoring, FINAL ACTION, scanner, financial, or snapshot data was changed. PM-029 remains **OPEN**. Stop here; Phase 1B is not authorized.

## 1. Baseline and preservation

- Branch: `feature/pm-029-final-review` (unchanged; no branch switch).
- `HEAD` and `develop`: `3d4e7019ae5877eb4d925c5b10ddf50309cb5a22`.
- Original 18 staged PM-029 files remain staged; no index-changing command was run in Phase 1A. The staged diff and tracked worktree diff both pass `git diff --check`.
- At the start of Phase 1A, the recorded staged binary diff SHA-1 was `708a965eab9e5167192fd554e21c6a4398e0ab86`, and the recorded Phase 0C tracked worktree diff SHA-1 was `bade080920cce141393891308e47b1279bfa2813`. No existing production or Phase 0C test source was edited in this phase.
- New files for this phase: `backend/PortfolioManager.Tests/Pm029SwingFormulaValidationTests.cs` and this report. Both remain unstaged.
- No refresh, writes, branch changes, reset, clean, checkout, commit, push, pull, merge, migration, seed, or deployment was performed.

### Phase 0C source/test starting hashes

The following SHA-256 values were recorded before Phase 1A edits and remain unchanged:

| Existing Phase 0C file | SHA-256 |
|---|---|
| `backend/PortfolioManager.Api/Services/FinalAction/FinalActionService.cs` | `A92837B59B2B71C3C7A8910EC806DE1006392C9488C7D7BB6F42DDE88702C654` |
| `backend/PortfolioManager.Api/Services/Opportunities/OpportunityService.cs` | `A4F72619CC0A1631218C999916847DAFEF5BB994430FE0CC864DE4042C9EB06D` |
| `backend/PortfolioManager.Api/Services/RsiScannerService.cs` | `19B63CC2BD03417B7B4B5F718527086AFF1CBA4FB1BDEE44CFB727722CBFC82F` |
| `backend/PortfolioManager.Api/Services/SecurityAnalysisResolver.cs` | `F060405E23475A0E8BE77650B15514C082281E22BE42C4CB4C33620AF682B6F4` |
| `backend/PortfolioManager.Api/Services/StagedSignalService.cs` | `25F7A42DB5D5B579BBE2E27930DDF034B5575619D4FFE42B1F44C43B8A02EA66` |
| `backend/PortfolioManager.Tests/Pm029FinalActionGoldenTests.cs` | `89996EBED8E64966B5368080046D7CB26A08DBBFA5857302BC09A269AED98902` |
| `backend/PortfolioManager.Tests/Pm029FinalActionRefactorTests.cs` | `EF10C5FACBC721730E4E132265E5CF22FC0646F220165ABB8BF7FDA455667130` |
| `backend/PortfolioManager.Tests/SecurityAnalysisResolverTests.cs` | `4B8D7552482F5C105B598184BE3F95A4A30F959BA3A043F678AB764F6B8276F2` |
| `backend/PortfolioManager.Tests/Pm029PhaseZeroRefreshSafetyTests.cs` | `1BC75DF2FFC9C340F0F870179212491A705D5D0DA9B7420E5DC4F277301DD385` |
| `backend/PortfolioManager.Tests/Pm029StagedSignalPreviewTests.cs` | `AFC8174058B4BDDD3A506A90E368C4D68FF9CBB2387FB763E9FA2F72659F4A18` |

The backend tests use .NET 9, VSTest, and xUnit v2. `Pm029CalculatorTests.cs` is part of the preserved staged baseline and was not edited.

## 2. Snapshot access and provenance

The snapshot was available through the existing local application configuration: `Program.cs` reads `ConnectionStrings:DefaultConnection`, with the local default in the API's `appsettings.json`. A local SQL Server service and the `PortfolioManagerLocal` database were available. Snapshot reads used the configured local connection with read-only application intent and SELECT-only commands; the raw JSON and financial/ownership fields were parsed locally and were not printed or copied into this report.

No safe, complete, sanitized application export exists for this diagnostic. The UI export is a filtered row export, omits the per-component `ScoreResult` evidence, and can include portfolio weights and optional account names. The authenticated GET snapshot endpoint is read-only, but the per-symbol detail endpoint includes saved plans and account-aware decisions and is not a sanitized export.

| Snapshot | Status | Version | Rows | Computed UTC | FINAL ACTION computed UTC | Data session | FINAL ACTION evaluation ID |
|---|---|---|---:|---|---|---|---|
| 6 (latest) | Succeeded | `pm029-v1` | 224 | 2026-11-14 22:49:21.7962299Z | 2026-11-14 22:48:42.1535765Z | 2026-11-13 | `7f5c3f3a-8567-4c0c-8815-b5b533723703` |
| 5 | Succeeded | `pm029-v1` | 224 | 2026-11-14 18:13:29.3087706Z | Not recorded in this report | 2026-11-13 | `154057c1-d528-4463-b841-a0733c1117f0` |

Both snapshots produced the same 224-row availability, component, candidate-score, and top-20 results below. Snapshot 6 is used for the detailed report. Its 224 rows all had `RowStatus=Ok`, expected session and data session `2026-11-13`, and a scan timestamp. Scan timestamps ranged from `2026-11-14 22:46:33.346057Z` through `22:48:58.806236Z`. No live market-data query or Opportunity refresh was run.

## 3. Existing scoring audit

In `OpportunityScoringEngine`, the current Swing score combines S1–S5 with S6, uses `Σ awarded / Σ available` for the headline, applies a 70% completeness gate to a 100-point pool, and marks S1, S3, and S6 essential. An unavailable essential S6 makes the headline N/A even when S1–S5 are usable.

| Component | Max | Snapshot 6 state: Observed / Partial / Missing | Awarded / available max across 224 rows | Missing evidence |
|---|---:|---:|---:|---|
| S1 Trend & Structure | 25 | 220 / 4 / 0 | 2,565 / 5,576 | Four partial S1 components lack SMA200 evidence. |
| S2 Relative Strength | 20 | 190 / 0 / 34 | 1,686 / 3,800 | 21 `Unmapped`; 13 `Unclassified`. Both 20D and 60D windows have those same status counts. |
| S3 RSI/MACD Momentum | 15 | 224 / 0 / 0 | 1,867 / 3,360 | No S3 component missing in this snapshot. |
| S4 Volume Confirmation | 10 | 224 / 0 / 0 | 850 / 2,240 | No S4 component missing in this snapshot. |
| S5 Entry Quality | 10 | 160 / 64 / 0 | 1,406 / 2,112 | 64 partial components lack the Fib input (`FibUnavailable`). |
| S6 Risk/Reward | 20 | 5 / 0 / 219 | 100 / 100 | 210 `DynamicStopMissing`; 217 `NoValidStop`; 7 `StopAtOrAboveEntry`; 2 `StopOutsideVolatilityBand`. These reason counts overlap. |

### Current 224-row result and the 219 root cause

- Existing Swing statuses: **3 Verified, 2 Provisional, 219 Insufficient, 0 Unavailable**.
- Existing score bands: **5 Weak, 219 None**; no Attractive, Moderate, Caution, or Strong rows.
- All 219 Insufficient rows have `MissingEssential:S6`. Their setup is unavailable because a valid protective stop/setup is not available; S1 and S3 are present and S1 is not missing. The 217 `NoValidStop` rows comprise missing or invalid stops; the additional two rows fail the volatility-band check. S6 therefore makes trade-setup availability a gate on intrinsic technical score.
- These are actual snapshot facts, not synthetic estimates. They do **not** show that any of the 219 is an executable trade.

## 4. Formula replay and method

Both options use exactly the persisted S1–S5 awarded points and component availability from the same row. The replay does not recompute or alter S1–S5 rules. It removes S6 from the technical numerator, denominator, completeness, and technical essential gate. It retains S1 and S3 as technical essentials, and applies the tested threshold to `Σ available S1–S5 max / 80`.

- **Option A:** `round-away-from-zero(100 × Σ awarded S1–S5 / Σ available S1–S5 max)`.
- **Option B:** `round-away-from-zero(100 × Σ awarded S1–S5 / 80)`.
- For either option, no headline is emitted if the row is otherwise unavailable, an essential technical component is missing, or completeness is below the selected gate. Completeness 100% is Verified; an eligible score below 100% is Provisional. Existing score bands and thresholds are unchanged.
- Missing sub-items remain `Partial`/`Missing` with their reasons. They are not rewritten as observed zero-point weakness. Under Option B they reduce the fixed-denominator score, while the Provisional status and completeness explicitly disclose that the lower score is evidence-limited.

The replay uses the stored component results from the actual `pm029-v1` snapshot. It is not a fresh evaluation and does not populate any cache or alter any stored snapshot.

## 5. Actual 224-row comparison

### Coverage and band distribution

| Completeness gate | Option | Scorable | Unscorable | Complete / Verified | Provisional | Strong | Attractive | Moderate | Caution | Weak |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 70% | A | 224 | 0 | 130 | 94 | 11 | 57 | 33 | 13 | 110 |
| 70% | B | 224 | 0 | 130 | 94 | 6 | 53 | 34 | 16 | 115 |
| 80% | A | 190 | 34 | 130 | 60 | 9 | 50 | 29 | 12 | 90 |
| 80% | B | 190 | 34 | 130 | 60 | 6 | 52 | 30 | 12 | 90 |
| 90% | A | 190 | 34 | 130 | 60 | 9 | 50 | 29 | 12 | 90 |
| 90% | B | 190 | 34 | 130 | 60 | 6 | 52 | 30 | 12 | 90 |

The 80% and 90% gates exclude the same 34 rows in this snapshot; there are no rows with technical completeness in the interval `(80%, 90%)`. The 70% gate makes 219 more rows technically scorable than the current S6-gated model.

### Score changes and ranking

At the 70% gate, A and B produce identical scores for 131 rows. The mean absolute score difference is **2.22 points**, with a maximum of **25 points**. Spearman rank correlation is **0.9712** and only **14 of the top 20** candidates are shared. Ties are ordered by symbol for this reproducible comparison.

At either the 80% or 90% gate, the mean absolute difference is **0.54 points**, the maximum is **3 points**, rank correlation is **0.9993**, and the top-20 membership is **20/20** shared.

This is the principal missing-data bias finding: Option A can normalize away a missing 20-point S2 and make the remaining evidence appear fully strong. At the approved 70% threshold, `NOVO.TO` scores **95** under A versus **71** under B at **75% completeness**; `CCL-B.TO` scores **91** versus **66** at **72.5% completeness**. Both lack S2 sector-relative evidence. B still produces a provisional technical score, but avoids labeling either row Strong based only on the available technical pool.

### Top 20 under the 70% gate

Scores and completeness below are actual-snapshot formula replay results. No account, holding, or private financial fields are included.

**Option A — available-evidence normalization**

| Rank | Symbol | Score / band | Completeness |
|---:|---|---|---:|
| 1 | NOVO.TO | 95 / Strong | 75% |
| 2 | CCL-B.TO | 91 / Strong | 72.5% |
| 3 | PPL.TO | 91 / Strong | 97.5% |
| 4 | AAPL | 88 / Strong | 97.5% |
| 5 | ARE.TO | 86 / Strong | 100% |
| 6 | ATRL.TO | 86 / Strong | 97.5% |
| 7 | KR | 86 / Strong | 97.5% |
| 8 | MTL.TO | 86 / Strong | 100% |
| 9 | STN.TO | 86 / Strong | 97.5% |
| 10 | TOU.TO | 85 / Strong | 100% |
| 11 | TXN | 85 / Strong | 100% |
| 12 | ATD.TO | 83 / Attractive | 75% |
| 13 | MRK.TO | 83 / Attractive | 75% |
| 14 | TOI.V | 83 / Attractive | 100% |
| 15 | TRP.TO | 83 / Attractive | 97.5% |
| 16 | TSLA | 83 / Attractive | 100% |
| 17 | BAM.TO | 82 / Attractive | 97.5% |
| 18 | QSR.TO | 82 / Attractive | 75% |
| 19 | DR.TO | 81 / Attractive | 72.5% |
| 20 | EIF.TO | 81 / Attractive | 100% |

**Option B — fixed technical denominator**

| Rank | Symbol | Score / band | Completeness |
|---:|---|---|---:|
| 1 | PPL.TO | 89 / Strong | 97.5% |
| 2 | AAPL | 86 / Strong | 97.5% |
| 3 | ARE.TO | 86 / Strong | 100% |
| 4 | MTL.TO | 86 / Strong | 100% |
| 5 | TOU.TO | 85 / Strong | 100% |
| 6 | TXN | 85 / Strong | 100% |
| 7 | ATRL.TO | 84 / Attractive | 97.5% |
| 8 | KR | 84 / Attractive | 97.5% |
| 9 | STN.TO | 84 / Attractive | 97.5% |
| 10 | TOI.V | 83 / Attractive | 100% |
| 11 | TSLA | 83 / Attractive | 100% |
| 12 | EIF.TO | 81 / Attractive | 100% |
| 13 | LMT | 81 / Attractive | 100% |
| 14 | TRP.TO | 81 / Attractive | 97.5% |
| 15 | BAM.TO | 80 / Attractive | 97.5% |
| 16 | FNV.TO | 80 / Attractive | 100% |
| 17 | VNP.TO | 80 / Attractive | 100% |
| 18 | IBM | 79 / Attractive | 97.5% |
| 19 | KXS.TO | 79 / Attractive | 97.5% |
| 20 | LLY | 79 / Attractive | 97.5% |

At 70%, A's top-20 list includes six S2-missing rows (`NOVO.TO`, `CCL-B.TO`, `ATD.TO`, `MRK.TO`, `QSR.TO`, `DR.TO`); none appears in B's top 20. Both formulas show 224 in “All scored” at this gate. The existing Attractive-or-better filter (70+) would show **68** rows under A versus **59** under B; the Moderate-or-better filter (60+) would show **101** versus **93**. A Verified-only filter would show the same **130 complete** rows for both; the other 94 remain visibly Provisional. At 80%/90%, both formulas have 190 scored rows and the same top-20 membership.

## 6. Evidence patterns and CCA.TO

- **Strong momentum without sector-relative evidence:** six rows have S3 awarded points ≥12/15 while S2 is missing: `BCE.TO`, `CCL-B.TO`, `CPH.TO`, `MRK.TO`, `NOVO.TO`, and `RCI-B.TO`. They remain incomplete; Option B retains the missing-evidence penalty without labeling S2 as observed weakness.
- **Weak technical score with valid setup:** five rows have S1–S5 awarded/available below 50% while the stored setup is `ValidCandidate`: `BTCC.TO` (A 8, B 6), `ETHH-B.TO` (A 5, B 4), `LAC.TO` (A 4, B 4), `PBR` (A 9, B 9), and `TSU.TO` (A 11, B 11). The setup's independent availability does not imply a strong technical score.
- **Technical evidence without a protective stop:** all **219** currently S6-missing rows pass the candidate 70% S1–S5 evidence gate. Their setup remains Unavailable; Entry/Stop/Target/Reward remain unavailable and must not be inferred from the technical score.

### CCA.TO component matrix — Snapshot 6

| Evidence | Result |
|---|---|
| Session / scan timestamp | Data and expected session `2026-11-13`; row status `Ok`. Its scan timestamp falls within the snapshot-wide range `2026-11-14 22:46:33.346057Z`–`22:48:58.806236Z`. |
| Existing Swing result | `Insufficient`; no headline score because S6 is missing. |
| S1 | Observed, **0 / 25**. The trend component is available but awards zero; this is observed technical weakness, not missing evidence. |
| S2 | Missing, **0 / 0 available**; `SectorHeat:Unmapped`. Both 20D and 60D windows are `Unmapped`; no benchmark mapping/evidence is available. |
| S6 / setup | Missing; setup `Unavailable`; reasons `NoValidStop`, `DynamicStopMissing`. No Reward/R is reported. |
| Candidate technical score | **37** under A and **28** under B at **75% completeness**, both Provisional under the 70% gate. |

The snapshot does not support a CCA.TO relative-strength conclusion because the benchmark mapping is absent. Its S1 zero is observed evidence; its S2 and S6 are unavailable evidence.

## 7. Data validation and Phase 0C safeguards

The current scorer gates on identity resolution, evaluation failure, scan presence, and `TradingDate == ExpectedSession`. Existing tests cover stale session dates. S1/S3 are required; in a fresh scan S3 is currently always built from non-null numeric RSI/MACD fields, so a separate S3-missing case was simulated at the component boundary.

The scorer does not independently validate positive current price, RSI in `[0,100]`, or wall-clock age of `ScannedAt`. The new synthetic test demonstrates that a current-session scan with price zero, RSI 101, and a ten-year-old `ScannedAt` can still yield a Verified S1–S5 candidate score after S6 is excluded. Trade setup rejects the nonpositive entry, but that does not currently invalidate the technical components. This is a data-quality/evidence gate requiring an explicit Phase 1B rule; it was not changed here. The expected session is provided to the scorer by the caller, so exchange-to-session correctness also remains an upstream responsibility.

Phase 0C semantics remain untouched: no scanner or staged-signal write, no mapping write, no FINAL ACTION behavior change, and no experimental cache population. The tests continue to exercise the Phase 0C preview, persist/read-only separation, and FINAL ACTION parity.

- **SQL Server concurrency limitation:** the local server was used only for SELECT-only snapshot reads. No isolated SQL Server concurrency or transaction-isolation test database was available/used. Phase 0C's SQL Server-specific concurrency limitation remains.
- **Universe Value Screener boundary:** Opportunity refresh can still conditionally persist/prune Universe-origin Value Screener snapshots when fundamentals are stale/missing. This boundary was not changed; no refresh was run.

## 8. Recommendation

**Recommend Option B with the existing 70% technical completeness gate**, retaining S1 and S3 as essential technical evidence and leaving score bands unchanged.

Reasons:

1. Both options achieve the approved business objective: S6 no longer determines whether intrinsic technical opportunity can be scored, while the actual setup remains separate.
2. At 70%, Option A yields Strong scores from only 72.5–75% completeness when the missing 20-point S2 is a weak/unavailable component; the actual replay shows a 25-point inflation and a 14/20 top-list overlap.
3. Option B is conservative and stable when evidence is complete. It produces the same score on all 130 complete rows and avoids promoting missing-sector evidence to Strong merely by shrinking the denominator.
4. The missing component remains visibly Missing/Partial and scores stay Provisional below 100% completeness, so absence is not relabeled as an observed negative. The 70% gate retains all 224 rows; an 80%/90% gate would exclude 34 without evidence here that those candidates are invalid.
5. A high score is not execution readiness. The setup, protective stop, target, Market Reward, Target Quality, and execution status remain independent.

This is a recommendation for review, **not an authorization to implement or activate the formula**.

## 9. Proposed Phase 1B changes and compatibility

Only after explicit approval:

1. Add a versioned Swing-v2 calculation over S1–S5 using the fixed 80-point denominator. Keep S1/S3 essential, use technical completeness `available S1–S5 max / 80`, retain the 70% gate, and preserve all current S1–S5 point rules.
2. Remove S6 from the Swing score numerator, denominator, completeness, and essential gate. Keep the existing market-derived `TradeSetupResolver` rules and display all setup evidence independently; missing entry/valid stop/target continues to mean no Market Reward/R and non-executable setup.
3. Add explicit score-denominator metadata (80 for Swing-v2) while retaining `AvailableMax` as observed available evidence. This avoids a drawer mismatch where the headline is awarded/80 but component reconciliation says awarded/available. Preserve the existing response envelope and make additive fields tolerant of v1 snapshots.
4. Preserve immutable v1 snapshots. Use a new scoring version (proposed `pm029-v2`) for newly computed snapshot results; Strategic rules remain mathematically unchanged. UI already renders score components dynamically, but its details label and points reconciliation must distinguish technical score from separate setup status and show the denominator/completeness. Existing bands, sorting, filters, and Provisional hatch/marker can remain.
5. Add approved validity checks for invalid price/indicator values and freshness. Do not invent a wall-clock freshness interval or exchange mapping rule; define these explicitly before coding.
6. Re-run the same 224-snapshot replay on the proposed implementation and add focused tests for score/completeness boundaries, S6 absence, incomplete S2/S4/S5, malformed data, and FINAL ACTION/Phase 0C invariance. Do not mutate historical snapshot rows or trigger an automatic refresh as part of this phase.

### Decisions requiring approval before Phase 1B

- Select **Option B** or Option A; this report recommends B.
- Confirm the 70% gate and S1/S3 essential evidence rules for the Swing technical score.
- Approve versioning (`pm029-v2`) and the additive denominator metadata/API/UI treatment.
- Define validity requirements for nonpositive price, out-of-range RSI/other invalid indicators, stale scan timestamps, and exchange-session mismatch. No arbitrary timestamp threshold is proposed.
- Confirm that S6 is fully separate from Swing score/status and remains represented only by setup/evidence status.

## 10. Tests and changed files

The isolated synthetic tests in `Pm029SwingFormulaValidationTests.cs` cover:

- All S1–S5 available and S6 unavailable.
- Missing S2 under both denominators.
- Missing S4 and S5.
- Missing S1 and a simulated missing S3 essential.
- Just-below, equal-to, and just-above 70% completeness.
- Valid setup with weak observed technical evidence.
- Setup/scenario changes not affecting technical candidate score.
- Zero price, RSI outside the normal range, and stale scan timestamp behavior.

Results:

- Isolated Phase 1A synthetic tests: **11 passed, 0 failed, 0 skipped**.
- Targeted PM-029/Phase 0C scanner, resolver, and FINAL ACTION regressions: **182 passed, 0 failed, 0 skipped** before the final additional setup-invariance test; the full run below includes that test.
- Full backend suite: **1,251 passed, 0 failed, 4 skipped** (1,255 total). The four skips are SQL Server-dependent `AccountTypeSqlServerTests` (3) and `Pm033IsolatedSqlTests` (1); they require an explicitly configured isolated SQL Server test environment.
- Frontend/Playwright tests were not run because no frontend file was changed in Phase 1A.

Only the new isolated test file and this report were added for Phase 1A. Test file SHA-256: `8037B1302451CBF97F8B5F3FC731E026DDD44E3C32978330F2FAD215FFFCC477`. No production scoring, Strategic scoring, API, UI, scanner, FINAL ACTION, PM-035, database, migration, seed, or Opportunity snapshot changes were made. PM-029 remains **OPEN**; stop and await formula/evidence-rule approval.
