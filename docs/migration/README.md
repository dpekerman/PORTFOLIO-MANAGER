# Data Migration — Quick Reference

## Run a full clean migration (local -> Azure SQL)

```
scripts\migrate.bat
```

or in PowerShell:

```powershell
cd D:\PORTFOLIO-MANAGER
.\scripts\migrate-full.ps1
```

This does everything automatically:

1. Fixes any missing columns in Azure SQL
2. Deletes all existing Azure business data
3. Exports all local SQL data
4. Imports to Azure SQL
5. Verifies row counts

---

## Files in this folder

| File                                         | Purpose                                          |
| -------------------------------------------- | ------------------------------------------------ |
| [01-first-run.md](01-first-run.md)           | One-time setup before first migration            |
| [02-regular-sync.md](02-regular-sync.md)     | How to sync local data to Azure going forward    |
| [03-schema-changes.md](03-schema-changes.md) | What to do when tables change (new EF migration) |

---

## Key files

| File                             | Purpose                                           |
| -------------------------------- | ------------------------------------------------- |
| `scripts/migration-config.ps1`   | Your Azure SQL connection string (**not in git**) |
| `scripts/migrate-full.ps1`       | Master migration PowerShell script                |
| `scripts/migrate.bat`            | Double-click shortcut to run migration            |
| `scripts/migration-output-*.sql` | Generated SQL export files (**not in git**)       |
