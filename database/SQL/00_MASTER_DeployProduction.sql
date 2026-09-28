-- ============================================================
-- SCRIPTS/00_MASTER_DeployProduction.sql
-- Master deployment script for Portfolio Manager Database.
--
-- PURPOSE:
--   Single-file deploy that creates (or upgrades) the full
--   PortfolioManagerDb schema and applies all seed data.
--   Safe to run on a NEW or EXISTING database — every step
--   is idempotent (guarded by IF NOT EXISTS / MERGE).
--
-- EXECUTION ORDER:
--   Step 1  Backup check / pre-flight                   (this file)
--   Step 2  01_CreateDatabase.sql                       (database creation)
--   Step 3  02_CreateTables.sql                         (all tables + columns + indexes)
--   Step 4  07_AdhocAnalysisSession.sql                 (AdhocAnalysisSessions + stored proc)
--   Step 5  08_CreateDailySignals.sql                   (DailySignals table)
--   Step 6  03_SeedData.sql                             (demo / default data + migrations history)
--   Step 7  09_SetStrategicIncomeRole.sql               (Strategic-Income role assignment)
--   Step 8  11_AddIdentityAndAuth.sql                   (ASP.NET Core Identity + RefreshTokens)
--   Step 9  14_AddFibonacciToDailySignals.sql           (Fibonacci snapshot columns on DailySignals)
--
-- SCRIPTS NOT RUN IN THIS MASTER:
--   04_SeedNotificationRecipients.sql  -- contains placeholder emails; run manually
--   05_DeleteAllData.sql               -- destructive; run only when resetting dev data
--   06_DropAll.sql                     -- destructive; run only to rebuild from scratch
--
-- HOW TO RUN:
--   Option A (SSMS): Open this file, connect to the target SQL Server, Execute (F5).
--   Option B (sqlcmd):
--     sqlcmd -S <server> -E -i "00_MASTER_DeployProduction.sql" -o deploy_log.txt
--
-- PRE-DEPLOYMENT CHECKLIST:
--   [ ] Backend API is stopped (no active EF connections)
--   [ ] You have a recent database backup (see Step 0 below)
--   [ ] SQL Server login has db_owner or sysadmin on PortfolioManagerDb
-- ============================================================

SET NOCOUNT ON;
PRINT '============================================================';
PRINT ' Portfolio Manager -- Production Deploy Script';
PRINT ' Started: ' + CONVERT(NVARCHAR, GETDATE(), 120);
PRINT '============================================================';
PRINT '';
GO

-- ============================================================
-- STEP 0: PRE-FLIGHT — Backup advisory
-- ============================================================
-- This step cannot create a backup automatically because the
-- backup path is environment-specific.  Use SQL Server Management
-- Studio or run the BACKUP command below, substituting your path.
--
-- EXAMPLE (uncomment and edit path before running):
--
-- BACKUP DATABASE PortfolioManagerDb
--   TO DISK = N'C:\Backups\PortfolioManagerDb_PreDeploy_' +
--              REPLACE(REPLACE(CONVERT(NVARCHAR,GETDATE(),120),' ','_'),':','-') +
--              '.bak'
--   WITH FORMAT, COMPRESSION, STATS = 10;
-- GO

PRINT 'STEP 0: Pre-flight check';

-- Verify SQL Server version (minimum SQL Server 2016 for JSON support)
DECLARE @sqlVersion NVARCHAR(200) = CAST(SERVERPROPERTY('ProductVersion') AS NVARCHAR);
PRINT '  SQL Server version: ' + @sqlVersion;

-- Warn if running against a server that already has the DB (upgrade scenario)
IF EXISTS (SELECT 1
FROM sys.databases
WHERE name = N'PortfolioManagerDb')
    PRINT '  [INFO] PortfolioManagerDb EXISTS -- this is an upgrade deployment.';
ELSE
    PRINT '  [INFO] PortfolioManagerDb not found -- this is a fresh installation.';

PRINT '  Pre-flight complete.';
PRINT '';
GO

-- ============================================================
-- STEP 1: Create database (01_CreateDatabase.sql)
-- ============================================================
PRINT 'STEP 1: Create database';
GO

USE master;
GO

IF NOT EXISTS (
    SELECT name
FROM sys.databases
WHERE name = N'PortfolioManagerDb'
)
BEGIN
    CREATE DATABASE PortfolioManagerDb
        COLLATE SQL_Latin1_General_CP1_CI_AS;
    PRINT '  Database PortfolioManagerDb created.';
END
ELSE
BEGIN
    PRINT '  Database PortfolioManagerDb already exists -- skipping create.';
END
GO

