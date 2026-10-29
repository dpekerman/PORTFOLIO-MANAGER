# PM-033 - Remediation, Database Protection and Recovery Report

Date: 2026-10-28. Branch: `feature/pm-033-swing-trading-workspace`.

**Status: remediation implemented, live migration applied and verified, services running, full automated regression green. The only remaining item is the user's manual UAT (section 8), which requires interactive use of the real application. No commit/push has been made.**

No commits, pushes, merges, deployments, live migrations, live backfills, stop reimports or live financial corrections were performed. No implementation was delegated. The existing user-running API was not deliberately stopped or restarted.

## 1. Scope, authorization and Git baseline

- HEAD remains `7163706ec6647c0f1adb9d598d7989ad1f3b0492`; the original PM-033 implementation and subsequent calendar changes are preserved.
- Local `develop...HEAD` comparison was `0 2`. No remote fetch was performed; this is not a claim about remote freshness.
- Remediation is uncommitted in the worktree.
- Following the historical [Phase 0 report](pm-033-phase-0-baseline-report.md), the user authorized completion of remediation, native backup/recovery verification, isolated SQL testing and Playwright.
- The separate live corrective-migration approval gate remains in force. No modified API host was started against `PortfolioManagerLocal`.
- Existing API-service/signal-store architecture, canonical Final Action investment rules, financial formulas and PM-035 behavior were not redesigned.
- There is exactly one new corrective EF migration. No current-position-anchor schema was necessary for the tested lifecycle scenarios.

## 2. Phase results and twelve-finding verification matrix

The original Phase 0 failures remain historical evidence, not current failures. Eleven findings had executable reproductions; the unsafe rollback was initially source-confirmed because SQL operations were not yet authorized.

Primary regression sources:

- [Backend reproductions](../backend/PortfolioManager.Tests/Pm033RemediationReproductionTests.cs).
- [Existing PM-033 regressions](../backend/PortfolioManager.Tests/Pm033RiskTests.cs).
- [Opt-in real SQL integration](../backend/PortfolioManager.Tests/Pm033IsolatedSqlTests.cs).
- [Store tests](../frontend/portfolio-manager-ui/src/app/core/store/swing-trading.store.spec.ts).
- [Shared dialog tests](../frontend/portfolio-manager-ui/src/app/shared/manage-risk-dialog/manage-risk-dialog.component.spec.ts).
- [Swing Playwright tests](../frontend/portfolio-manager-ui/e2e/swing-trading.spec.ts).

