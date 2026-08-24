# Final Migration Scripts — Local ⇄ Azure SQL

Authoritative, up-to-date guide for keeping the local SQL Server database and the
Azure SQL database in sync. Supersedes the older notes in `docs/migration/` (kept
for history) — this is the current process as of **2026-08-24**.

## What changed in this pass

- Fixed a schema-drift bug: `__EFMigrationsHistory` was missing 2 rows
  (`AddPortfolioValueHistory`, `AddCashTransactionDate`) even though the
  corresponding table/column already existed locally. Repaired so future
  `dotnet ef database update` runs won't fail trying to recreate them.
- Replaced the hand-maintained "list of ALTER TABLE" schema-fix block in
  `scripts/migrate-full.ps1` with a **generic schema-sync engine**
  (`scripts/lib/SchemaSync.ps1`) that diffs live table/column metadata between
  source and target and auto-generates `CREATE TABLE` / `ALTER TABLE ADD COLUMN`
  — no more manually keeping two lists in sync (this is what caused
  `UserPreferences`, the grid column-configuration table, to be silently
  missing from Azure deploys).
- Added `UserPreferences` and `StagedSignals` to the deploy table list (they
  were missing from `migrate-full.ps1`, present in `migrate-local-to-azure.ps1`).
- Fixed a data-visibility bug: rows in `PortfolioItems` / `WatchlistItems` /
  `CashItems` / `OptionItems` / `UserPreferences` carry a `UserId`. After a
  migration the `UserId` still points at the **source** environment's user
  GUID, which doesn't exist in the **target** environment's `AspNetUsers`
  table — so migrated grid-column settings and holdings were invisible after
  login. Both scripts now reassign `UserId` to the target environment's Admin
  user automatically.
- Added a new reverse script, `scripts/restore-from-azure.ps1`, for pulling
  Azure data back down to local (e.g. after vacation). It backs up the local
  database first (`scripts/local-backups/*.bak`) before overwriting anything.
- Verified live end-to-end: ran `scripts/migrate-full.ps1` against the real
  Azure database — all 14 tables matched row-for-row (460 rows total), twice
  (2026-08-24, both runs clean).
