# Regular Sync — Keeping Azure SQL Up to Date

After the first migration, run this whenever you want to push local data changes to Azure.

---

## When to Run

- You added new portfolio positions, transactions, or watchlist items locally
- You want to refresh Azure with the latest local data
- Before switching users from local to Azure as the primary app

---

## One Command

Double-click `scripts\migrate.bat`

Or in PowerShell:

```powershell
cd D:\PORTFOLIO-MANAGER
.\scripts\migrate-full.ps1
```

That's it. The script always does a full clean sync:

- Clears Azure data
- Imports all current local data

---

## Important: Direction is Always Local -> Azure

The migration script **replaces** Azure data with local data. It does not merge.

| Scenario                  | What happens                            |
| ------------------------- | --------------------------------------- |
| Data exists only locally  | Gets copied to Azure                    |
| Data exists only in Azure | Gets deleted                            |
| Data exists in both       | Azure version replaced by local version |

Once you start using Azure as your primary environment (all users logging in via the web app),
**stop running the migration** — Azure becomes the source of truth and local is for development only.

---

## Troubleshooting

### "Database is not currently available"

The DB auto-paused. Open Azure portal Query Editor, run any query, wait 30 seconds, retry.

### "Cannot connect to local SQL Server"

Make sure your local backend is running (`start-all.bat`), or that SQL Server service is started.

### Row counts don't match after migration

The DB may have timed out mid-import. Run the migration again.