-- ============================================================
-- STEP 2: Create / upgrade all tables (02_CreateTables.sql)
-- ============================================================
PRINT '';
PRINT 'STEP 2: Create / upgrade tables';
GO

USE PortfolioManagerDb;
GO

-- ── TABLE: PortfolioItems ────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND type = N'U'
)
BEGIN
    CREATE TABLE [dbo].[PortfolioItems]
    (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [Symbol] NVARCHAR(20) NOT NULL,
        [CompanyName] NVARCHAR(200) NOT NULL,
        [Shares] DECIMAL(18,6) NOT NULL,
        [AverageCostBasis] DECIMAL(18,4) NOT NULL,
        [AddedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [Sector] NVARCHAR(100) NOT NULL DEFAULT '',
        [Industry] NVARCHAR(100) NOT NULL DEFAULT '',
        [IsManual] BIT NOT NULL DEFAULT 0,
        [ManualMarketValue] DECIMAL(18,4) NULL,
        [SectorIsOverridden] BIT NOT NULL DEFAULT 0,
        CONSTRAINT [PK_PortfolioItems] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT '  Table PortfolioItems created.';
END
ELSE
BEGIN
    PRINT '  Table PortfolioItems exists -- checking columns...';

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND name = N'Sector')
        BEGIN
        ALTER TABLE [dbo].[PortfolioItems] ADD [Sector] NVARCHAR(100) NOT NULL DEFAULT '';
        PRINT '  + Sector added.';
    END

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND name = N'Industry')
        BEGIN
        ALTER TABLE [dbo].[PortfolioItems] ADD [Industry] NVARCHAR(100) NOT NULL DEFAULT '';
        PRINT '  + Industry added.';
    END

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND name = N'IsManual')
        BEGIN
        ALTER TABLE [dbo].[PortfolioItems] ADD [IsManual] BIT NOT NULL DEFAULT 0;
        PRINT '  + IsManual added.';
    END

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND name = N'ManualMarketValue')
        BEGIN
        ALTER TABLE [dbo].[PortfolioItems] ADD [ManualMarketValue] DECIMAL(18,4) NULL;
        PRINT '  + ManualMarketValue added.';
    END

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND name = N'SectorIsOverridden')
        BEGIN
        ALTER TABLE [dbo].[PortfolioItems] ADD [SectorIsOverridden] BIT NOT NULL DEFAULT 0;
        PRINT '  + SectorIsOverridden added.';
    END

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND name = N'TransactionType')
        BEGIN
        ALTER TABLE [dbo].[PortfolioItems] ADD [TransactionType] NVARCHAR(10) NULL;
        PRINT '  + TransactionType added.';
    END

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND name = N'AccountType')
        BEGIN
        ALTER TABLE [dbo].[PortfolioItems] ADD [AccountType] NVARCHAR(30) NULL;
        PRINT '  + AccountType added.';
    END

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND name = N'OpenDate')
        BEGIN
        ALTER TABLE [dbo].[PortfolioItems] ADD [OpenDate] DATETIME2 NULL;
        PRINT '  + OpenDate added.';
    END

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND name = N'CloseDate')
        BEGIN
        ALTER TABLE [dbo].[PortfolioItems] ADD [CloseDate] DATETIME2 NULL;
        PRINT '  + CloseDate added.';
    END

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND name = N'ClosingPrice')
        BEGIN
        ALTER TABLE [dbo].[PortfolioItems] ADD [ClosingPrice] DECIMAL(18,4) NULL;
        PRINT '  + ClosingPrice added.';
    END

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND name = N'HoldingRole')
        BEGIN
        ALTER TABLE [dbo].[PortfolioItems] ADD [HoldingRole] NVARCHAR(20) NULL;
        PRINT '  + HoldingRole added.';
    END

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND name = N'Notes')
        BEGIN
        ALTER TABLE [dbo].[PortfolioItems] ADD [Notes] NVARCHAR(MAX) NULL;
        PRINT '  + Notes added.';
    END

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND name = N'DecisionSource')
        BEGIN
        ALTER TABLE [dbo].[PortfolioItems] ADD [DecisionSource] NVARCHAR(50) NULL;
        PRINT '  + DecisionSource added.';
    END
END
GO

-- Ensure Symbol index is non-unique (older installs may have a unique constraint)
IF EXISTS (
    SELECT 1
FROM sys.indexes
WHERE name = N'IX_PortfolioItems_Symbol'
    AND object_id = OBJECT_ID(N'[dbo].[PortfolioItems]')
    AND is_unique = 1
)
BEGIN
    DROP INDEX [IX_PortfolioItems_Symbol] ON [dbo].[PortfolioItems];
    PRINT '  Dropped unique IX_PortfolioItems_Symbol (will recreate non-unique).';
