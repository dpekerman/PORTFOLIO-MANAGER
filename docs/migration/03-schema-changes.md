# Schema Changes — New Tables or Columns

When you or a developer adds a new EF migration (new table or new column),
here is what happens automatically and what you need to do manually.

---

## What Happens Automatically

When a new EF migration is deployed to Azure (via `git push origin main`):

1. GitHub Actions builds and deploys the .NET API to App Service
2. App Service restarts
3. `Program.cs` runs `await db.Database.MigrateAsync()`
4. The new migration applies to Azure SQL automatically

**Schema changes require no manual action after the initial one-time setup.**

---

## What You May Need to Do Manually

### If the migration adds a new column with data (seed data)

Add the column seeding logic to the migration script. Open `scripts\migrate-full.ps1`
and add the new column to the `$schemaFixes` array if needed:

```powershell
# Example: new column added by a future migration
"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('PortfolioItems') AND name='NewColumn') ALTER TABLE [PortfolioItems] ADD [NewColumn] NVARCHAR(100) NULL"
```

### If the migration adds a new business table

Add the table name to the `$tables` array in `scripts\migrate-full.ps1`:

```powershell
$tables = @(
    "AllocationRiskTargets",
    ...
    "YourNewTableName"    # add here
)
```

Then run the migration as usual.

---

## Developer Workflow for Schema Changes

```powershell
# 1. Create the migration locally
cd D:\PORTFOLIO-MANAGER\backend\PortfolioManager.Api
dotnet ef migrations add AddYourFeature

# 2. Apply to local database
dotnet ef database update

# 3. Test locally with start-all.bat

# 4. Create a PR to develop, then develop -> main
# Azure SQL gets the schema change automatically on next deployment
```

---

## Checklist When a New Migration Is Added

- [ ] `dotnet ef migrations add` created and applied locally
- [ ] App tested locally
- [ ] PR submitted for review (do not push directly to develop/main)
- [ ] After PR merges to main: CD deploys, Azure SQL schema updates automatically
- [ ] If new table or new important columns: update `$tables` or `$schemaFixes` in `migrate-full.ps1`
- [ ] Run `.\scripts\migrate-full.ps1` if you want fresh local data in Azure
