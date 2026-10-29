# PM-033 Phase 0 - Baseline, reproduction and database protection report

Date: 2026-10-28. Branch: `feature/pm-033-swing-trading-workspace`.

Historical Phase 0 record. Subsequent authorized implementation, recovery operations and current readiness are documented in the [remediation and recovery report](pm-033-remediation-recovery-report.md); the statements below describe the Phase 0 boundary only.

**Status: Phase 0 evidence collected; STOP at the review gate. Not ready for commit/push or final PM-033 sign-off.**

No production application code was changed. No SQL Server backup, restore, writing test, migration, import, backfill, live correction, commit or push was executed. Reproduction tests intentionally fail against current behavior; this is baseline evidence, not a passing remediation suite.

## 1. Git baseline and changed files

- HEAD: `7163706` (`add calendars`), following PM-033 implementation `e566cc8`.
- Worktree was clean before this phase; local `develop...HEAD` left/right count was `0 2`.
- No fetch was performed. These statements compare local references, not verified remote branch freshness.
- Existing calendar/date-only dialog changes were preserved.
- No subagents or API hosts were started. The existing user-running API was not stopped.

Phase 0 changes:

| File | Purpose |
|---|---|
| [Pm033RemediationReproductionTests.cs](../backend/PortfolioManager.Tests/Pm033RemediationReproductionTests.cs) | Synthetic, EF InMemory correctness assertions exposing existing backend defects |
| [swing-trading.store.spec.ts](../frontend/portfolio-manager-ui/src/app/core/store/swing-trading.store.spec.ts) | Mixed and incomplete lot-action reproductions |
| [manage-risk-dialog.component.spec.ts](../frontend/portfolio-manager-ui/src/app/shared/manage-risk-dialog/manage-risk-dialog.component.spec.ts) | Demo-mode average-cost disclosure reproduction |
| [PM033_PreRemediation_Schema.sql](../database/SQL/PM033_PreRemediation_Schema.sql) | Generated schema-only actual-database snapshot, not an executable deployment recommendation |
| This report | Sanitized findings, validation, recovery preparation and review gate |

Detailed financial evidence, per-record SHA-256 digests and raw test reports are private session artifacts, not repository fixtures.

## 2. Read-only database baseline and reconciliation

- Connection: `localhost`, resolving to SQL Server `HOME-LAPTOP`.
- SQL Server: `15.0.2200.2`, Express Edition (64-bit).
- Database: `PortfolioManagerLocal`.
- Database files: 72 MiB data and 72 MiB log at inspection; physical locations are recorded only in approved future restore preparation.
- Start capture: `2026-10-28T21:04:09.8577056Z`.
- Isolation: existing READ COMMITTED; RCSI disabled and SNAPSHOT isolation OFF.
- Capture used sequential read-only queries, not a cross-table transactionally consistent snapshot. No database options or intrusive locking were introduced.

| Data set | Start count | Ending comparison |
|---|---:|---|
| TradeEpisodes | 80 | All captured fields/digests unchanged |
| PortfolioItems | 130 | All captured fields/digests unchanged |
| PortfolioItems with episode link | 82 | No orphan or owner/account/symbol mismatch found |
| RiskPlans | 4 | All fields/digests unchanged |
| BrokerStopRecords | 12 | All fields/digests unchanged |
| RiskAuditEvents | 21 | All fields/digests unchanged |
| Risk-related AuditLogs | 21 | All fields/digests unchanged |
| AccountTypes | 8 | Unchanged |
| CashItems | 47 | Unchanged |
| OptionItems | 30 | Unchanged |
| CashLedgerSettings | 1 | Unchanged |
| PortfolioSnapshots | 3 | One current snapshot refreshed; see below |
| PortfolioValueHistories | 85 | Unchanged |
| TransactionContextSnapshots | 39 | Unchanged |
| UnlinkedTradeBaselines | 8 | Unchanged |

These are this phase's observations, not fixed target counts.

