-- ============================================================
-- Portfolio Manager - Full Azure SQL Reference Script
-- Run on a FRESH Azure SQL database to recreate full schema
-- All tables created by EF MigrateAsync() on App Service startup
-- This script is for REFERENCE and manual recovery only
-- Last updated: 2026-08-23
-- ============================================================
-- ============================================================
-- SECTION 1: Verify connection
-- ============================================================
SELECT DB_NAME() AS [Database],
       GETUTCDATE() AS [ServerTimeUTC];


GO
-- ============================================================
-- SECTION 2: ASP.NET Identity tables (created by EF migrations)
-- These are auto-created — included here for reference only
-- ============================================================
-- AspNetUsers, AspNetRoles, AspNetUserRoles, AspNetUserClaims,
-- AspNetUserLogins, AspNetUserTokens, AspNetRoleClaims
-- RefreshTokens (custom)
-- ============================================================
-- SECTION 3: Business tables (created by EF migrations)
-- ============================================================
IF NOT EXISTS (SELECT 1
               FROM   sys.tables
               WHERE  name = 'PortfolioItems')
    RAISERROR ('PortfolioItems missing — run EF migrations first: dotnet ef database update', 16, 1);


GO
-- ============================================================
-- SECTION 4: Snapshot tables (added 2026-08-21/22)
-- ============================================================
IF NOT EXISTS (SELECT 1
               FROM   sys.tables
               WHERE  name = 'RsiScanSnapshots')
    BEGIN
        CREATE TABLE [RsiScanSnapshots] (
            [Id]              INT            NOT NULL CONSTRAINT [PK_RsiScanSnapshots] PRIMARY KEY,
            [SnapshotJson]    NVARCHAR (MAX) DEFAULT N'{}' NOT NULL,
            [ScannedAt]       DATETIME2      DEFAULT GETUTCDATE() NOT NULL,
            [SymbolCount]     INT            DEFAULT 0 NOT NULL,
            [OversoldCount]   INT            DEFAULT 0 NOT NULL,
            [OverboughtCount] INT            DEFAULT 0 NOT NULL
        );
        PRINT 'Created RsiScanSnapshots';
    END


GO
IF NOT EXISTS (SELECT 1
               FROM   sys.tables
               WHERE  name = 'UserPreferences')
    BEGIN
        CREATE TABLE [UserPreferences] (
            [Id]              INT            IDENTITY (1, 1) NOT NULL,
            [UserId]          NVARCHAR (450) NOT NULL,
            [PreferenceKey]   NVARCHAR (100) NOT NULL,
            [PreferenceValue] NVARCHAR (MAX) DEFAULT N'' NOT NULL,
            [UpdatedAt]       DATETIME2      DEFAULT GETUTCDATE() NOT NULL,
            CONSTRAINT [PK_UserPreferences] PRIMARY KEY ([Id])
        );
        CREATE UNIQUE INDEX [IX_UserPreferences_UserId_PreferenceKey]
            ON [UserPreferences]([UserId], [PreferenceKey]);
        PRINT 'Created UserPreferences';
    END


GO
IF NOT EXISTS (SELECT 1
               FROM   sys.tables
               WHERE  name = 'PortfolioSnapshots')
    BEGIN
        CREATE TABLE [PortfolioSnapshots] (
            [UserId]       NVARCHAR (450) NOT NULL CONSTRAINT [PK_PortfolioSnapshots] PRIMARY KEY,
            [SnapshotJson] NVARCHAR (MAX) DEFAULT N'[]' NOT NULL,
            [UpdatedAt]    DATETIME2      DEFAULT GETUTCDATE() NOT NULL,
            [ItemCount]    INT            DEFAULT 0 NOT NULL
        );
        PRINT 'Created PortfolioSnapshots';
    END


GO
IF NOT EXISTS (SELECT 1
               FROM   sys.tables
               WHERE  name = 'WatchlistSnapshots')
    BEGIN
        CREATE TABLE [WatchlistSnapshots] (
            [UserId]       NVARCHAR (450) NOT NULL CONSTRAINT [PK_WatchlistSnapshots] PRIMARY KEY,
            [SnapshotJson] NVARCHAR (MAX) DEFAULT N'[]' NOT NULL,
            [UpdatedAt]    DATETIME2      DEFAULT GETUTCDATE() NOT NULL,
            [ItemCount]    INT            DEFAULT 0 NOT NULL
        );
        PRINT 'Created WatchlistSnapshots';
    END


GO
-- ============================================================
-- SECTION 5: Post-migration UserId fix
-- Run this after every data migration to reassign
-- migrated rows (which have the LOCAL admin's UserId)
-- to the AZURE admin user's actual UserId
-- ============================================================
/*
DECLARE @AdminId NVARCHAR(450);
SELECT TOP 1 @AdminId = u.Id
FROM AspNetUsers u
JOIN AspNetUserRoles ur ON u.Id = ur.UserId
JOIN AspNetRoles r      ON ur.RoleId = r.Id
WHERE r.Name = 'Admin';

IF @AdminId IS NULL
    SELECT TOP 1 @AdminId = Id FROM AspNetUsers ORDER BY CreatedAt;

SELECT 'Reassigning to Azure admin: ' + ISNULL(@AdminId, 'NO USER FOUND') AS Info;

UPDATE PortfolioItems SET UserId = @AdminId;
UPDATE WatchlistItems SET UserId = @AdminId;
UPDATE CashItems      SET UserId = @AdminId;
UPDATE OptionItems    SET UserId = @AdminId;

SELECT 'Updated PortfolioItems: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' rows' AS Result;
*/
-- Uncomment and run the block above after data migration
-- ============================================================
-- SECTION 6: Verify table inventory
-- ============================================================
SELECT   t.name AS [Table],
         SUM(p.rows) AS [Rows]
FROM     sys.tables AS t
         INNER JOIN
         sys.partitions AS p
         ON t.object_id = p.object_id
            AND p.index_id IN (0, 1)
GROUP BY t.name
ORDER BY t.name;