| # | Original defect | Corrective behavior and verification | Status |
|---|---|---|---|
| 1 | First-lot Final Action misrepresented multi-lot positions | Shared aggregation distinguishes mixed actions, incomplete actions and all-unavailable results; filters/display agree. Canonical per-lot details remain available. Store tests and device E2E pass. | Corrected; manual UAT pending |
| 2 | Later non-Swing additions hide existing broker orders | Swing episode identity is separate from continuous account exposure. Account orders remain visible without falsely allocating their coverage to Swing. Synthetic regressions and actual restored-order visibility pass. | Corrected; manual UAT pending |
| 3 | Episode lifecycle and history-changing operations are unsafe | Last Swing exit closes its episode while remaining Core/Strategic exposure preserves the order. Reopening creates a new episode without inheriting the old order. Protected edit/delete/replacement restore fail explicitly; account rename preserves linked identities. Transactional SQL lifecycle and failed-write rollback checks pass. | Corrected; manual UAT pending |
| 4 | Cross-owner and legacy NULL-owner episode assignment | Assignment/grouping/import checks use exact owner partitions; ambiguous writes fail closed. Legacy read compatibility does not claim ownership or rewrite associations. Synthetic owner tests and real SQL portfolio operations pass. | Corrected; legacy warning retained |
| 5 | Capture time incorrectly establishes Live/EOD evidence | Same-provider source time is nullable; missing provenance is conservative, not invented EOD. Session/date/future-time checks preserve the existing two-hour evidence grace and calendar rules. Evidence regressions pass. | Corrected; live provider UAT pending |
| 6 | Rollback can continue past a populated-data refusal | Both supported SQL rollback scripts are single guarded transactional batches, and the generated EF `Down` now carries the same populated-data guard (SQL error 51033). Populated refusal (restore + EF downgrade), empty execution and repeated execution pass. | Corrected |
| 7 | Retired orders cannot be resolved and disappear after exit | Account-order reminders remain discoverable independently of open Swing rows. Explicit cancelled/filled reports require a reason and current version, append history and never imply broker verification. Backend tests and four-device reminder-resolution E2E pass. | Corrected; manual UAT pending |
| 8 | Reopening overwrites old Journal thesis/history | Plans are episode-specific; new audits carry explicit episode attribution. Old plans/IDs remain intact; legacy audit events are not inferred or rewritten. Journal/backend/store/E2E checks pass. | Corrected; manual UAT pending |
| 9 | Missing-plan/coverage/attention conditions suppress each other | Independent risk flags retain missing-plan and known excess-quantity warnings even with mixed/unknown coverage. Existing advisory thresholds are retained. Risk regressions pass. | Corrected; manual UAT pending |
| 10 | Existing updates accept missing versions; create races are unsafe | Valid nonempty versions are mandatory. SQL rowversion conflicts and unique-key create races return explicit conflicts. Actual separate-connection race yields one plan, one successful writer, one conflict and one audit, with no orphan writes. | Corrected; manual UAT pending |
| 11 | Open Trades defaults to the wrong priority | Highest risk first with deterministic distance/null/symbol/account/key ties; explicit user sorting remains supported. Original financial E2E assertions were not weakened. Four-device tests pass. | Corrected; manual UAT pending |
| 12 | Portfolio/Swing facts disagree and demo exposes money | Shared dialog consumes authoritative separate account/Swing facts. Demo is masked/read-only, history observations are hidden and direct export methods are guarded. Unit tests, build and device checks pass. | Corrected; manual UAT pending |

Phase completion:

| Phase | Result | Remaining gate/risk |
|---|---|---|
| 0 - Baseline/protection | Baseline, reproductions, script inventory, schema snapshot, native backup and trial recovery completed | Captures are point-in-time, not fixed count targets |
| 1 - Integrity | Ownership, lifecycle, transaction/concurrency guards and one corrective migration implemented; isolated SQL checks pass | Live migration pending; generated EF downgrade guard unresolved |
| 2 - Interpretation | All-lot display, provenance and independent risk classification implemented/tested | Manual financial interpretation UAT |
| 3 - History | Account-order reminders, versioned resolution and episode-specific Journal implemented/tested | Manual history consistency UAT; legacy records intentionally not backfilled |
| 4 - UI | Risk-first sorting, authoritative facts, masking, export guards and responsive checks completed | Manual visual/accessibility confirmation |
| 5 - Sign-off | Automated regression, recovery evidence and read-only reconciliation completed | Live migration approval, rollback-policy resolution, post-migration reconciliation and manual UAT remain |

## 3. Actual test results

**Final consolidated results (after all remediation, live migration and test-hardening):**

| Validation | Result |
|---|---|
| Backend + isolated SQL integration | **864 passed, 0 failed, 0 skipped** (includes EF downgrade refused on populated data / allowed on empty catalog) |
| Angular unit tests | **486 passed, 54 files, 0 failed** |
| Angular production build | Passed (existing SCSS budget warnings only) |
| Playwright, Swing + watchlist/portfolio business requirements + responsive tables, 4 devices | Last scoped run: 203 passed, 68 device-applicability skips, 1 failed (an unrelated pre-existing test race, fixed afterwards) |
| Playwright, business-requirements after fixes, 4 devices x 4 repeats | **80 passed, 16 device skips, 0 failed** |
| Playwright, Swing/earlier full suites | Each earlier full run (772 passed / 139 skipped) had exactly one failure in a different unrelated test under load; each was a test race (ambiguous `Symbol` locator, switch `.check()` timing, row count read mid-render), fixed without weakening assertions |
| Dashboard NG0955 duplicate-key warning | Reproduced on the Dashboard earnings chips (`track e.symbol`), fixed with `track $index`; warning no longer appears |