END
IF NOT EXISTS (
    SELECT 1
FROM sys.indexes
WHERE name = N'IX_PortfolioItems_Symbol'
    AND object_id = OBJECT_ID(N'[dbo].[PortfolioItems]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PortfolioItems_Symbol] ON [dbo].[PortfolioItems] ([Symbol] ASC);
    PRINT '  Index IX_PortfolioItems_Symbol created (non-unique).';
END
GO

-- ── TABLE: WatchlistItems ────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[WatchlistItems]') AND type = N'U'
)
BEGIN
    CREATE TABLE [dbo].[WatchlistItems]
    (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [Symbol] NVARCHAR(20) NOT NULL,
        [Notes] NVARCHAR(500) NOT NULL DEFAULT '',
        [AddedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [Role] NVARCHAR(20) NOT NULL DEFAULT 'Strategic',
        CONSTRAINT [PK_WatchlistItems] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT '  Table WatchlistItems created.';
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[WatchlistItems]') AND name = N'Role')
        BEGIN
        ALTER TABLE [dbo].[WatchlistItems] ADD [Role] NVARCHAR(20) NOT NULL DEFAULT 'Strategic';
        PRINT '  + WatchlistItems.Role added.';
    END

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[WatchlistItems]') AND name = N'IsFavorite')
        BEGIN
        ALTER TABLE [dbo].[WatchlistItems] ADD [IsFavorite] BIT NOT NULL DEFAULT 0;
        PRINT '  + WatchlistItems.IsFavorite added.';
    END
END
GO
IF NOT EXISTS (
    SELECT 1
FROM sys.indexes
WHERE name = N'IX_WatchlistItems_Symbol' AND object_id = OBJECT_ID(N'[dbo].[WatchlistItems]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_WatchlistItems_Symbol] ON [dbo].[WatchlistItems] ([Symbol] ASC);
    PRINT '  Index IX_WatchlistItems_Symbol created.';
END
GO

-- ── TABLE: CashItems ─────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[CashItems]') AND type = N'U'
)
BEGIN
    CREATE TABLE [dbo].[CashItems]
    (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [Description] NVARCHAR(200) NOT NULL DEFAULT 'CASH',
        [Amount] DECIMAL(18,4) NOT NULL,
        [AddedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_CashItems] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT '  Table CashItems created.';
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[CashItems]') AND name = N'AccountType')
        BEGIN
        ALTER TABLE [dbo].[CashItems] ADD [AccountType] NVARCHAR(30) NULL;
        PRINT '  + CashItems.AccountType added.';
    END
    PRINT '  Table CashItems exists -- checked.';
END
GO

-- ── TABLE: OptionItems ───────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[OptionItems]') AND type = N'U'
)
BEGIN
    CREATE TABLE [dbo].[OptionItems]
    (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [UnderlyingTicker] NVARCHAR(20) NOT NULL,
        [PositionType] NVARCHAR(10) NOT NULL,
        [ExpirationDate] DATETIME2 NOT NULL,
        [Strike] DECIMAL(18,4) NOT NULL,
        [Premium] DECIMAL(18,4) NOT NULL,
        [NumberOfContracts] INT NOT NULL,
        [MarketPrice] DECIMAL(18,4) NOT NULL,
        [AddedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_OptionItems] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT '  Table OptionItems created.';
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[OptionItems]') AND name = N'TransactionType')
        BEGIN
        ALTER TABLE [dbo].[OptionItems] ADD [TransactionType] NVARCHAR(10) NULL;
        PRINT '  + OptionItems.TransactionType added.';
    END
    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[OptionItems]') AND name = N'AccountType')
        BEGIN
        ALTER TABLE [dbo].[OptionItems] ADD [AccountType] NVARCHAR(30) NULL;
        PRINT '  + OptionItems.AccountType added.';
    END
    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[OptionItems]') AND name = N'OpenDate')
        BEGIN
        ALTER TABLE [dbo].[OptionItems] ADD [OpenDate] DATETIME2 NULL;
        PRINT '  + OptionItems.OpenDate added.';
    END
    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[OptionItems]') AND name = N'CloseDate')
        BEGIN
        ALTER TABLE [dbo].[OptionItems] ADD [CloseDate] DATETIME2 NULL;
        PRINT '  + OptionItems.CloseDate added.';
    END
    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[OptionItems]') AND name = N'ClosingPrice')
        BEGIN
        ALTER TABLE [dbo].[OptionItems] ADD [ClosingPrice] DECIMAL(18,4) NULL;
        PRINT '  + OptionItems.ClosingPrice added.';
    END
    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[OptionItems]') AND name = N'Notes')
        BEGIN
        ALTER TABLE [dbo].[OptionItems] ADD [Notes] NVARCHAR(MAX) NULL;
        PRINT '  + OptionItems.Notes added.';
    END
    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[OptionItems]') AND name = N'DecisionSource')
        BEGIN
        ALTER TABLE [dbo].[OptionItems] ADD [DecisionSource] NVARCHAR(50) NULL;
        PRINT '  + OptionItems.DecisionSource added.';
    END
    PRINT '  Table OptionItems exists -- checked.';
END
GO

-- ── TABLE: NotificationRecipients ───────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[NotificationRecipients]') AND type = N'U'
)
BEGIN
    CREATE TABLE [dbo].[NotificationRecipients]
    (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [Email] NVARCHAR(254) NOT NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [AddedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_NotificationRecipients] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    CREATE UNIQUE NONCLUSTERED INDEX [IX_NotificationRecipients_Email]
        ON [dbo].[NotificationRecipients] ([Email] ASC);
    PRINT '  Table NotificationRecipients created.';
END
ELSE
    PRINT '  Table NotificationRecipients exists -- skipping.';
GO

-- ── TABLE: AllocationRiskTargets ─────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[AllocationRiskTargets]') AND type = N'U'
)
BEGIN
    CREATE TABLE [dbo].[AllocationRiskTargets]
    (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [Role] NVARCHAR(50) NOT NULL,
        [TargetPct] DECIMAL(5,2) NOT NULL,
        [DisplayOrder] INT NOT NULL DEFAULT 0,
        CONSTRAINT [PK_AllocationRiskTargets] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT '  Table AllocationRiskTargets created.';
END
ELSE
    PRINT '  Table AllocationRiskTargets exists -- skipping.';
GO

-- ── TABLE: AllocationSectorTargets ──────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[AllocationSectorTargets]') AND type = N'U'
)
BEGIN
    CREATE TABLE [dbo].[AllocationSectorTargets]
    (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [Sector] NVARCHAR(100) NOT NULL,
        [TargetPct] DECIMAL(5,2) NOT NULL,
        [DisplayOrder] INT NOT NULL DEFAULT 0,
        CONSTRAINT [PK_AllocationSectorTargets] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT '  Table AllocationSectorTargets created.';
END
ELSE
    PRINT '  Table AllocationSectorTargets exists -- skipping.';
GO

-- ── TABLE: SinglePositionLimits ──────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[SinglePositionLimits]') AND type = N'U'
)
BEGIN
    CREATE TABLE [dbo].[SinglePositionLimits]
    (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [Role] NVARCHAR(50) NOT NULL,
        [TargetPct] DECIMAL(5,2) NOT NULL,
        [DisplayOrder] INT NOT NULL DEFAULT 0,
        CONSTRAINT [PK_SinglePositionLimits] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT '  Table SinglePositionLimits created.';
END
ELSE
    PRINT '  Table SinglePositionLimits exists -- skipping.';
GO

-- ── TABLE: ValueScreenerResults ──────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[ValueScreenerResults]') AND type = N'U'
)
BEGIN
    CREATE TABLE [dbo].[ValueScreenerResults]
    (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [Symbol] NVARCHAR(20) NOT NULL,
        [ScreenedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [ResultJson] NVARCHAR(MAX) NOT NULL,
        [RunGroup] NVARCHAR(30) NULL,
        CONSTRAINT [PK_ValueScreenerResults] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    CREATE NONCLUSTERED INDEX [IX_ValueScreenerResults_Symbol]
        ON [dbo].[ValueScreenerResults] ([Symbol] ASC, [ScreenedAt] DESC);
    PRINT '  Table ValueScreenerResults created.';
END
ELSE
    PRINT '  Table ValueScreenerResults exists -- skipping.';
GO

-- ── EF Migrations history table (required by EF Core) ────────────────────────
IF NOT EXISTS (
    SELECT 1
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[__EFMigrationsHistory]') AND type = N'U'
)
BEGIN
    CREATE TABLE [dbo].[__EFMigrationsHistory]
    (
        [MigrationId] NVARCHAR(150) NOT NULL,
        [ProductVersion] NVARCHAR(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
    PRINT '  Table __EFMigrationsHistory created.';
END
ELSE
    PRINT '  Table __EFMigrationsHistory exists -- skipping.';
GO

-- ============================================================
-- STEP 3: AdhocAnalysisSessions + stored procedure
--         (07_AdhocAnalysisSession.sql)
-- ============================================================
PRINT '';
PRINT 'STEP 3: AdhocAnalysisSessions';
GO

IF NOT EXISTS (
    SELECT 1
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[AdhocAnalysisSessions]') AND type = N'U'
)
BEGIN
    CREATE TABLE [dbo].[AdhocAnalysisSessions]
    (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [SessionKey] NVARCHAR(100) NOT NULL DEFAULT 'default',
        [Symbols] NVARCHAR(MAX) NOT NULL DEFAULT '[]',
        [ResultsJson] NVARCHAR(MAX) NULL,
        [OversoldThreshold] DECIMAL(5,2) NOT NULL DEFAULT 30.00,
        [OverboughtThreshold] DECIMAL(5,2) NOT NULL DEFAULT 75.00,
        [LogicMode] NVARCHAR(20) NOT NULL DEFAULT 'Legacy',
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_AdhocAnalysisSessions] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    CREATE NONCLUSTERED INDEX [IX_AdhocAnalysisSessions_SessionKey_UpdatedAt]
        ON [dbo].[AdhocAnalysisSessions] ([SessionKey] ASC, [UpdatedAt] DESC);
    PRINT '  Table AdhocAnalysisSessions created.';
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[AdhocAnalysisSessions]') AND name = N'OversoldThreshold')
        BEGIN
        ALTER TABLE [dbo].[AdhocAnalysisSessions] ADD [OversoldThreshold] DECIMAL(5,2) NOT NULL DEFAULT 30.00;
        PRINT '  + OversoldThreshold added.';
    END
    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[AdhocAnalysisSessions]') AND name = N'OverboughtThreshold')
        BEGIN
        ALTER TABLE [dbo].[AdhocAnalysisSessions] ADD [OverboughtThreshold] DECIMAL(5,2) NOT NULL DEFAULT 75.00;
        PRINT '  + OverboughtThreshold added.';
    END
    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[AdhocAnalysisSessions]') AND name = N'LogicMode')
        BEGIN
        ALTER TABLE [dbo].[AdhocAnalysisSessions] ADD [LogicMode] NVARCHAR(20) NOT NULL DEFAULT 'Legacy';
        PRINT '  + LogicMode added.';
    END
    PRINT '  Table AdhocAnalysisSessions exists -- checked.';
END
GO

IF OBJECT_ID(N'[dbo].[usp_SaveAdhocSession]', N'P') IS NOT NULL
    DROP PROCEDURE [dbo].[usp_SaveAdhocSession];
GO

CREATE PROCEDURE [dbo].[usp_SaveAdhocSession]
    @SessionKey          NVARCHAR(100) = 'default',
    @Symbols             NVARCHAR(MAX),
    @ResultsJson         NVARCHAR(MAX) = NULL,
    @OversoldThreshold   DECIMAL(5,2)  = 30.00,
    @OverboughtThreshold DECIMAL(5,2)  = 75.00,
    @LogicMode           NVARCHAR(20)  = 'Legacy'
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1
    FROM [dbo].[AdhocAnalysisSessions]
    WHERE [SessionKey] = @SessionKey)
    BEGIN
        UPDATE [dbo].[AdhocAnalysisSessions]
        SET    [Symbols]             = @Symbols,
               [ResultsJson]        = @ResultsJson,
               [OversoldThreshold]  = @OversoldThreshold,
               [OverboughtThreshold]= @OverboughtThreshold,
               [LogicMode]          = @LogicMode,
               [UpdatedAt]          = GETUTCDATE()
        WHERE  [SessionKey] = @SessionKey;
    END
    ELSE
    BEGIN
        INSERT INTO [dbo].[AdhocAnalysisSessions]
            ([SessionKey],[Symbols],[ResultsJson],[OversoldThreshold],[OverboughtThreshold],[LogicMode],[CreatedAt],[UpdatedAt])
        VALUES
            (@SessionKey, @Symbols, @ResultsJson, @OversoldThreshold, @OverboughtThreshold, @LogicMode, GETUTCDATE(), GETUTCDATE());
    END
END
GO
PRINT '  usp_SaveAdhocSession created/replaced.';
GO

-- ============================================================
-- STEP 4: DailySignals table (08_CreateDailySignals.sql)
-- ============================================================
PRINT '';
PRINT 'STEP 4: DailySignals';
GO

IF NOT EXISTS (
    SELECT 1
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[DailySignals]') AND type = N'U'
)
BEGIN
    CREATE TABLE [dbo].[DailySignals]
    (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [Symbol] NVARCHAR(20) NOT NULL,
        [CompanyName] NVARCHAR(200) NOT NULL DEFAULT '',
        [ScanType] NVARCHAR(20) NOT NULL,
        [SignalType] NVARCHAR(30) NOT NULL,
        [Rsi] DECIMAL(7,4) NOT NULL,
        [Price] DECIMAL(18,4) NOT NULL,
        [TriggerDetails] NVARCHAR(1000) NOT NULL DEFAULT '',
        [SignalDate] NVARCHAR(10) NOT NULL,
        [RecordedAt] DATETIME2 NOT NULL,
        [RuleVersion] NVARCHAR(20) NOT NULL DEFAULT 'Legacy',
        [SignalState] NVARCHAR(30) NOT NULL DEFAULT 'Active',
        [Sector] NVARCHAR(100) NOT NULL DEFAULT '',
        [ReversalProbability] NVARCHAR(20) NOT NULL DEFAULT '',
        [VolumeSignal] NVARCHAR(30) NOT NULL DEFAULT '',
        [Notes] NVARCHAR(MAX) NULL,
        [UpdatedAt] DATETIME2 NULL,
        CONSTRAINT [PK_DailySignals] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    CREATE NONCLUSTERED INDEX [IX_DailySignals_Symbol]          ON [dbo].[DailySignals] ([Symbol] ASC);
    CREATE NONCLUSTERED INDEX [IX_DailySignals_SignalDate]       ON [dbo].[DailySignals] ([SignalDate] ASC);
    CREATE NONCLUSTERED INDEX [IX_DailySignals_Symbol_SignalDate] ON [dbo].[DailySignals] ([Symbol] ASC, [SignalDate] ASC);
    PRINT '  Table DailySignals created with indexes.';
END
ELSE
    PRINT '  Table DailySignals exists -- skipping.';
GO

-- ============================================================
-- STEP 5: Seed data (03_SeedData.sql)
-- ============================================================
PRINT '';
PRINT 'STEP 5: Seed data';
GO

-- ── EF Core Migrations History ────────────────────────────────────────────────
MERGE [dbo].[__EFMigrationsHistory] AS target
USING (
    VALUES
    ('20260611201226_InitialCreate', '8.0.0'),
    ('20260611235305_AddWatchlistAndSector', '8.0.0'),
    ('20260612030112_AddManualPosition', '8.0.0'),
    ('20260615120000_AddSectorOverride', '8.0.0'),
    ('20260618010551_AddCashOptionAndAdhocTables', '8.0.0'),
    ('20260619000000_AddTransactionFields', '8.0.0'),
    ('20260619000002_RemovePortfolioSymbolUniqueConstraint', '8.0.0'),
    ('20260622183326_AddRoleAndHoldingRole', '8.0.0')
) AS source ([MigrationId], [ProductVersion])
ON target.[MigrationId] = source.[MigrationId]
WHEN NOT MATCHED THEN
    INSERT ([MigrationId], [ProductVersion])
    VALUES (source.[MigrationId], source.[ProductVersion]);
PRINT '  EF Migrations history stamped.';
GO

-- ── Allocation & Risk defaults (only if tables are empty) ────────────────────
IF NOT EXISTS (SELECT 1
FROM [dbo].[AllocationRiskTargets])
BEGIN
    INSERT INTO [dbo].[AllocationRiskTargets]
        ([Role],[TargetPct],[DisplayOrder])
    VALUES
        ('Core', 40, 1),
        ('Strategic', 15, 2),
        ('Strategic-Income', 5, 3),
        ('Swing', 20, 4),
        ('Speculative', 10, 5),
        ('Options', 5, 6),
        ('Cash', 5, 7);
    PRINT '  AllocationRiskTargets seeded.';
END
GO

IF NOT EXISTS (SELECT 1
FROM [dbo].[AllocationSectorTargets])
BEGIN
    INSERT INTO [dbo].[AllocationSectorTargets]
        ([Sector],[TargetPct],[DisplayOrder])
    VALUES
        ('Energy', 20, 1),
        ('Industrials', 20, 2),
        ('Financial Services', 15, 3),
        ('Communication Services', 5, 4),
        ('Utilities', 10, 5),
        ('Technology', 10, 6),
        ('Healthcare', 5, 7),
        ('Consumer Defensive', 10, 8),
        ('Materials', 3, 9),
        ('Cash', 2, 10);
    PRINT '  AllocationSectorTargets seeded.';
END
GO

IF NOT EXISTS (SELECT 1
FROM [dbo].[SinglePositionLimits])
BEGIN
    INSERT INTO [dbo].[SinglePositionLimits]
        ([Role],[TargetPct],[DisplayOrder])
    VALUES
        ('Core', 5, 1),
        ('Strategic', 5, 2),
        ('Strategic-Income', 5, 3),
        ('Swing', 2, 4),
        ('Speculative', 2, 5),
        ('Options', 1, 6);
    PRINT '  SinglePositionLimits seeded.';
END
GO

-- ============================================================
-- STEP 6: Strategic-Income role assignment
--         (09_SetStrategicIncomeRole.sql)
-- ============================================================
PRINT '';
PRINT 'STEP 6: Strategic-Income role assignment';
GO

UPDATE [dbo].[PortfolioItems]
SET    [HoldingRole] = 'Strategic-Income'
WHERE  [Symbol] IN ('BANK.TO', 'SIXY.TO', 'T.TO', 'HMAX.TO')
    AND ([TransactionType] IS NULL OR [TransactionType] = 'OPEN')
    AND ([HoldingRole] IS NULL OR [HoldingRole] != 'Strategic-Income');

PRINT '  Strategic-Income role applied to BANK.TO, SIXY.TO, T.TO, HMAX.TO (where applicable).';
GO

-- ============================================================
-- STEP 7: Post-deploy validation
-- ============================================================
PRINT '';
PRINT 'STEP 7: Post-deploy validation';
GO

SELECT
    t.name                          AS [Table],
    p.rows                          AS [RowCount]
FROM sys.tables t
    JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0,1)
WHERE  t.name IN (
    'PortfolioItems', 'WatchlistItems', 'CashItems', 'OptionItems',
    'NotificationRecipients', 'AllocationRiskTargets', 'AllocationSectorTargets',
    'SinglePositionLimits', 'ValueScreenerResults', 'DailySignals',
    'AdhocAnalysisSessions', '__EFMigrationsHistory'
)
ORDER BY t.name;
GO

PRINT '';
PRINT '============================================================';
PRINT ' Portfolio Manager -- Deploy COMPLETE';
PRINT ' Finished: ' + CONVERT(NVARCHAR, GETDATE(), 120);
PRINT '============================================================';
PRINT '';
PRINT 'NEXT STEPS:';
PRINT '  1. If this is a fresh install, run 04_SeedNotificationRecipients.sql';
PRINT '     after adding real email addresses.';
PRINT '  2. Ensure Jwt:Secret is set in user-secrets or environment variables.';
PRINT '  3. Start the .NET backend: dotnet run --launch-profile http';
PRINT '  4. Open http://localhost:4200 — first visit shows the admin Setup screen.';
PRINT '  5. Create the administrator account, then use Config > Users to add more users.';
PRINT '';
GO

-- ════════════════════════════════════════════════════════════════════════════
-- STEP 8: Identity + Auth Tables
-- ════════════════════════════════════════════════════════════════════════════
PRINT '-- Step 8: ASP.NET Core Identity + RefreshTokens --';

IF NOT EXISTS (SELECT 1
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME = 'AspNetRoles')
BEGIN
    CREATE TABLE [dbo].[AspNetRoles]
    (
        [Id] NVARCHAR(450) NOT NULL,
        [Name] NVARCHAR(256) NULL,
        [NormalizedName] NVARCHAR(256) NULL,
        [ConcurrencyStamp] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [RoleNameIndex] ON [dbo].[AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
    PRINT '  Created AspNetRoles';
END
GO
IF NOT EXISTS (SELECT 1
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME = 'AspNetUsers')
BEGIN
    CREATE TABLE [dbo].[AspNetUsers]
    (
        [Id] NVARCHAR(450) NOT NULL,
        [DisplayName] NVARCHAR(MAX) NOT NULL DEFAULT '',
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [UserName] NVARCHAR(256) NULL,
        [NormalizedUserName] NVARCHAR(256) NULL,
        [Email] NVARCHAR(256) NULL,
        [NormalizedEmail] NVARCHAR(256) NULL,
        [EmailConfirmed] BIT NOT NULL DEFAULT 0,
        [PasswordHash] NVARCHAR(MAX) NULL,
        [SecurityStamp] NVARCHAR(MAX) NULL,
        [ConcurrencyStamp] NVARCHAR(MAX) NULL,
        [PhoneNumber] NVARCHAR(MAX) NULL,
        [PhoneNumberConfirmed] BIT NOT NULL DEFAULT 0,
        [TwoFactorEnabled] BIT NOT NULL DEFAULT 0,
        [LockoutEnd] DATETIMEOFFSET NULL,
        [LockoutEnabled] BIT NOT NULL DEFAULT 1,
        [AccessFailedCount] INT NOT NULL DEFAULT 0,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
    CREATE INDEX [EmailIndex] ON [dbo].[AspNetUsers] ([NormalizedEmail]);
    CREATE UNIQUE INDEX [UserNameIndex] ON [dbo].[AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
    PRINT '  Created AspNetUsers';
END
GO
IF NOT EXISTS (SELECT 1
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME = 'AspNetRoleClaims') BEGIN
    CREATE TABLE [dbo].[AspNetRoleClaims]
    (
        [Id] INT NOT NULL IDENTITY(1,1),
        [RoleId] NVARCHAR(450) NOT NULL,
        [ClaimType] NVARCHAR(MAX) NULL,
        [ClaimValue] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ARC_Roles] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [dbo].[AspNetRoleClaims] ([RoleId]);
END
GO
IF NOT EXISTS (SELECT 1
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME = 'AspNetUserClaims') BEGIN
    CREATE TABLE [dbo].[AspNetUserClaims]
    (
        [Id] INT NOT NULL IDENTITY(1,1),
        [UserId] NVARCHAR(450) NOT NULL,
        [ClaimType] NVARCHAR(MAX) NULL,
        [ClaimValue] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AUC_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [dbo].[AspNetUserClaims] ([UserId]);
END
GO
IF NOT EXISTS (SELECT 1
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME = 'AspNetUserLogins') BEGIN
    CREATE TABLE [dbo].[AspNetUserLogins]
    (
        [LoginProvider] NVARCHAR(450) NOT NULL,
        [ProviderKey] NVARCHAR(450) NOT NULL,
        [ProviderDisplayName] NVARCHAR(MAX) NULL,
        [UserId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider],[ProviderKey]),
        CONSTRAINT [FK_AUL_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [dbo].[AspNetUserLogins] ([UserId]);
END
GO
IF NOT EXISTS (SELECT 1
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME = 'AspNetUserRoles') BEGIN
    CREATE TABLE [dbo].[AspNetUserRoles]
    (
        [UserId] NVARCHAR(450) NOT NULL,
        [RoleId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId],[RoleId]),
        CONSTRAINT [FK_AUR_Roles] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AUR_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [dbo].[AspNetUserRoles] ([RoleId]);
END
GO
IF NOT EXISTS (SELECT 1
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME = 'AspNetUserTokens') BEGIN
    CREATE TABLE [dbo].[AspNetUserTokens]
    (
        [UserId] NVARCHAR(450) NOT NULL,
        [LoginProvider] NVARCHAR(450) NOT NULL,
        [Name] NVARCHAR(450) NOT NULL,
        [Value] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId],[LoginProvider],[Name]),
        CONSTRAINT [FK_AUT_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END
GO
IF NOT EXISTS (SELECT 1
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME = 'RefreshTokens')
BEGIN
    CREATE TABLE [dbo].[RefreshTokens]
    (
        [Id] INT NOT NULL IDENTITY(1,1),
        [Token] NVARCHAR(64) NOT NULL,
        [UserId] NVARCHAR(450) NOT NULL,
        [ExpiresAt] DATETIME2 NOT NULL,
        [IsRevoked] BIT NOT NULL DEFAULT 0,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RT_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_RefreshTokens_Token]  ON [dbo].[RefreshTokens] ([Token]);
    CREATE INDEX        [IX_RefreshTokens_UserId] ON [dbo].[RefreshTokens] ([UserId]);
    PRINT '  Created RefreshTokens';
END
GO

-- Step 8 validation
IF NOT EXISTS (SELECT 1
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME = 'AspNetUsers')
    RAISERROR('STEP 8 FAILED: AspNetUsers table was not created.', 16, 1);
ELSE
    PRINT '  Step 8 OK: Identity tables verified.';
GO

-- ════════════════════════════════════════════════════════════════════════════
-- STEP 9: Fibonacci snapshot columns on DailySignals (14_AddFibonacciToDailySignals.sql)
-- ════════════════════════════════════════════════════════════════════════════
PRINT '-- Step 9: Fibonacci Retracement V1 snapshot columns --';

IF NOT EXISTS (
    SELECT 1
FROM sys.columns
WHERE object_id = OBJECT_ID(N'[dbo].[DailySignals]') AND name = N'Fib61_8AtSignal'
)
BEGIN
    ALTER TABLE [dbo].[DailySignals] ADD [Fib61_8AtSignal] DECIMAL(18,4) NULL;
    PRINT '  Added Fib61_8AtSignal.';
END
IF NOT EXISTS (
    SELECT 1
FROM sys.columns
WHERE object_id = OBJECT_ID(N'[dbo].[DailySignals]') AND name = N'FibZoneAtSignal'
)
BEGIN
    ALTER TABLE [dbo].[DailySignals] ADD [FibZoneAtSignal] NVARCHAR(30) NULL;
    PRINT '  Added FibZoneAtSignal.';
END
IF NOT EXISTS (
    SELECT 1
FROM sys.columns
WHERE object_id = OBJECT_ID(N'[dbo].[DailySignals]') AND name = N'FibStatusAtSignal'
)
BEGIN
    ALTER TABLE [dbo].[DailySignals] ADD [FibStatusAtSignal] NVARCHAR(30) NULL;
    PRINT '  Added FibStatusAtSignal.';
END
PRINT '  Step 9 OK: Fibonacci columns verified.';
GO

