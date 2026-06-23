-- ============================================================
-- SCRIPTS/02_CreateTables.sql
-- Creates all application tables.  Safe to re-run: every CREATE
-- is guarded by an IF NOT EXISTS check, and every ALTER adds a
-- column only when it doesn't already exist.
-- ============================================================

USE PortfolioManagerDb;
GO

-- ────────────────────────────────────────────────────────────────────────────
-- TABLE: PortfolioItems
-- Core holdings table.  Columns were added across four migrations:
--   InitialCreate          : Id, Symbol, CompanyName, Shares, AverageCostBasis, AddedAt
--   AddWatchlistAndSector  : Sector, Industry
--   AddManualPosition      : IsManual, ManualMarketValue
--   AddSectorOverride      : SectorIsOverridden
-- ────────────────────────────────────────────────────────────────────────────
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
    PRINT 'Table PortfolioItems created.';
END
ELSE
BEGIN
    PRINT 'Table PortfolioItems already exists – checking for missing columns...';

    -- AddWatchlistAndSector columns
    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND name = N'Sector')
    BEGIN
        ALTER TABLE [dbo].[PortfolioItems] ADD [Sector] NVARCHAR(100) NOT NULL DEFAULT '';
        PRINT '  + Column Sector added.';
    END

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND name = N'Industry')
    BEGIN
        ALTER TABLE [dbo].[PortfolioItems] ADD [Industry] NVARCHAR(100) NOT NULL DEFAULT '';
        PRINT '  + Column Industry added.';
    END

    -- AddManualPosition columns
    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND name = N'IsManual')
    BEGIN
        ALTER TABLE [dbo].[PortfolioItems] ADD [IsManual] BIT NOT NULL DEFAULT 0;
        PRINT '  + Column IsManual added.';
    END

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND name = N'ManualMarketValue')
    BEGIN
        ALTER TABLE [dbo].[PortfolioItems] ADD [ManualMarketValue] DECIMAL(18,4) NULL;
        PRINT '  + Column ManualMarketValue added.';
    END

    -- AddSectorOverride column
    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND name = N'SectorIsOverridden')
    BEGIN
        ALTER TABLE [dbo].[PortfolioItems] ADD [SectorIsOverridden] BIT NOT NULL DEFAULT 0;
        PRINT '  + Column SectorIsOverridden added.';
    END

    -- AddTransactionFields columns
    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND name = N'TransactionType')
    BEGIN
        ALTER TABLE [dbo].[PortfolioItems] ADD [TransactionType] NVARCHAR(10) NULL;
        PRINT '  + Column TransactionType added.';
    END

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND name = N'AccountType')
    BEGIN
        ALTER TABLE [dbo].[PortfolioItems] ADD [AccountType] NVARCHAR(30) NULL;
        PRINT '  + Column AccountType added.';
    END

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND name = N'OpenDate')
    BEGIN
        ALTER TABLE [dbo].[PortfolioItems] ADD [OpenDate] DATETIME2 NULL;
        PRINT '  + Column OpenDate added.';
    END

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND name = N'CloseDate')
    BEGIN
        ALTER TABLE [dbo].[PortfolioItems] ADD [CloseDate] DATETIME2 NULL;
        PRINT '  + Column CloseDate added.';
    END

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND name = N'ClosingPrice')
    BEGIN
        ALTER TABLE [dbo].[PortfolioItems] ADD [ClosingPrice] DECIMAL(18,4) NULL;
        PRINT '  + Column ClosingPrice added.';
    END

    -- AddRoleAndHoldingRole column
    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND name = N'HoldingRole')
    BEGIN
        ALTER TABLE [dbo].[PortfolioItems] ADD [HoldingRole] NVARCHAR(20) NULL;
        PRINT '  + Column HoldingRole added.';
    END
END
GO

-- Non-unique index on Symbol (allows multiple positions in same symbol across accounts)
-- Note: was unique in early migrations; constraint removed by RemovePortfolioSymbolUniqueConstraint
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_PortfolioItems_Symbol'
      AND object_id = OBJECT_ID(N'[dbo].[PortfolioItems]')
      AND is_unique = 1
)
BEGIN
    -- Drop the old unique index and recreate as non-unique
    DROP INDEX [IX_PortfolioItems_Symbol] ON [dbo].[PortfolioItems];
    PRINT 'Dropped unique index IX_PortfolioItems_Symbol (will recreate as non-unique).';
END