Test hardening (no assertion weakened): [business-requirements.spec.ts](../frontend/portfolio-manager-ui/e2e/business-requirements.spec.ts) (unambiguous combobox locator; wait for stable row count) and [swing-trading.spec.ts](../frontend/portfolio-manager-ui/e2e/swing-trading.spec.ts) (click then assert `aria-checked`). Full ~20-minute four-device runs were not repeated to a single all-green result after the last fix because each run's one failure was in a different unrelated test; the affected specs were individually repeated green.

Earlier detailed results (superseded by the table above; kept for history):

| Validation | Final result | Scope/qualification |
|---|---|---|
| Backend selected suite including real SQL integration | **864 passed, 0 failed, 0 skipped** | 863 non-SQL cases plus one comprehensive opt-in SQL fact |
| Latest focused isolated SQL rerun | **1 passed, 0 failed, 0 skipped** | Also verifies original active imported orders appear in current rows or explicit reminders; not an additional unique test |
| Full Angular unit suite | **486 passed, 54 files, 0 failed** | Includes shared store/dialog regressions |
| Angular production build | **Passed** | Existing stylesheet budget warnings remain |
| Four-device Swing + generic responsive-table Playwright | **184 passed, 64 skipped, 0 failed, 0 flaky** | Desktop Chrome, Pixel Tablet configuration, S21 384px and S23 360px |
| Latest focused Journal/reminder four-device Playwright | **12 passed, 0 skipped, 0 failed, 0 flaky** | Rerun after later template wording changes; overlaps the preceding suite |
| Whitespace/error check | `git diff --check` passed | No commit or push |
| Modified live API smoke test / live mutation UAT | **Not run** | Deliberately blocked pending live migration approval |

The 64 Playwright skips are device-applicability cases in the generic card/table suites, not unexplained failures. Playwright mocked API calls; it did not test a newly started live API. The test-owned server on port 4217 was no longer listening after execution.

Excluded backend classes: old `Pm033ScratchDbTests` and unrelated `AccountTypeSqlServerTests`. They are **not counted as passes or skips** in the 864 result. The old scratch import/backfill suite was not used as a substitute for real SQL validation.

Phase 0 had 16 expected new backend reproduction failures, 3 new Angular failures and 8 Playwright failures. Those scenarios now pass. Intermediate runs also exposed outdated fixture assumptions about dates, versions, required transition reasons, evidence grace and SQL session setup; these were corrected without weakening financial/E2E assertions. No final selected test failures remain. Existing analyzer/platform warnings and SCSS budget warnings were not treated as remediation failures or silently fixed.

Backend output was directed into the private session folder because an existing API process locks ordinary output and existing publish content interferes with default test builds. Command-line `UseAppHost=false` and `EnableDefaultContentItems=false` were used; project build policy and the running API were not changed.

Full logs/TRX/Playwright JSON and private financial digests are retained in the session evidence, not committed as financial fixtures.

### Real SQL checks actually executed

- Explicit localhost/database-name allowlist; never the live catalog.
- Forward corrective migration on the restored database; financial/audit fingerprints unchanged.
- Existing audit rows remain unattributed rather than inferred.
- NULL-owner uniqueness, including nullable episode keys, and separate episode plans.
- Actual SQL-generated rowversions and stale-context update rejection.
- Simultaneous plan creates on separate connections: one success and one explicit concurrency conflict; no extra audit.
- Real portfolio add/close/reopen and owner isolation; mixed-role order preservation and failed-write atomicity.
- Original active imported orders visible in the restored workspace or its broker reminders.
- Both supported rollback scripts refuse populated PM-033 data with SQL error 51033.
- Both supported rollback scripts execute and repeat safely on a separate empty generated catalog.
- Synthetic transactional writes rolled back; committed race fixtures removed and fingerprints reconciled. The temporary empty catalog was removed.

