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

    -- AddNotesFields column
    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND name = N'Notes')
    BEGIN
        ALTER TABLE [dbo].[PortfolioItems] ADD [Notes] NVARCHAR(MAX) NULL;
        PRINT '  + Column Notes added.';
    END

    -- AddDecisionSource column (2026-07-08)
    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]') AND name = N'DecisionSource')
    BEGIN
        ALTER TABLE [dbo].[PortfolioItems] ADD [DecisionSource] NVARCHAR(50) NULL;
        PRINT '  + Column DecisionSource added.';
    END
END
GO

-- Non-unique index on Symbol (allows multiple positions in same symbol across accounts)
-- Note: was unique in early migrations; constraint removed by RemovePortfolioSymbolUniqueConstraint
IF EXISTS (
    SELECT 1
FROM sys.indexes
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

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
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

-- ────────────────────────────────────────────────────────────────────────────
-- Additional columns added 2026-07-08
-- ────────────────────────────────────────────────────────────────────────────

-- CashItems: AccountType (AddCashAccountType migration)
IF NOT EXISTS (SELECT 1
FROM sys.columns
WHERE object_id = OBJECT_ID(N'[dbo].[CashItems]') AND name = N'AccountType')
BEGIN
    ALTER TABLE [dbo].[CashItems] ADD [AccountType] NVARCHAR(30) NULL;
    PRINT '+ CashItems.AccountType added.';
END
GO

-- OptionItems: DecisionSource
IF NOT EXISTS (SELECT 1
FROM sys.columns
WHERE object_id = OBJECT_ID(N'[dbo].[OptionItems]') AND name = N'DecisionSource')
BEGIN
    ALTER TABLE [dbo].[OptionItems] ADD [DecisionSource] NVARCHAR(50) NULL;
    PRINT '+ OptionItems.DecisionSource added.';
END
GO

-- WatchlistItems: IsFavorite (AddWatchlistFavorite migration)
IF NOT EXISTS (SELECT 1
FROM sys.columns
WHERE object_id = OBJECT_ID(N'[dbo].[WatchlistItems]') AND name = N'IsFavorite')
BEGIN
    ALTER TABLE [dbo].[WatchlistItems] ADD [IsFavorite] BIT NOT NULL DEFAULT 0;
    PRINT '+ WatchlistItems.IsFavorite added.';
END
GO

-- ────────────────────────────────────────────────────────────────────────────
-- TABLE: AllocationRiskTargets  (2026-07-08)
-- ────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[AllocationRiskTargets]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[AllocationRiskTargets]
    (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [Role] NVARCHAR(30) NOT NULL,
        [TargetPct] DECIMAL(5,2) NOT NULL,
        [DisplayOrder] INT NOT NULL DEFAULT 0,
        CONSTRAINT [PK_AllocationRiskTargets] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT 'Table AllocationRiskTargets created.';
END
GO

-- ────────────────────────────────────────────────────────────────────────────
-- TABLE: AllocationSectorTargets  (2026-07-08)
-- ────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[AllocationSectorTargets]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[AllocationSectorTargets]
    (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [Sector] NVARCHAR(100) NOT NULL,
        [TargetPct] DECIMAL(5,2) NOT NULL,
        [DisplayOrder] INT NOT NULL DEFAULT 0,
        CONSTRAINT [PK_AllocationSectorTargets] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT 'Table AllocationSectorTargets created.';
END
GO

-- ────────────────────────────────────────────────────────────────────────────
-- TABLE: SinglePositionLimits  (2026-07-08)
-- ────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[SinglePositionLimits]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[SinglePositionLimits]
    (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [Role] NVARCHAR(30) NOT NULL,
        [TargetPct] DECIMAL(5,2) NOT NULL,
        [DisplayOrder] INT NOT NULL DEFAULT 0,
        CONSTRAINT [PK_SinglePositionLimits] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT 'Table SinglePositionLimits created.';
END
GO

-- ────────────────────────────────────────────────────────────────────────────
-- TABLE: PortfolioValueHistories  (2026-07-19)
-- Stores end-of-day portfolio value snapshots persisted at 4:30 PM ET.
-- ────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioValueHistories]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[PortfolioValueHistories]
    (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [RecordedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [RecordedDate] NVARCHAR(10) NOT NULL DEFAULT '',
        [TotalValue] DECIMAL(18,4) NOT NULL DEFAULT 0,
        [StocksValue] DECIMAL(18,4) NOT NULL DEFAULT 0,
        [CashValue] DECIMAL(18,4) NOT NULL DEFAULT 0,
        [OptionsValue] DECIMAL(18,4) NOT NULL DEFAULT 0,
        CONSTRAINT [PK_PortfolioValueHistories] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    CREATE NONCLUSTERED INDEX [IX_PortfolioValueHistories_RecordedDate]
        ON [dbo].[PortfolioValueHistories] ([RecordedDate] ASC);
    PRINT 'Table PortfolioValueHistories created.';
END
GO

-- ────────────────────────────────────────────────────────────────────────────
-- TABLE: ValueScreenerSnapshots  (2026-07-12)
-- Persists the latest Value Screener run results per origin (portfolio/watchlist).
-- ────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[ValueScreenerSnapshots]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[ValueScreenerSnapshots]
    (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [Origin] NVARCHAR(20) NOT NULL,
        [RunAt] DATETIME2 NOT NULL,
        [ResultsJson] NVARCHAR(MAX) NOT NULL DEFAULT '[]',
        CONSTRAINT [PK_ValueScreenerSnapshots] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    CREATE NONCLUSTERED INDEX [IX_ValueScreenerSnapshots_Origin_RunAt]
        ON [dbo].[ValueScreenerSnapshots] ([Origin] ASC, [RunAt] ASC);
    PRINT 'Table ValueScreenerSnapshots created.';
END
GO

-- ────────────────────────────────────────────────────────────────────────────
-- TABLE: ValueScreenerScheduleConfigs  (2026-07-12)
-- Stores the single schedule configuration row for the Value Screener.
-- ────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[ValueScreenerScheduleConfigs]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[ValueScreenerScheduleConfigs]
    (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [ScheduledTimeEt] NVARCHAR(10) NOT NULL DEFAULT '17:00',
        [Enabled] BIT NOT NULL DEFAULT 1,
        [LastPortfolioRunAt] DATETIME2 NULL,
        [LastWatchlistRunAt] DATETIME2 NULL,
        CONSTRAINT [PK_ValueScreenerScheduleConfigs] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT 'Table ValueScreenerScheduleConfigs created.';
END
GO

-- ----------------------------------------------------------------------------
-- TABLE: ValueScreenerSnapshots  (2026-07-12)
-- Persists the latest Value Screener run results per origin (portfolio/watchlist).
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[ValueScreenerSnapshots]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[ValueScreenerSnapshots]
    (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [Origin] NVARCHAR(20) NOT NULL,
        [RunAt] DATETIME2 NOT NULL,
        [ResultsJson] NVARCHAR(MAX) NOT NULL DEFAULT '[]',
        CONSTRAINT [PK_ValueScreenerSnapshots] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    CREATE NONCLUSTERED INDEX [IX_ValueScreenerSnapshots_Origin_RunAt]
        ON [dbo].[ValueScreenerSnapshots] ([Origin] ASC, [RunAt] ASC);
    PRINT 'Table ValueScreenerSnapshots created.';
END
GO

-- ----------------------------------------------------------------------------
-- TABLE: ValueScreenerScheduleConfigs  (2026-07-12)
-- Stores the single schedule configuration row for the Value Screener.
-- ----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[ValueScreenerScheduleConfigs]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[ValueScreenerScheduleConfigs]
    (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [ScheduledTimeEt] NVARCHAR(10) NOT NULL DEFAULT '17:00',
        [Enabled] BIT NOT NULL DEFAULT 1,
        [LastPortfolioRunAt] DATETIME2 NULL,
        [LastWatchlistRunAt] DATETIME2 NULL,
        CONSTRAINT [PK_ValueScreenerScheduleConfigs] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT 'Table ValueScreenerScheduleConfigs created.';
END
GO


-- -- ASP.NET Core Identity + RefreshTokens ---------------------------------
-- Added: 2026-08-02  (migration: AddIdentityAndRefreshTokens)

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AspNetRoles')
BEGIN
    CREATE TABLE [dbo].[AspNetRoles] (
        [Id] NVARCHAR(450) NOT NULL, [Name] NVARCHAR(256) NULL,
        [NormalizedName] NVARCHAR(256) NULL, [ConcurrencyStamp] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id]));
    CREATE UNIQUE INDEX [RoleNameIndex] ON [dbo].[AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AspNetUsers')
BEGIN
    CREATE TABLE [dbo].[AspNetUsers] (
        [Id] NVARCHAR(450) NOT NULL, [DisplayName] NVARCHAR(MAX) NOT NULL DEFAULT '',
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [UserName] NVARCHAR(256) NULL, [NormalizedUserName] NVARCHAR(256) NULL,
        [Email] NVARCHAR(256) NULL, [NormalizedEmail] NVARCHAR(256) NULL,
        [EmailConfirmed] BIT NOT NULL DEFAULT 0, [PasswordHash] NVARCHAR(MAX) NULL,
        [SecurityStamp] NVARCHAR(MAX) NULL, [ConcurrencyStamp] NVARCHAR(MAX) NULL,
        [PhoneNumber] NVARCHAR(MAX) NULL, [PhoneNumberConfirmed] BIT NOT NULL DEFAULT 0,
        [TwoFactorEnabled] BIT NOT NULL DEFAULT 0, [LockoutEnd] DATETIMEOFFSET NULL,
        [LockoutEnabled] BIT NOT NULL DEFAULT 1, [AccessFailedCount] INT NOT NULL DEFAULT 0,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id]));
    CREATE INDEX [EmailIndex] ON [dbo].[AspNetUsers] ([NormalizedEmail]);
    CREATE UNIQUE INDEX [UserNameIndex] ON [dbo].[AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AspNetRoleClaims')
BEGIN
    CREATE TABLE [dbo].[AspNetRoleClaims] ([Id] INT NOT NULL IDENTITY(1,1), [RoleId] NVARCHAR(450) NOT NULL, [ClaimType] NVARCHAR(MAX) NULL, [ClaimValue] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_Roles] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE);
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [dbo].[AspNetRoleClaims] ([RoleId]);
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AspNetUserClaims')
BEGIN
    CREATE TABLE [dbo].[AspNetUserClaims] ([Id] INT NOT NULL IDENTITY(1,1), [UserId] NVARCHAR(450) NOT NULL, [ClaimType] NVARCHAR(MAX) NULL, [ClaimValue] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE);
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [dbo].[AspNetUserClaims] ([UserId]);
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AspNetUserLogins')
BEGIN
    CREATE TABLE [dbo].[AspNetUserLogins] ([LoginProvider] NVARCHAR(450) NOT NULL, [ProviderKey] NVARCHAR(450) NOT NULL, [ProviderDisplayName] NVARCHAR(MAX) NULL, [UserId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider],[ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE);
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [dbo].[AspNetUserLogins] ([UserId]);
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AspNetUserRoles')
BEGIN
    CREATE TABLE [dbo].[AspNetUserRoles] ([UserId] NVARCHAR(450) NOT NULL, [RoleId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId],[RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_Roles] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE);
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [dbo].[AspNetUserRoles] ([RoleId]);
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AspNetUserTokens')
BEGIN
    CREATE TABLE [dbo].[AspNetUserTokens] ([UserId] NVARCHAR(450) NOT NULL, [LoginProvider] NVARCHAR(450) NOT NULL, [Name] NVARCHAR(450) NOT NULL, [Value] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId],[LoginProvider],[Name]),
        CONSTRAINT [FK_AspNetUserTokens_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE);
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'RefreshTokens')
BEGIN
    CREATE TABLE [dbo].[RefreshTokens] (
        [Id] INT NOT NULL IDENTITY(1,1), [Token] NVARCHAR(64) NOT NULL,
        [UserId] NVARCHAR(450) NOT NULL, [ExpiresAt] DATETIME2 NOT NULL,
        [IsRevoked] BIT NOT NULL DEFAULT 0, [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE);
    CREATE UNIQUE INDEX [IX_RefreshTokens_Token] ON [dbo].[RefreshTokens] ([Token]);
    CREATE INDEX [IX_RefreshTokens_UserId] ON [dbo].[RefreshTokens] ([UserId]);
END
GO