### Original eight broker orders

All eight expected original import keys are present exactly once. Their expected symbol/account/trigger/order type/reported status/verification/unknown coverage checks passed. Stable order identities are nonempty. Start/end digests confirm no change to any of the twelve order rows, including identities, import keys and associations. All existing risk events and risk AuditLogs also remained unchanged. Actual financial records/prices are deliberately omitted from this repository report.

### Integrity checks

- Zero orphan portfolio episode links, plan episode links or order episode links.
- Zero duplicate import keys, order identities or current owner/account/symbol plan keys.
- Zero orphan risk-event plan/order links.
- Zero portfolio-to-episode or plan-to-episode owner/account/symbol mismatches.
- **One existing order-to-episode ownership-partition mismatch:** an imported, non-NULL-owner order references a NULL-owner episode; symbol and account agree. This was present in the starting baseline. It is not proof of a different identified user's data being corrupted, but must be considered before introducing ownership constraints or association changes. Preserve the order/history; do not silently reassign it.

### Concurrent snapshot activity

One current PortfolioSnapshot changed `SnapshotJson` and `UpdatedAt`; item count remained 130. Comparison by stable holding ID found:

- No missing or reordered entries.
- Only `finalActionUpdatedAt` changed in 56 embedded item payloads.
- Quote capture `timestamp` changed in 130 entries and `openPrice` changed in one.
- No other embedded item fields changed; no persisted PortfolioItems or historical value/context rows changed.

This is consistent with live market/snapshot refresh rather than remediation writes. The initiating live user/background task was not independently identified, so attribution is not claimed as proven. Do not overwrite or normalize this activity to force checksum equality. Structure metadata, including migration history, was identical at the end.

## 3. Database structure and migration inventory

The [schema-only snapshot](../database/SQL/PM033_PreRemediation_Schema.sql) was generated using installed SQL Server SMO with data scripting disabled.

Verified coverage:

- 49 actual user tables, 510 columns, 49 primary keys, 10 foreign keys and 66 defaults.
- Table names matched actual metadata exactly. Index definitions, uniqueness and filters were included.
- Actual metadata has zero check constraints, views, stored procedures, functions, triggers and sequences. These classes are absent, not silently omitted by the export.
- Migration ledger is preserved as comments only.
- No INSERT/MERGE business data, login/credential creation or password assignment statements were found.
- Database settings/files, server configuration, principals/permissions and source code are not backed up by this artifact.
- SHA-256: `6D45804A3A4753C3B27033A00EFC16AC04BC0D371A5289CB918E9A1401C73C94`.
- The generated SQL was inspected, not executed or proven reconstructable by replay.

### PM-033 structure

- TradeEpisodes has a GUID primary key and account/symbol/status lookup index.
- PortfolioItems has nullable `TradeEpisodeId` and a lookup index, but no SQL FK enforcing that episode relationship.
- RiskPlans has identity PK, nullable owner/episode, rowversion and unique owner/account/symbol index filtered on `[UserId] IS NOT NULL`. It is not episode-aware; NULL-owner rows are outside that uniqueness rule.
- BrokerStopRecords has identity PK, unique OrderIdentity, unique non-NULL ImportKey, account/symbol/status lookup and rowversion.
- RiskAuditEvents has plan/order FKs with NO ACTION delete behavior and history lookup indexes, but no episode-attribution column.
- Episode IDs in plans/orders are not currently protected by episode FKs. This is why read-only orphan/scope checks are necessary.

### Applied and repository migrations

- Database ledger: 54 IDs. Repository: 52 migration implementation files.
- Latest known migration in both: `20261028145833_AddTradeEpisodesAndRiskTracking`.
- No migration filename ID was absent from the database ledger; no corrective migration has been generated. This is a source/ledger comparison, not a claim that every implementation is EF-discoverable.
- Two ledger IDs have no matching repository migration implementation/designer:
  - `20261023210946_AddPreMarketAgent`
  - `20261023221055_AddSecuritySectorCache`
