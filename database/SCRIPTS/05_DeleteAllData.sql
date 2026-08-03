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
DELETE FROM [dbo].[CashItems];
DELETE FROM [dbo].[OptionItems];
-- Do NOT delete __EFMigrationsHistory – that would confuse EF Core.

-- ── Auth tables (FK-safe order) ──────────────────────────────────────────────
IF OBJECT_ID('dbo.RefreshTokens',    'U') IS NOT NULL DELETE FROM [dbo].[RefreshTokens];
IF OBJECT_ID('dbo.AspNetUserRoles',  'U') IS NOT NULL DELETE FROM [dbo].[AspNetUserRoles];
IF OBJECT_ID('dbo.AspNetUserClaims', 'U') IS NOT NULL DELETE FROM [dbo].[AspNetUserClaims];
IF OBJECT_ID('dbo.AspNetUserLogins', 'U') IS NOT NULL DELETE FROM [dbo].[AspNetUserLogins];
IF OBJECT_ID('dbo.AspNetUserTokens', 'U') IS NOT NULL DELETE FROM [dbo].[AspNetUserTokens];
IF OBJECT_ID('dbo.AspNetRoleClaims', 'U') IS NOT NULL DELETE FROM [dbo].[AspNetRoleClaims];
IF OBJECT_ID('dbo.AspNetUsers',      'U') IS NOT NULL DELETE FROM [dbo].[AspNetUsers];
IF OBJECT_ID('dbo.AspNetRoles',      'U') IS NOT NULL DELETE FROM [dbo].[AspNetRoles];

-- Re-enable constraints
EXEC sp_MSforeachtable 'ALTER TABLE ? CHECK CONSTRAINT ALL';
GO

-- Reset identity sequences so IDs start from 1 again
DBCC CHECKIDENT('[dbo].[NotificationRecipients]', RESEED, 0);
DBCC CHECKIDENT('[dbo].[WatchlistItems]',          RESEED, 0);
DBCC CHECKIDENT('[dbo].[PortfolioItems]',          RESEED, 0);
DBCC CHECKIDENT('[dbo].[CashItems]',               RESEED, 0);
DBCC CHECKIDENT('[dbo].[OptionItems]',             RESEED, 0);
GO

PRINT 'All application data deleted.  Identity columns reset to 1.';
GO
