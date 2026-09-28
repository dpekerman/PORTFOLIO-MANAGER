-- ============================================================
-- Portfolio Manager -- New Snapshot Tables
-- Run on Azure SQL after deploying backend (EF MigrateAsync also runs these on startup)
-- All statements are idempotent: safe to run multiple times
-- ============================================================
-- RSI Scan Snapshot (single-row upsert, Id always = 1)
IF NOT EXISTS (SELECT 1
               FROM   sys.tables
               WHERE  name = 'RsiScanSnapshots')
    BEGIN
        CREATE TABLE [RsiScanSnapshots] (
            [Id]              INT            NOT NULL CONSTRAINT [PK_RsiScanSnapshots] PRIMARY KEY,
            [SnapshotJson]    NVARCHAR (MAX) CONSTRAINT [DF_RsiSnap_Json] DEFAULT N'{}' NOT NULL,
            [ScannedAt]       DATETIME2      DEFAULT GETUTCDATE() NOT NULL,
            [SymbolCount]     INT            DEFAULT 0 NOT NULL,
            [OversoldCount]   INT            DEFAULT 0 NOT NULL,
            [OverboughtCount] INT            DEFAULT 0 NOT NULL
        );
        PRINT 'Created RsiScanSnapshots';
    END
ELSE
    PRINT 'RsiScanSnapshots already exists — skipped';


GO
-- Per-user preferences (key-value store)
IF NOT EXISTS (SELECT 1
               FROM   sys.tables
               WHERE  name = 'UserPreferences')
    BEGIN
        CREATE TABLE [UserPreferences] (
            [Id]              INT            IDENTITY (1, 1) NOT NULL,
            [UserId]          NVARCHAR (450) NOT NULL,
            [PreferenceKey]   NVARCHAR (100) NOT NULL,
            [PreferenceValue] NVARCHAR (MAX) CONSTRAINT [DF_UserPref_Value] DEFAULT N'' NOT NULL,
            [UpdatedAt]       DATETIME2      DEFAULT GETUTCDATE() NOT NULL,
            CONSTRAINT [PK_UserPreferences] PRIMARY KEY ([Id])
        );
        CREATE UNIQUE INDEX [IX_UserPreferences_UserId_PreferenceKey]
            ON [UserPreferences]([UserId], [PreferenceKey]);
        PRINT 'Created UserPreferences';
    END
ELSE
    PRINT 'UserPreferences already exists — skipped';


GO
-- Per-user portfolio quotes snapshot
IF NOT EXISTS (SELECT 1
               FROM   sys.tables
               WHERE  name = 'PortfolioSnapshots')
    BEGIN
        CREATE TABLE [PortfolioSnapshots] (
            [UserId]       NVARCHAR (450) NOT NULL CONSTRAINT [PK_PortfolioSnapshots] PRIMARY KEY,
            [SnapshotJson] NVARCHAR (MAX) CONSTRAINT [DF_PortSnap_Json] DEFAULT N'[]' NOT NULL,
            [UpdatedAt]    DATETIME2      DEFAULT GETUTCDATE() NOT NULL,
            [ItemCount]    INT            DEFAULT 0 NOT NULL
        );
        PRINT 'Created PortfolioSnapshots';
    END
ELSE
    PRINT 'PortfolioSnapshots already exists — skipped';


GO
-- Per-user watchlist quotes snapshot
IF NOT EXISTS (SELECT 1
               FROM   sys.tables
               WHERE  name = 'WatchlistSnapshots')
    BEGIN
        CREATE TABLE [WatchlistSnapshots] (
            [UserId]       NVARCHAR (450) NOT NULL CONSTRAINT [PK_WatchlistSnapshots] PRIMARY KEY,
            [SnapshotJson] NVARCHAR (MAX) CONSTRAINT [DF_WlSnap_Json] DEFAULT N'[]' NOT NULL,
            [UpdatedAt]    DATETIME2      DEFAULT GETUTCDATE() NOT NULL,
            [ItemCount]    INT            DEFAULT 0 NOT NULL
        );
        PRINT 'Created WatchlistSnapshots';
    END
ELSE
    PRINT 'WatchlistSnapshots already exists — skipped';