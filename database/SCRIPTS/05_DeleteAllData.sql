-- ============================================================
-- SCRIPTS/05_DeleteAllData.sql
-- Deletes ALL rows from every application table while
-- KEEPING the table structure and the database intact.
-- USE WITH CAUTION – development / reset only.
-- ============================================================

USE PortfolioManagerDb;
GO

-- Disable foreign-key constraints temporarily (none exist yet, but included
-- as a safety measure for future schema additions).
EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';
GO

DELETE FROM [dbo].[NotificationRecipients];
DELETE FROM [dbo].[WatchlistItems];
DELETE FROM [dbo].[PortfolioItems];
-- Do NOT delete __EFMigrationsHistory – that would confuse EF Core.

-- Re-enable constraints
EXEC sp_MSforeachtable 'ALTER TABLE ? CHECK CONSTRAINT ALL';
GO

-- Reset identity sequences so IDs start from 1 again
DBCC CHECKIDENT('[dbo].[NotificationRecipients]', RESEED, 0);
DBCC CHECKIDENT('[dbo].[WatchlistItems]',          RESEED, 0);
DBCC CHECKIDENT('[dbo].[PortfolioItems]',          RESEED, 0);
GO

PRINT 'All application data deleted.  Identity columns reset to 1.';
GO