- Added `scripts/backup-local-db.ps1` — full local `.bak` backup + a
  human-readable `.sql` data export, saved to `D:\PORTFOLIO-MANAGER-SQL-BACKUP\`.
- `BACKUP DATABASE ... WITH COMPRESSION` is not supported on SQL Server
  Express — removed `COMPRESSION` from both `backup-local-db.ps1` and
  `restore-from-azure.ps1`'s pre-restore backup step.
- **New default local database: `PortfolioManagerLocal`.** Cloned from the
  old `PortfolioManagerDb` via native `RESTORE DATABASE` (byte-perfect schema:
  all tables, indexes, constraints, local login accounts), then overlaid with
  Azure's exact business data via `restore-from-azure.ps1`. The backend's
  `appsettings.json` `DefaultConnection` now points at `PortfolioManagerLocal`.
  All three scripts default `$LocalDatabase` to `PortfolioManagerLocal`; pass
  `-LocalDatabase <name>` to target a different database. The old
  `PortfolioManagerDb` is left untouched as a fallback.
- Moved the Decision Source / Sector / Industry picklists out of
  `sector-industry-lists.json` (a loose file that was silently reset to
  defaults by a past commit, losing custom entries like "Manual - Buy on
  pullback") into a new `SectorIndustryConfigs` DB table (EF migration
  `AddSectorIndustryConfig`). Now covered by the normal DB backup/migration
  process. Added to the synced table list in all three scripts.
- Fixed a `UserPreferences` reassignment bug: when the source environment has
  multiple distinct users' preference rows, blindly reassigning all of them to
  one target admin violates the unique `(UserId, PreferenceKey)` index. Both
  scripts now de-duplicate (keep most-recently-updated per key) before
  reassigning. The verify step treats a `UserPreferences` count drop from this
  dedup as expected, not a mismatch.
- Both scripts now skip tables that don't exist yet on the source side (e.g.
  a brand-new table not yet deployed to Azure) instead of crashing.
- Fixed a session-timeout bug (unrelated to the "Session timeout (minutes)"
  setting): the refresh-token cookie was `SameSite=Strict`, which browsers
  block on cross-site requests — exactly the local-frontend/Azure-backend
  topology used in production. Every access-token expiry (15 min) silently
  failed to refresh and forced a re-login. `AuthController` now sets
  `SameSite=None; Secure=true` in production and `SameSite=Lax` locally.

## What is synced

Business + settings tables (exact list in `$tables` in both scripts):

```
AllocationRiskTargets, AllocationSectorTargets, SinglePositionLimits,
AdhocAnalysisSessions, PortfolioItems, WatchlistItems, CashItems,
OptionItems, DailySignals, StagedSignals, ValueScreenerScheduleConfigs,
ValueScreenerSnapshots, PortfolioValueHistories, UserPreferences,
SectorIndustryConfigs
```

`UserPreferences` is where saved grid column layouts/settings live — it is
included, so column configuration now travels with every deploy/restore.

### Intentionally excluded

- **Identity/auth tables** (`AspNetUsers`, `AspNetRoles`, `RefreshTokens`, etc.)
  — each environment keeps its own login accounts. Only the `UserId` values on
  the tables above are re-pointed to the correct account per environment.
- **Snapshot/cache tables** (`RsiScanSnapshots`, `PortfolioSnapshots`,
  `WatchlistSnapshots`) — ephemeral, regenerate automatically on first page
  load after deploy. Not worth syncing.

## Prerequisites (one-time)

1. Edit `scripts/migration-config.ps1` with your real Azure SQL connection
   string (this file is `.gitignore`d — never commit it).
2. Both scripts require `sqlcmd`/`System.Data.SqlClient` (ships with Windows +
   .NET) — no extra install needed.

## 0. Back up local database

```powershell
.\scripts\backup-local-db.ps1
```

Saves to `D:\PORTFOLIO-MANAGER-SQL-BACKUP\`:

- `PortfolioManagerDb_<timestamp>.bak` — full native SQL Server backup (every
  table/object, restorable with a standard `RESTORE DATABASE`).
- `PortfolioManagerDb_DataExport_<timestamp>.sql` — human-readable INSERT
  script for the business/settings tables (quick inspection, or a fallback
  data-only restore path).

Run this before any migration/restore, or anytime you want an independent
snapshot outside of `scripts/local-backups/` (which only holds pre-restore
safety backups).

## 1. Deploy: Local → Azure (single command)

```
scripts\migrate.bat
```

or in PowerShell:

```powershell
.\scripts\migrate-full.ps1
```

Steps performed automatically:

1. Connect to local + Azure SQL.
2. **Schema sync** — diff local vs Azure for every table in the list; create
   missing tables, add missing columns. Safe to re-run any time.
3. Delete all existing Azure data in the synced tables.
4. Export every row from local.
5. Import into Azure.
6. Reassign `UserId` on migrated rows to the Azure Admin account.
7. Verify row counts match **exactly** between local and Azure, table by
   table. Exits with a non-zero code and prints `MISMATCH` if anything is off.

## 2. Restore: Azure → Local (single command)

Use this after time away, to pull the latest Azure data back to your dev
machine.

```
scripts\restore-from-azure.bat
```

or in PowerShell:

```powershell
.\scripts\restore-from-azure.ps1
```

Steps performed automatically:

1. Connect to local SQL Server.
2. **Back up the local database first** to
   `scripts\local-backups\PortfolioManagerDb_PreRestore_<timestamp>.bak`
   (safety net — restore is destructive to local data).
3. Connect to Azure SQL.
4. Schema sync (Azure → local).
5. Delete all existing local data in the synced tables.
6. Export every row from Azure, import into local.
7. Reassign `UserId` on migrated rows to the local Admin account.
8. Verify row counts match exactly between Azure and local.

## Verified test run (2026-08-24)

Ran `migrate-full.ps1` against the real Azure database. Result: all 14 tables
matched exactly (460 total rows), including `UserPreferences` (1 row) and
`StagedSignals` (45 rows), with `UserId` correctly reassigned to the Azure
Admin account (`dima.pekerman@gmail.com`).

## Adding a new table/column in the future

Nothing to maintain manually anymore for schema — the sync engine reads live
metadata from the source database. Just:

1. Add the EF migration / SQL as usual and apply it locally.
2. Add the new table name to the `$tables` array in **both**
   `scripts/migrate-full.ps1` and `scripts/restore-from-azure.ps1` (keep them
   identical) if it's a table you want synced as data. New _columns_ on
   already-listed tables need no script changes at all.

## Files

| File                             | Purpose                                                |
| -------------------------------- | ------------------------------------------------------ | --- | ----------------------------- | -------------------------------------- | --- | -------------------------- | ------------------------------------- |
| `scripts/lib/SchemaSync.ps1`     | Generic table/column diff + CREATE/ALTER engine        |     | `scripts/backup-local-db.ps1` | Full local `.bak` + data `.sql` backup |     | `scripts/migrate-full.ps1` | Local → Azure deploy (single command) |
| `scripts/migrate.bat`            | Double-click launcher for the above                    |
| `scripts/restore-from-azure.ps1` | Azure → Local restore (single command)                 |
| `scripts/restore-from-azure.bat` | Double-click launcher for the above                    |
| `scripts/migration-config.ps1`   | Your Azure SQL connection string (**not in git**)      |
| `scripts/local-backups/`         | Pre-restore local `.bak` backups (**not in git**)      |
| `scripts/migration-output-*.sql` | Generated export files, local → Azure (**not in git**) |
| `scripts/restore-output-*.sql`   | Generated export files, Azure → local (**not in git**) |