IF NOT EXISTS (
    SELECT 1
FROM sys.indexes
WHERE name = N'IX_PortfolioItems_Symbol'
    AND object_id = OBJECT_ID(N'[dbo].[PortfolioItems]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PortfolioItems_Symbol]
        ON [dbo].[PortfolioItems] ([Symbol] ASC);
    PRINT 'Index IX_PortfolioItems_Symbol created (non-unique).';
END
GO

-- ────────────────────────────────────────────────────────────────────────────
-- TABLE: WatchlistItems
-- Symbols the user tracks without holding a portfolio position.
-- Added by the AddWatchlistAndSector migration.
-- ────────────────────────────────────────────────────────────────────────────
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
    PRINT 'Table WatchlistItems created.';
END
ELSE
BEGIN
    PRINT 'Table WatchlistItems already exists – checking for missing columns...';

    IF NOT EXISTS (SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'[dbo].[WatchlistItems]') AND name = N'Role')
    BEGIN
        ALTER TABLE [dbo].[WatchlistItems] ADD [Role] NVARCHAR(20) NOT NULL DEFAULT 'Strategic';
        PRINT '  + Column Role added.';
    END
END
GO

-- Unique index on WatchlistItems.Symbol
IF NOT EXISTS (
    SELECT 1
FROM sys.indexes
WHERE name = N'IX_WatchlistItems_Symbol'
    AND object_id = OBJECT_ID(N'[dbo].[WatchlistItems]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_WatchlistItems_Symbol]
        ON [dbo].[WatchlistItems] ([Symbol] ASC);
    PRINT 'Index IX_WatchlistItems_Symbol created.';
END
GO

-- ────────────────────────────────────────────────────────────────────────────
-- TABLE: CashItems
-- Cash positions in the portfolio.
-- ────────────────────────────────────────────────────────────────────────────
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
    PRINT 'Table CashItems created.';
END
ELSE
BEGIN
    PRINT 'Table CashItems already exists – skipping.';
END
GO

-- ────────────────────────────────────────────────────────────────────────────
-- TABLE: OptionItems
-- Options positions in the portfolio.
-- ────────────────────────────────────────────────────────────────────────────
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
        -- CALL or PUT
        [ExpirationDate] DATETIME2 NOT NULL,
        [Strike] DECIMAL(18,4) NOT NULL,
        [Premium] DECIMAL(18,4) NOT NULL,
        [NumberOfContracts] INT NOT NULL,
        [MarketPrice] DECIMAL(18,4) NOT NULL,
        [AddedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT [PK_OptionItems] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT 'Table OptionItems created.';
END
ELSE
BEGIN
    PRINT 'Table OptionItems already exists – checking for missing columns...';

    -- AddTransactionFields columns
    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[OptionItems]') AND name = N'TransactionType')
    BEGIN
        ALTER TABLE [dbo].[OptionItems] ADD [TransactionType] NVARCHAR(10) NULL;
        PRINT '  + Column TransactionType added.';
    END

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[OptionItems]') AND name = N'AccountType')
    BEGIN
        ALTER TABLE [dbo].[OptionItems] ADD [AccountType] NVARCHAR(30) NULL;
        PRINT '  + Column AccountType added.';
    END

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[OptionItems]') AND name = N'OpenDate')
    BEGIN
        ALTER TABLE [dbo].[OptionItems] ADD [OpenDate] DATETIME2 NULL;
        PRINT '  + Column OpenDate added.';
    END

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[OptionItems]') AND name = N'CloseDate')
    BEGIN
        ALTER TABLE [dbo].[OptionItems] ADD [CloseDate] DATETIME2 NULL;
        PRINT '  + Column CloseDate added.';
    END

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[OptionItems]') AND name = N'ClosingPrice')
    BEGIN
        ALTER TABLE [dbo].[OptionItems] ADD [ClosingPrice] DECIMAL(18,4) NULL;
        PRINT '  + Column ClosingPrice added.';
    END
END
GO

-- ────────────────────────────────────────────────────────────────────────────
-- TABLE: NotificationRecipients
-- Email addresses that receive RSI Confirmed Signal alerts.
-- Replaces the notification-recipients.json file (which is excluded from git
-- because it contains personal data).
-- ────────────────────────────────────────────────────────────────────────────
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
        -- max RFC 5321 length
        [IsActive] BIT NOT NULL DEFAULT 1,
        [AddedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT [PK_NotificationRecipients] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UQ_NotificationRecipients_Email] UNIQUE ([Email])
    );
    PRINT 'Table NotificationRecipients created.';
END
ELSE
BEGIN
    PRINT 'Table NotificationRecipients already exists – skipping.';
END
GO

-- ────────────────────────────────────────────────────────────────────────────
-- TABLE: __EFMigrationsHistory
-- EF Core creates this automatically; included here for completeness
-- when setting up the database manually.
-- ────────────────────────────────────────────────────────────────────────────
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
    PRINT 'Table __EFMigrationsHistory created.';
END
GO

PRINT '';
PRINT '=== All tables verified / created successfully ===';
GO