- The database also has related pre-market/cache objects. Do not delete the ledger entries or objects. Their historical deployment provenance is unresolved; clean-database migration replay alone must not be presented as complete recovery of this database.
- A second source check found only 46 explicit Migration attributes. Six implementation files lack matching designer metadata/attributes: AddWatchlistFavorite, AddDecisionSourceAndCashAccount, AddAllocationRiskTables, AddValueScreenerPersistence, AddPortfolioValueHistory and AddCashTransactionDate (July 2026). Their filename IDs are in the live ledger, but migration discovery/replay completeness must not be assumed from filenames. Do not repair these unrelated historical migrations during PM-033 remediation; preserve the actual-schema snapshot and validate recovery using an approved full restore.

## 4. Existing SQL script inventory and risks

Read-only inventory covered 53 existing SQL files across [database/SQL](../database/SQL) and [database/SCRIPTS](../database/SCRIPTS), excluding the new schema snapshot.

| Script group | Observed behavior / status |
|---|---|
| `00_MASTER_DeployProduction.sql` | Creates/upgrades schema and seeds data; targets `PortfolioManagerDb`, not the actual local catalog. Does not constitute a complete current PM-033 deployment or a backup. Do not execute blindly. |
| `01_CreateDatabase.sql`, `02_CreateTables.sql` | Initial schema/bootstrap conventions, not proof of exact current schema. Some rebuild/index operations are destructive. |
| `03_SeedData.sql`, `04_SeedNotificationRecipients.sql` | Data/default seeding and migration-ledger writes. Not read-only and not a safe live remediation step. |
| `05_DeleteAllData.sql`, `06_DropAll.sql` | Destructive row deletion/database drop. Never run on the real database for this task. |
| `07` through `18`, and `20` | Incremental feature/schema scripts, including duplicate `13`/`14` numbering and a staged-signals rollback. Inspect dependencies individually; filenames do not prove execution. |
| `09_SetStrategicIncomeRole.sql` | Updates existing portfolio role data, which affects episode interpretation. Not authorized to rerun. |
| `19_AddAnalysisCurrencyToDailySignals.sql` | Present under SCRIPTS, absent from SQL. |
| `23_AddAccountTypes.sql`, `24_WidenAccountTypeNames.sql` | Account catalog/data/history and widening/index changes; not an episode reconciliation mechanism. |
| `25_CreateAuditLog.sql`, `26_CreateSignalAuditEvents.sql` | Audit structure/history deployment scripts; do not recreate or rewrite audit data. |
| `27_AddTradeEpisodesAndRiskTracking.sql` | Applied migration ID is in the ledger and objects exist. Do not rerun or treat it as the corrective migration. |
| `27_Rollback_TradeEpisodesAndRiskTracking.sql` | **Unsafe:** populated-data THROW and subsequent DROP/history-deletion statements are separated by GO. A client continuing after errors can reach destructive statements. Execution deliberately not attempted. |

Duplicate script trees differ for master deployment, create-tables and delete-all-data scripts. Do not assume the two directories are interchangeable. Standalone script execution history is **unknown** unless independently recorded; migration-ledger presence does not prove which script/client deployed an object.

Existing backup infrastructure:

- DatabaseBackupService schedules native full backups and copies SQL scripts, using `WITH INIT`.
- The local PowerShell backup script uses `WITH FORMAT`; its separate table-data export omits PM-033 tables.
- Neither was executed or modified. Neither provides the full required checksum/trial-restore/reconciliation workflow.
- SQL Server Agent is stopped; application-hosted background scheduling exists independently.

## 5. Backup command proposal and recovery procedure

**Status: PREPARED ONLY / NOT EXECUTED / SEPARATE AUTHORIZATION REQUIRED.**

Approved proposed root: `D:\BACKUP DB AND CODE - RemediationPlanAmendment - 2026-10-28`.