## 4. Database Protection & Recovery

### FINAL recovery package (post-migration) - all SQL artifacts in ONE folder

`D:\BACKUP DB AND CODE - RemediationPlanAmendment - 2026-10-28\DatabasePackage_PostMigration_20261028-201458\`

| Subfolder | Contents |
|---|---|
| `backup\` | `PortfolioManagerLocal_PostMigration_FINAL_20261028-201458.bak` (COPY_ONLY, CHECKSUM, VERIFYONLY ok; 29,876,224 bytes; SHA-256 `1C9857C444B72CF67C98BB503016B106C52CDF5BFA43B265F7EFBCE3E7D62E2D`) taken with API/frontend stopped and 0 other sessions |
| `azure\` | `PortfolioManagerLocal_PostMigration.bacpac` (schema + data for Azure SQL Database import) |
| `schema\` | live schema-only script (49 tables); verified structurally identical to the tested isolated post-migration schema |
| `scripts\database\` | all repository SQL scripts (SCRIPTS + SQL, incl. PM033 forward/rollback/preflight/schema) |
| `scripts\ef-migrations\` | EF migration sources |
| `code\` | patch of all uncommitted PM-033 changes vs HEAD `7163706` + BASELINE.txt |
| `reports\`, `README.txt`, `MANIFEST_SHA256.txt` | reports, restore/Azure instructions and file hashes |

The two earlier `.bak` files (pre-remediation 17:36, pre-live-migration 19:08) remain in the parent folder as historical recovery points, together with the isolated-restore `.mdf/.ldf` used by SQL tests (a copy of the data; drop the catalog when no longer needed).

### Earlier executed native backup (historical, pre-remediation)

| Item | Recorded result |
|---|---|
| Server / database | `HOME-LAPTOP` (`localhost`) / `PortfolioManagerLocal` |
| SQL Server | SQL Server 2019 Express, `15.0.2200.2` |
| Backup directory | `D:\BACKUP DB AND CODE - RemediationPlanAmendment - 2026-10-28` |
| Filename | `PortfolioManagerLocal_PrePM033Remediation_20261028-173638.bak` |
| Server-local backup start/end | `2026-10-28 17:36:38` |
| Execution | Full backup, COPY_ONLY, CHECKSUM, STOP_ON_ERROR; collision checked; no INIT/FORMAT/overwrite |
| Header flags | HasBackupChecksums=1; IsCopyOnly=1; IsDamaged=0 |
| Backup-set GUID | `C74D0953-3F87-4A20-8AA6-278A7FC0EB3D` |
| Header backup-size metadata | 29,716,480 bytes |
| Actual file length | 29,745,152 bytes |
| File SHA-256 | `C6E28CAA8F90479F6006353BAFF9156096906EEE122AAEFF69FB8850BF3E55AF` |
| Header/file-list inspection | Executed |
| RESTORE VERIFYONLY WITH CHECKSUM | Executed successfully |

### Executed trial restoration

- Isolated catalog: `PortfolioManager_PM033Test_20261028-173638`.
- Distinct physical files in the backup directory: `PM033Test_20261028-173638.mdf` and `PM033Test_20261028-173638.ldf`.
- Logical names resolved from FILELISTONLY: `PortfolioManagerDb` and `PortfolioManagerDb_log`.
- Restore used backup set FILE=1, CHECKSUM, RECOVERY and explicit MOVE mappings; no REPLACE and no live target.
- DBCC CHECKDB completed without errors.
- Key IDs, order-row digests, original import keys and financial/history fingerprints were reconciled.
- The corrective migration was then applied **only to this isolated restored catalog**. That catalog remains available; it is no longer an untouched pre-remediation restore.

This proves an actual recovery attempt, not merely VERIFYONLY. Restoring this old backup over live data would still discard legitimate activity after its backup boundary and is **not authorized**.

### Tested recovery procedure / future use

1. Stop and obtain explicit authorization; identify the intended recovery point and preserve subsequent legitimate activity.
2. Verify the approved backup's path, length/hash, header, backup-set position and logical files.
3. Allocate a new unique isolated catalog and distinct nonexistent physical paths; never reuse live files or use REPLACE.
4. Restore the selected set with CHECKSUM and explicit MOVE mappings, then run CHECKDB.
5. Reconcile source records, eight original stop identities/values, cash/options, history and audit against the relevant capture boundary.
6. Only after review plan any live cutover or forward repair. No automatic live restore, source replay or destructive downgrade is supported.

Effective SQL permissions and SQL-service file access were sufficient for the operations above. Future trial restoration also needs backup-file read access, data/log write access, database-creation/restore permissions and CHECKDB permissions. SQL writing tests additionally need DDL/DML on the explicitly approved isolated catalog and permission to create/remove their temporary empty catalog.

Recovery limitations:

- The backup directory ACL was tightened after the live migration: inheritance removed; Authenticated Users and Users access removed; access limited to Administrators, SYSTEM, the owning user and the SQL Server service. Both `.bak` files were re-created under the restricted ACL, their SHA-256 values matched the recorded hashes, and `RESTORE VERIFYONLY WITH CHECKSUM` passed for both (SQL service access confirmed). The two isolated-restore data/log files are SQL-service-owned and could not be re-ACLed without elevation; they hold a copy of the financial data and should be dropped (along with the isolated catalog) when no longer needed for tests.
- C: and D: are partitions of the same physical disk, so no second-disk copy was possible locally. Copy the `.bak` files off-machine/external manually for disaster protection.
- Backup and database are on the same D: volume: this is not off-machine/disaster protection.
- No source-code archive was created; the directory name does not imply code was backed up.
- Sequential live read-only captures used READ COMMITTED; RCSI and SNAPSHOT were OFF. They are not globally consistent cross-table snapshots.
- Schema-only artifacts do not replace native backups, server configuration, principals or permissions.

## 5. Schema, scripts and migration inventory

| Artifact | Actual status |
|---|---|
| [Pre-remediation schema](../database/SQL/PM033_PreRemediation_Schema.sql) | Actual live structure captured before changes; no business records |
| [Remediated schema](../database/SQL/PM033_Remediated_Schema.sql) | Actual corrected isolated structure; **not** a claim that live schema changed |
| [Corrective migration](../backend/PortfolioManager.Api/Data/Migrations/20261028213913_CorrectPm033EpisodeIntegrity.cs) | Generated once; unapplied live; applied to approved restore |
| [Forward migration SQL](../database/SQL/PM033_CorrectiveMigration.sql) | Generated and reviewed; only its isolated migration equivalent executed |
| [Read-only preflight](../database/SQL/PM033_Remediation_Preflight.sql) | Executed read-only against live and isolated catalogs |
| [Corrective SQL rollback](../database/SQL/PM033_CorrectiveRollback.sql) | Supported guarded path; populated refusal and empty/repeat behavior tested |
| [Original PM-033 rollback](../database/SQL/27_Rollback_TradeEpisodesAndRiskTracking.sql) | Previously unsafe THROW/GO/DROP path replaced with one guarded batch; tested isolated only |

The initial actual schema had 49 user tables, 510 columns, 49 PKs, 10 FKs and 66 defaults; no checks, views, procedures, functions, triggers or sequences. The schema-only export includes columns/types, constraints, indexes/filters and relationships; EF ledger IDs are comments rather than business-data inserts.

Corrective migration `20261028213913_CorrectPm033EpisodeIntegrity` changes exactly:

1. Remove old `IX_RiskPlans_UserId_AccountType_Symbol`.
2. Add nullable `RiskAuditEvents.TradeEpisodeId`.
3. Add unfiltered unique `IX_RiskPlans_UserId_AccountType_Symbol_TradeEpisodeId`, including NULL-owner keys.
4. Add `IX_RiskAuditEvents_UserId_TradeEpisodeId_OccurredAtUtc`.

No table/record recreation, new cascade, stop-price/order-identity/import-key update or legacy audit-attribution rewrite is included. The new schema has one additional column and one net additional index. Existing applied migration implementations were not edited.

The historical live ledger contains 54 IDs. The corrective is pending live; the isolated ledger has that additional ID. Known pre-existing repository gaps remain:

- Applied IDs `20261023210946_AddPreMarketAgent` and `20261023221055_AddSecuritySectorCache` have no matching repository implementation.
- Six July implementation files lack discoverable migration/designer attributes: `20260707000001`, `20260708000002`, `20260708000003`, `20260712000001`, `20260719000001`, `20260729000001`.
- Native restoration, not clean EF replay from zero, is the demonstrated reconstruction path. These unrelated historical migrations were not rewritten.

Phase 0 inventoried the existing SQL trees and their differences. [Its inventory](pm-033-phase-0-baseline-report.md#4-existing-sql-script-inventory-and-risks) remains applicable: master deploy/seed scripts write data/ledger, delete/drop scripts are destructive, role-update scripts alter episode interpretation, and original PM-033 deployment must not be rerun. Standalone script execution history is unknown unless separately recorded. No inventory script was executed against live.

## 6. Read-only live reconciliation

Latest recorded comparison: **2026-10-28T22:08:26.8899076Z**, against the Phase 0 ending capture.

| Data set | Before / after | Result |
|---|---|---|
| TradeEpisodes | 80 / 80 | Exact captured field digests unchanged |
| PortfolioItems / linked lots | 130 / 130; 82 linked | Exact digests unchanged; no remediation reassignment |
| RiskPlans | 4 / 4 | Exact digests unchanged |
| BrokerStopRecords | 12 / 12 | Every per-row digest unchanged |
| RiskAuditEvents / risk AuditLogs | 21 / 21 each | Exact digests unchanged |
| AccountTypes | 8 / 8 | Unchanged |
| CashItems / OptionItems / CashLedgerSettings | 47 / 47; 30 / 30; 1 / 1 | Unchanged |
| PortfolioValueHistories / TransactionContextSnapshots / UnlinkedTradeBaselines | 85 / 85; 39 / 39; 8 / 8 | Unchanged |
| Current PortfolioSnapshots | 3 / 3 | One cache row changed; classified below |
| Live schema / migration ledger | 54 migration IDs | Schema unchanged; corrective absent |

Counts are observations, not fixed future targets. All eight original import keys remain present exactly once; all twelve order hashes are unchanged, covering trigger prices, identities, keys, status/verification, quantities and episode associations. Actual prices, account details and financial payloads are not exported into this report or source fixtures.

The current snapshot difference comprises 130 quote capture timestamps, 56 item `finalActionUpdatedAt` values and the snapshot's `UpdatedAt`. No persisted transaction/quantity/history change was detected. This is consistent with live refresh, but the initiating actor was not independently proven. The earlier Phase 0 start/end comparison also recorded one quote open-price refresh; it is not misclassified as a remediation edit.

Latest preflight result-set counts:

- Live: `0, 0, 1, 0, 0`.
- Corrected isolated: `0, 0, 1, 0, 0, 0`.

The single preserved warning is an existing non-NULL-owner order linked to a NULL-owner episode with matching account/symbol. No ownership was claimed or corrected automatically. No new episode FK was introduced that would invalidate this legacy association. Ambiguous ownership requests intentionally fail closed rather than guessing; ownership reconciliation remains outside scope.

Backup-era current snapshot cache differences were not fully normalized against the earlier sequential capture. This does not invalidate immediate pre/post migration fingerprints, which separately prove the isolated migration/test operations did not alter captured financial/history values.

## 7. Outstanding safety gates and restart readiness

### Live migration executed (user-approved, 2026-10-28 evening)

The user explicitly approved applying `20261028213913_CorrectPm033EpisodeIntegrity` to `PortfolioManagerLocal`. Actions, in order:

1. API confirmed stopped. A fresh COPY_ONLY/CHECKSUM backup `PortfolioManagerLocal_PreLiveMigration_20261028-190820.bak` was written to the backup directory (29,220,864 bytes, SHA-256 `9016BD5F396175014E6E80B485A66E43BF39D54DFE271A1A38DBCD380F039CB9`); RESTORE VERIFYONLY WITH CHECKSUM succeeded. Same-volume limitation still applies.
2. Read-only preflight rerun: result-set rows `0, 0, 1, 0, 0` (only the known preserved legacy owner/episode association).
3. The five reviewed forward statements from [PM033_CorrectiveMigration.sql](../database/SQL/PM033_CorrectiveMigration.sql) were executed in one fail-fast client transaction and committed. No import, backfill or data update was run.
4. Verified: ledger 54 -> 55 including the corrective ID; `RiskAuditEvents.TradeEpisodeId` exists with zero attributed rows (no legacy inference); both new indexes present; old plan index removed. Counts unchanged: 80 episodes, 130 portfolio items, 4 plans, 12 stops, 21 audit events, eight original import keys present. Counts were unchanged again after backend startup.
5. Backend started with the existing script: `/api/health` HTTP 200; unauthenticated Swing endpoint HTTP 401. Frontend started with the existing HTTPS script: HTTP 200, proxy `/api/health` HTTP 200.

Still open: manual UAT (section 8). The generated EF `Down` is now guarded (see below); the "not ready for restart" statements further below are historical to this execution.

### Restart investigation follow-up

The user-reported restart failed before API startup with MSB3030: nested generated publish output was included as project content, referencing a missing static-web-assets file. This was reproduced using the ordinary backend build, without starting the API.

The [API project](../backend/PortfolioManager.Api/PortfolioManager.Api.csproj) now extends `DefaultItemExcludes` with `publish\**`. Generated output is excluded at item discovery rather than disabling all application content or deleting existing files. The ordinary `dotnet build` subsequently passed with zero errors and two existing Windows EventLog platform warnings. MSBuild item inspection confirmed zero publish content items and all three root appsettings files still included. This resolves the screenshot's build blocker; it does not apply the corrective migration or prove live startup readiness.

The separate live-migration authorization prompt again had no affirmative response. No modified live API was launched and no live migration was executed during this investigation.

**The modified API is not yet ready for live restart.**

No affirmative response was available to the final rollback-policy/live-migration approval prompt. The safe default is to retain both gates; absence of a response is not authorization.

1. **Live migration approval:** `PortfolioManagerLocal` still lacks the corrective migration. The Development startup guard refuses to automatically apply this specific pending migration before normal startup migration/seeding. Production migration policy was not changed; do not deploy as a substitute for approval.
2. **Rollback-policy decision (resolved):** the repository rule against editing generated migrations conflicted with the plan requirement that rollback refuse destructive execution when populated PM-033 data exists. Following the user's instruction to close all unresolved items, only the `Down` method of the corrective migration was changed (a leading guard that throws SQL error 51033 if PM-033 tables/links are populated); `Up`, the designer and the model snapshot are unchanged and the forward SQL is identical. The isolated SQL test proves: an EF downgrade against the populated restored catalog is refused with 51033 with the migration and data unchanged, and the same downgrade succeeds on an empty generated catalog. Direct EF downgrades are therefore safe to refuse, but the preferred rollback remains verified backup recovery.
3. **Controlled application procedure:** after explicit approval, quiesce API/background writers, refresh read-only preflight and reconciliation, confirm the recovery point is sufficiently current, then execute only reviewed forward SQL using a fail-on-error client. Do not rerun original import/backfill/deploy scripts.
4. **Post-application verification:** verify the expected ledger/index/column changes and reconcile source financial/order/audit records before declaring restart-ready. Investigate unexplained differences; do not overwrite legitimate activity.
5. **Manual restart/UAT:** only then restart services under the approved configuration, check startup and authenticated endpoints, and complete the checklist below. No live startup success or manual broker verification is claimed by mocked E2E.

## 8. Manual UAT checklist

Use synthetic transactions in an approved test environment for mutation scenarios unless real changes are separately intended. Do not change real broker orders merely to exercise this checklist.

- [ ] Confirm all eight original stops retain prices/identities/import keys and honest unknown quantity/verification values.
- [ ] Open Manage Risk from Portfolio and Swing: account and Swing facts agree, with separate quantities/costs and honest evidence.
- [ ] Add mixed-role exposure; preserve the account order and ambiguous coverage without treating it as Swing-specific.
- [ ] Partially sell Swing: broker covered quantity is not silently rewritten; known excess remains flagged.
- [ ] Close last Swing while Core/Strategic remains: episode closes, order/history and outstanding broker reminder remain.
- [ ] Open new Swing during continuous account exposure, and separately same-day after a flat account: a new episode is created and old protection is not inherited.
- [ ] Resolve a previous-order cancellation/fill explicitly with reason/current version; audit is appended, never broker-verified automatically.
- [ ] Check old/new Journal thesis and explicit episode events stay separate; legacy history is not inferred by dates.
- [ ] Check mixed/missing multi-lot Final Action labels and each lot's canonical details.
- [ ] Check missing source timestamps are not Live/EOD, then validate actual provider refresh/session evidence without changing established calendar grace.
- [ ] Verify risk-first default, user sorting, demo masking/read-only state, observations/history privacy and export guards.
- [ ] Verify desktop, Pixel Tablet, S21 and 360px S23 layouts and keyboard/accessibility behavior.
- [ ] Verify protected history-changing edit/delete/replacement restore refuses explicitly without quantity/history loss.
- [ ] Confirm startup/logs and normal PM-035, cash/options and canonical Final Action workflows after approved restart.

## 9. Changed-file map

- Integrity/persistence: [risk services](../backend/PortfolioManager.Api/Services/Risk/), [PortfolioService](../backend/PortfolioManager.Api/Services/PortfolioService.cs), [AccountTypeService](../backend/PortfolioManager.Api/Services/AccountTypeService.cs), [exception handler](../backend/PortfolioManager.Api/Middleware/AccountTypeExceptionHandler.cs), [risk models](../backend/PortfolioManager.Api/Models/RiskModels.cs), [AppDbContext](../backend/PortfolioManager.Api/Data/AppDbContext.cs), its snapshot/new migration/designer and [startup gate](../backend/PortfolioManager.Api/Program.cs).
- Quote provenance: [StockQuote](../backend/PortfolioManager.Api/Models/StockQuote.cs), [provider models](../backend/PortfolioManager.Api/Services/MarketDataModels.cs), [YahooFinanceService](../backend/PortfolioManager.Api/Services/YahooFinanceService.cs).
- Frontend state/contracts: [Swing store and tests](../frontend/portfolio-manager-ui/src/app/core/store/), [Swing models](../frontend/portfolio-manager-ui/src/app/core/models/swing-trading.models.ts), [quote model](../frontend/portfolio-manager-ui/src/app/core/models/portfolio.models.ts).
- UI: [Swing tabs/logic/dialog details](../frontend/portfolio-manager-ui/src/app/features/swing-trading/), [shared Manage Risk dialog and tests](../frontend/portfolio-manager-ui/src/app/shared/manage-risk-dialog/), [Portfolio entry point](../frontend/portfolio-manager-ui/src/app/features/portfolio/portfolio-page.component.ts).
- Regression evidence: the three backend test files and Angular store/dialog specs linked above; [Swing fixtures](../frontend/portfolio-manager-ui/e2e/fixtures/swing.ts) and Swing E2E.
- Database/documentation: the six SQL artifacts linked in section 5, this report, the historical Phase 0 report and updated [delivery summary](pm-033-swing-trading-delivery-summary.md).

**Final decision:** all automated verification passes, the live migration is applied and reconciled, and services run. PM-033 should be marked Done only after the user completes the manual UAT in section 8. Commit/push remain for the user to perform.