- Directory exists, is readable and remained empty at inspection.
- Drive D had approximately 212.7 GiB free when checked.
- SQL Server service identity is `NT Service\MSSQLSERVER`.
- SQL login inspection reports sysadmin, VIEW DEFINITION and BACKUP DATABASE permission. This does not authorize using those privileges.
- Directory ACL includes Authenticated Users modify and Users read/execute. Effective SQL service access was not proven; broad inherited access is also a confidentiality consideration for future real-data backups.
- No ACLs or service identities were changed; no write probe was made.

After separate authorization, the operator procedure should resolve a new timestamped filename, refuse collisions, check capacity/effective service access and execute this form with an error-stopping client:

```sql
-- REVIEW ONLY. Substitute a NEW approved timestamp before authorized execution.
BACKUP DATABASE [PortfolioManagerLocal]
TO DISK = N'D:\BACKUP DB AND CODE - RemediationPlanAmendment - 2026-10-28\PortfolioManagerLocal_PrePM033Remediation_<yyyyMMdd-HHmmss>.bak'
WITH COPY_ONLY, CHECKSUM, STOP_ON_ERROR, STATS = 10;
```

The surrounding execution wrapper must fail if the file exists and on any SQL/client error; do not append to an existing media file or use INIT/FORMAT. Do not request compression on this Express instance. Record actual start/completion time, server/catalog, path and resulting metadata.

The read-only `msdb` inventory lists an existing full backup at 14:32:52 without copy-only/checksums and earlier 12:00:25/10:59:41 full entries with copy-only/checksums on this date. These are historical ledger entries only, not verified file existence or recoverability; none was created by this phase.

After separately authorized backup:

1. Inspect RESTORE HEADERONLY and FILELISTONLY metadata and backup-set position.
2. Execute RESTORE VERIFYONLY WITH CHECKSUM for that exact set.
3. Allocate a distinct approved isolated catalog, for example `PortfolioManager_PM033Test_<stamp>`, and new data/log paths. Resolve actual logical names from FILELISTONLY; do not guess them from the catalog name.
4. RESTORE DATABASE into that isolated catalog with explicit MOVE mappings and the exact backup-set position. Refuse existing catalogs/files. Never use REPLACE or target PortfolioManagerLocal.
5. Execute DBCC CHECKDB on the restored database with appropriate resource checks.
6. Reconcile IDs, row/value digests, original stops and audit history. Account for the backup capture boundary and legitimate later live activity.
7. Record commands/outcomes and test the documented restoration procedure.

VERIFYONLY alone does not establish recovery. No backup file, code archive or tested restore currently exists in the new package root. A source snapshot, if wanted, is a separate authorized operation and must preserve uncommitted changes without copying secrets. Do not confuse a directory name with an existing DB-and-code backup.

## 6. Findings reproduction matrix and proposed minimal corrections

| # | Result and executable/source evidence | Proposed correction / main risk |
|---|---|---|
| 1 | **REPRODUCED:** two new store tests return a single `Hold Swing` despite conflicting or missing lot results. | Complete all-lot display assessment, mixed label and explicit unavailable state; preserve canonical engine rules and wire filters/details/exports. |
| 2 | **REPRODUCED:** `Finding02_existing_order_survives_later_non_swing_addition` sees an empty broker-order list after adding Strategic exposure. | Separate Swing identity from continuous account exposure; preserve existing order association/history without falsely allocating it to Swing. |
| 3 | **REPRODUCED:** three tests show the episode stays Open after last Swing exit with other roles remaining, protected deletion is accepted, and account rename leaves risk records at the old account. Source inspection also confirms replacement restore discards episode links. | Targeted lifecycle hooks and fail-before-mutation guards. Preserve quantities/IDs and reject ambiguity; do not implement a general history-reconciliation subsystem. Full restore/update permutations still need Phase 1 tests. |
| 4 | **REPRODUCED:** both another-owner and legacy NULL-owner fixtures receive the other partition's episode ID on addition. This is synthetic proof of the unsafe path, not proof that cross-identified-user corruption has already occurred live. | Exact ownership partitions for mutation/grouping/history; keep legacy read visibility separate. Existing NULL-owner order association needs explicit treatment, not automatic claiming. |
| 5 | **REPRODUCED:** NULL, empty and CLOSED market state plus capture time become EOD without source provenance. | Nullable provider provenance and conservative session evidence; maintain Timestamp meaning and legacy compatibility. |
| 6 | **SOURCE-CONFIRMED; SQL REPRODUCTION BLOCKED:** reviewed actual THROW/GO/DROP sequence. No destructive SQL test was run. | Single guarded transactional rollback with fail-closed populated/unknown state and continue-on-error client tests on an approved isolated restore. |
| 7 | **REPRODUCED:** explicit cancellation report for an EpisodeClosed stop throws "already inactive." Open-only rows also exclude fully exited positions. | Discoverable outstanding broker checks and versioned append-only user-reported resolution; never claim actual broker cancellation/verification. |
| 8 | **REPRODUCED:** after close/reopen/new plan, original Journal thesis becomes NULL. | Episode-aware plans/new audit attribution with one Phase 1 migration; preserve old IDs/values/events, no inferred historical rewrite. |
| 9 | **REPRODUCED:** four cases hide missing plan, known excess with unknown/mixed coverage, or attention for material partial protection. | Independent risk conditions and coverage confidence; preserve thresholds and advisory language. |
| 10 | **REPRODUCED:** unversioned existing-plan update is accepted. Actual SQL rowversion races remain untested. | Mandatory valid versions for existing mutations plus explicit conflicts/create-race handling; SQL proof requires isolated authorization. |
| 11 | **REPRODUCED:** both original ordering-dependent E2E scenarios fail on all four devices: actual first row is MAL rather than the expected highest-risk DIS row. | Risk-first default with deterministic ties; keep meaningful assertions and user sorting. |
| 12 | **REPRODUCED:** demo-mode DOM exposes unmasked average cost. Source trace confirms Swing-only money versus account-wide quantities and different facts from Portfolio entry. | Authoritative separate account/Swing facts plus complete masking across dialog/history/warnings/exports; preserve calendar changes and financial formulas. |

No original finding was withdrawn. Finding 6 lacks executed SQL reproduction; concurrency/DDL and complete scenario permutations are not claimed verified. Additional source observations are not presented as completed runtime tests.

## 7. Test execution and limitations

| Validation | Actual result |
|---|---|
| Backend selected existing PM-033, Final Action, evidence, calendar, account and related tests | 405 passed |
| New backend reproduction cases | 16 failed as expected, 0 skipped |
| Combined backend run | 421 total: 405 passed, 16 failed, 0 skipped |
| Additional cash rules/ledger/trade-link/snapshot run | 73 passed, 0 failed, 0 skipped; overlaps the first run in one existing test, so do not sum as unique coverage |
| Focused Angular store/logic/dialog tests | 55 total: 52 existing passed, 3 new reproduction tests failed, 0 skipped |
| Angular production build | Passed with existing component stylesheet budget warnings |
| Four-device Swing Playwright | 104 total: 90 passed, 8 failed, 6 skipped, 0 flaky |
| SQL Server writing/concurrency/migration/rollback/restore tests | NOT EXECUTED; separate authorization absent |
| Live mutation UAT / real stop import / backfill | NOT EXECUTED |

Playwright skips are device applicability, not new exclusions:

- Two phone header-sort cases: handset card controls are used instead.
- Two desktop/tablet phone-layout cases: phone-only assertions.
- Two phone keyboard cases: desktop/tablet keyboard flow.

All eight failures are the same two existing scenarios on four devices. Assertions were not weakened. The mock fixture intercepts localhost `/api/` requests; no .NET API was started. Test-owned port 4217 had no remaining listener afterward.

### Commands and build isolation

.NET SDK `9.0.203`, xUnit v2/VSTest, test target net9.0 and referenced API target net8.0. No global.json/Directory.Build.props/Directory.Packages.props overrides were found.

The initial backend command failed before tests because existing recursive publish content referenced a missing static-web-assets file. It was not counted as a defect reproduction. The successful run used a session-local output directory, `UseAppHost=false` and command-line-only `EnableDefaultContentItems=false`; no project/build policy was edited and no user process was stopped.

```powershell
dotnet test backend\PortfolioManager.Tests\PortfolioManager.Tests.csproj `
  "-p:OutputPath=<private-session-output>\" -p:UseAppHost=false -p:EnableDefaultContentItems=false `
  --filter "FullyQualifiedName~Pm033RiskTests|FullyQualifiedName~Pm033RemediationReproductionTests|FullyQualifiedName~FinalAction|FullyQualifiedName~PortfolioEvidenceGate|FullyQualifiedName~TradingCalendar|FullyQualifiedName~AccountTypeTests|FullyQualifiedName~CashTransaction|FullyQualifiedName~TradeCash|FullyQualifiedName~TransactionContext" `
  --logger "trx;LogFileName=backend-phase0.trx" --results-directory "<private-session-results>"
```

The additional backend run used `--no-build` and filter `CashFlowTypeRulesTests|CashLedgerTests|CashTradeLinkTests|UnlinkedTradeAndSnapshotTests`, each qualified with `FullyQualifiedName~`.

From the UI project:

```powershell
npx ng test --watch=false --include "**/swing-trading.store.spec.ts" `
  --include "**/swing-trading-logic.spec.ts" --include "**/manage-risk-dialog.component.spec.ts" `
  --reporters=json --output-file "<private-session-results>\angular-tests.json"
npx ng build
$env:E2E_PORT = '4217'
$env:E2E_BASE_URL = 'http://localhost:4217'
npx playwright test swing-trading.spec.ts --project=desktop-chrome --project=tablet `
  --project=mobile-s21 --project=mobile-s23 --workers=4 --reporter=json
```

Backend reproduction fixtures use uniquely named EF InMemory databases and no host, network or SQL connection. The null market-data dependency follows existing test prior art and is not invoked by these paths. Existing SQL scratch tests were inspected and excluded: they write/import/backfill and return trivially when unconfigured, which must not be counted as SQL validation. SQLite/InMemory cannot prove SQL Server constraints, rowversion or DDL safety.

## 8. Phase 1 readiness and required approvals

**Assessment: ready for Phase 0 review; NOT ready for unrestricted Phase 1 execution or persistence sign-off.**

Prepared evidence supports targeted corrections, but remaining gates are real:

1. Review this Phase 0 report and authorize Phase 1 code/test work.
2. Separately authorize the uniquely named native backup into the proposed package root; confirm adequate confidentiality/effective service access.
3. Separately authorize metadata/VERIFYONLY, isolated restore, consistency checks and private reconciliation. Approve actual isolated catalog/data/log paths before execution.
4. Separately authorize SQL writing tests, forward corrective migration testing and rollback refusal tests on the isolated restore only.
5. Review the existing owner-partition mismatch and unmatched migration-history IDs before any constraint design assumes clean ownership or complete migration-only reconstruction.

Phase 1 must prepare ONE corrective migration and read-only conflict preflight. Existing migration files must remain unchanged, episode-specific Journal persistence must be included now, and any extra current-position anchor must be justified by isolated tests. No live migration or repair is authorized.

Keep the modified API away from the real database: startup invokes MigrateAsync and account initialization, then hosted services can run timers. Disabling migration alone would not make a new-schema API compatible with the old schema.

**STOP here.** Phases 1-5 remain pending their review gates. PM-033 remains In Progress. There is no commit/push readiness claim, no verified recovery claim and no authorization to bypass a gate for overnight completion.
