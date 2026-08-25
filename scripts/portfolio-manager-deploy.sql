IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611201226_InitialCreate'
)
BEGIN
    CREATE TABLE [PortfolioItems] (
        [Id] int NOT NULL IDENTITY,
        [Symbol] nvarchar(20) NOT NULL,
        [CompanyName] nvarchar(200) NOT NULL,
        [Shares] decimal(18,6) NOT NULL,
        [AverageCostBasis] decimal(18,4) NOT NULL,
        [AddedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PortfolioItems] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611201226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PortfolioItems_Symbol] ON [PortfolioItems] ([Symbol]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611201226_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260611201226_InitialCreate', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611235305_AddWatchlistAndSector'
)
BEGIN
    ALTER TABLE [PortfolioItems] ADD [Industry] nvarchar(100) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611235305_AddWatchlistAndSector'
)
BEGIN
    ALTER TABLE [PortfolioItems] ADD [Sector] nvarchar(100) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611235305_AddWatchlistAndSector'
)
BEGIN
    CREATE TABLE [WatchlistItems] (
        [Id] int NOT NULL IDENTITY,
        [Symbol] nvarchar(20) NOT NULL,
        [Notes] nvarchar(500) NOT NULL DEFAULT N'',
        [AddedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_WatchlistItems] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611235305_AddWatchlistAndSector'
)
BEGIN
    CREATE UNIQUE INDEX [IX_WatchlistItems_Symbol] ON [WatchlistItems] ([Symbol]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611235305_AddWatchlistAndSector'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260611235305_AddWatchlistAndSector', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260612030112_AddManualPosition'
)
BEGIN
    ALTER TABLE [PortfolioItems] ADD [IsManual] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260612030112_AddManualPosition'
)
BEGIN
    ALTER TABLE [PortfolioItems] ADD [ManualMarketValue] decimal(18,4) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260612030112_AddManualPosition'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260612030112_AddManualPosition', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615120000_AddSectorOverride'
)
BEGIN
    ALTER TABLE [PortfolioItems] ADD [SectorIsOverridden] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615120000_AddSectorOverride'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260615120000_AddSectorOverride', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618010551_AddCashOptionAndAdhocTables'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260618010551_AddCashOptionAndAdhocTables', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619000000_AddTransactionFields'
)
BEGIN
    ALTER TABLE [PortfolioItems] ADD [TransactionType] nvarchar(10) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619000000_AddTransactionFields'
)
BEGIN
    ALTER TABLE [PortfolioItems] ADD [AccountType] nvarchar(30) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619000000_AddTransactionFields'
)
BEGIN
    ALTER TABLE [PortfolioItems] ADD [OpenDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619000000_AddTransactionFields'
)
BEGIN
    ALTER TABLE [PortfolioItems] ADD [CloseDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619000000_AddTransactionFields'
)
BEGIN
    ALTER TABLE [PortfolioItems] ADD [ClosingPrice] decimal(18,4) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619000000_AddTransactionFields'
)
BEGIN
    ALTER TABLE [OptionItems] ADD [TransactionType] nvarchar(10) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619000000_AddTransactionFields'
)
BEGIN
    ALTER TABLE [OptionItems] ADD [AccountType] nvarchar(30) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619000000_AddTransactionFields'
)
BEGIN
    ALTER TABLE [OptionItems] ADD [OpenDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619000000_AddTransactionFields'
)
BEGIN
    ALTER TABLE [OptionItems] ADD [CloseDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619000000_AddTransactionFields'
)
BEGIN
    ALTER TABLE [OptionItems] ADD [ClosingPrice] decimal(18,4) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619000000_AddTransactionFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260619000000_AddTransactionFields', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619000002_RemovePortfolioSymbolUniqueConstraint'
)
BEGIN
    DROP INDEX [IX_PortfolioItems_Symbol] ON [PortfolioItems];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619000002_RemovePortfolioSymbolUniqueConstraint'
)
BEGIN
    CREATE INDEX [IX_PortfolioItems_Symbol] ON [PortfolioItems] ([Symbol]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619000002_RemovePortfolioSymbolUniqueConstraint'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260619000002_RemovePortfolioSymbolUniqueConstraint', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622183326_AddRoleAndHoldingRole'
)
BEGIN
    ALTER TABLE [WatchlistItems] ADD [Role] nvarchar(20) NOT NULL DEFAULT N'Strategic';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622183326_AddRoleAndHoldingRole'
)
BEGIN
    ALTER TABLE [PortfolioItems] ADD [HoldingRole] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622183326_AddRoleAndHoldingRole'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260622183326_AddRoleAndHoldingRole', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260623153856_AddNotesFields'
)
BEGIN
    ALTER TABLE [PortfolioItems] ADD [Notes] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260623153856_AddNotesFields'
)
BEGIN
    ALTER TABLE [OptionItems] ADD [Notes] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260623153856_AddNotesFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260623153856_AddNotesFields', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260624000000_AddDailySignals'
)
BEGIN
    CREATE TABLE [DailySignals] (
        [Id] int NOT NULL IDENTITY,
        [Symbol] nvarchar(20) NOT NULL,
        [CompanyName] nvarchar(200) NOT NULL DEFAULT N'',
        [ScanType] nvarchar(20) NOT NULL,
        [SignalType] nvarchar(30) NOT NULL,
        [Rsi] decimal(7,4) NOT NULL,
        [Price] decimal(18,4) NOT NULL,
        [TriggerDetails] nvarchar(1000) NOT NULL DEFAULT N'',
        [SignalDate] nvarchar(10) NOT NULL,
        [RecordedAt] datetime2 NOT NULL,
        [RuleVersion] nvarchar(20) NOT NULL DEFAULT N'Legacy',
        [SignalState] nvarchar(30) NOT NULL DEFAULT N'Active',
        [Sector] nvarchar(100) NOT NULL DEFAULT N'',
        [ReversalProbability] nvarchar(20) NOT NULL DEFAULT N'',
        [VolumeSignal] nvarchar(30) NOT NULL DEFAULT N'',
        [Notes] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_DailySignals] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260624000000_AddDailySignals'
)
BEGIN
    CREATE INDEX [IX_DailySignals_SignalDate] ON [DailySignals] ([SignalDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260624000000_AddDailySignals'
)
BEGIN
    CREATE INDEX [IX_DailySignals_Symbol] ON [DailySignals] ([Symbol]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260624000000_AddDailySignals'
)
BEGIN
    CREATE INDEX [IX_DailySignals_Symbol_SignalDate] ON [DailySignals] ([Symbol], [SignalDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260624000000_AddDailySignals'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260624000000_AddDailySignals', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802231757_AddIdentityAndRefreshTokens'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802231757_AddIdentityAndRefreshTokens'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [DisplayName] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802231757_AddIdentityAndRefreshTokens'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802231757_AddIdentityAndRefreshTokens'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802231757_AddIdentityAndRefreshTokens'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802231757_AddIdentityAndRefreshTokens'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802231757_AddIdentityAndRefreshTokens'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802231757_AddIdentityAndRefreshTokens'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] int NOT NULL IDENTITY,
        [Token] nvarchar(64) NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [IsRevoked] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802231757_AddIdentityAndRefreshTokens'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802231757_AddIdentityAndRefreshTokens'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802231757_AddIdentityAndRefreshTokens'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802231757_AddIdentityAndRefreshTokens'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802231757_AddIdentityAndRefreshTokens'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802231757_AddIdentityAndRefreshTokens'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802231757_AddIdentityAndRefreshTokens'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802231757_AddIdentityAndRefreshTokens'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_Token] ON [RefreshTokens] ([Token]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802231757_AddIdentityAndRefreshTokens'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802231757_AddIdentityAndRefreshTokens'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260802231757_AddIdentityAndRefreshTokens', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803141906_AddUserIdToPrivateData'
)
BEGIN
    DROP INDEX [IX_WatchlistItems_Symbol] ON [WatchlistItems];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803141906_AddUserIdToPrivateData'
)
BEGIN
    ALTER TABLE [WatchlistItems] ADD [UserId] nvarchar(450) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803141906_AddUserIdToPrivateData'
)
BEGIN
    ALTER TABLE [PortfolioItems] ADD [UserId] nvarchar(450) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803141906_AddUserIdToPrivateData'
)
BEGIN
    ALTER TABLE [OptionItems] ADD [UserId] nvarchar(450) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803141906_AddUserIdToPrivateData'
)
BEGIN
    ALTER TABLE [CashItems] ADD [UserId] nvarchar(450) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803141906_AddUserIdToPrivateData'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_WatchlistItems_Symbol_UserId] ON [WatchlistItems] ([Symbol], [UserId]) WHERE [UserId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803141906_AddUserIdToPrivateData'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260803141906_AddUserIdToPrivateData', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811000001_AddDecisionSourceClosed'
)
BEGIN
    ALTER TABLE [PortfolioItems] ADD [DecisionSourceClosed] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811000001_AddDecisionSourceClosed'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260811000001_AddDecisionSourceClosed', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814222541_AddOptionDecisionSourceClosed'
)
BEGIN

                    IF NOT EXISTS (
                        SELECT 1 FROM sys.columns
                        WHERE object_id = OBJECT_ID('dbo.OptionItems') AND name = 'DecisionSourceClosed'
                    )
                        ALTER TABLE dbo.OptionItems ADD DecisionSourceClosed nvarchar(50) NULL;
                
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814222541_AddOptionDecisionSourceClosed'
)
BEGIN

                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailySignals') AND name = 'TrendShift')
                        ALTER TABLE dbo.DailySignals ADD TrendShift nvarchar(50) NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailySignals') AND name = 'RsiDelta1D')
                        ALTER TABLE dbo.DailySignals ADD RsiDelta1D decimal(18,4) NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailySignals') AND name = 'EntryPrice')
                        ALTER TABLE dbo.DailySignals ADD EntryPrice decimal(18,4) NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailySignals') AND name = 'StopLossPrice')
                        ALTER TABLE dbo.DailySignals ADD StopLossPrice decimal(18,4) NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailySignals') AND name = 'RiskPerShare')
                        ALTER TABLE dbo.DailySignals ADD RiskPerShare decimal(18,4) NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailySignals') AND name = 'Sma200')
                        ALTER TABLE dbo.DailySignals ADD Sma200 decimal(18,4) NULL;
                
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814222541_AddOptionDecisionSourceClosed'
)
BEGIN

                    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StagedSignals')
                    BEGIN
                        CREATE TABLE dbo.StagedSignals (
                            StagedId          INT IDENTITY(1,1) NOT NULL,
                            Symbol            nvarchar(20) NOT NULL,
                            ScanType          nvarchar(20) NOT NULL,
                            BasePrice         decimal(18,4) NOT NULL,
                            BaseRsi           decimal(18,4) NOT NULL,
                            BaseHigh          decimal(18,4) NOT NULL,
                            BaseLow           decimal(18,4) NOT NULL,
                            PreviousPrice     decimal(18,4) NULL,
                            PreviousRsi       decimal(18,4) NULL,
                            CurrentPrice      decimal(18,4) NULL,
                            CurrentRsi        decimal(18,4) NULL,
                            RsiDelta1D        decimal(18,4) NULL,
                            ExtremeLow        decimal(18,4) NULL,
                            ExtremeHigh       decimal(18,4) NULL,
                            StagedDate        date NOT NULL,
                            LastEvaluatedDate date NULL,
                            IsActiveWatch     bit NOT NULL DEFAULT 1,
                            CreatedAt         datetime2 NOT NULL DEFAULT SYSDATETIME(),
                            UpdatedAt         datetime2 NOT NULL DEFAULT SYSDATETIME(),
                            CONSTRAINT PK_StagedSignals PRIMARY KEY (StagedId)
                        );
                        CREATE INDEX IX_StagedSignals_Symbol ON dbo.StagedSignals (Symbol);
                        CREATE INDEX IX_StagedSignals_IsActiveWatch ON dbo.StagedSignals (IsActiveWatch);
                    END
                
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814222541_AddOptionDecisionSourceClosed'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260814222541_AddOptionDecisionSourceClosed', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815005554_AddEma9AtEntryToDailySignals'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OptionItems]') AND [c].[name] = N'DecisionSourceClosed');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [OptionItems] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [OptionItems] ALTER COLUMN [DecisionSourceClosed] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815005554_AddEma9AtEntryToDailySignals'
)
BEGIN
    ALTER TABLE [DailySignals] ADD [Ema9AtEntry] decimal(18,4) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815005554_AddEma9AtEntryToDailySignals'
)
BEGIN
    ALTER TABLE [DailySignals] ADD [Ema9ConfirmedAtEntry] bit NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815005554_AddEma9AtEntryToDailySignals'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260815005554_AddEma9AtEntryToDailySignals', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817202407_AddFibonacciToDailySignals'
)
BEGIN
    ALTER TABLE [DailySignals] ADD [Fib61_8AtSignal] decimal(18,4) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817202407_AddFibonacciToDailySignals'
)
BEGIN
    ALTER TABLE [DailySignals] ADD [FibStatusAtSignal] nvarchar(30) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817202407_AddFibonacciToDailySignals'
)
BEGIN
    ALTER TABLE [DailySignals] ADD [FibZoneAtSignal] nvarchar(30) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817202407_AddFibonacciToDailySignals'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260817202407_AddFibonacciToDailySignals', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260820013706_RepairMissingTablesForAzure'
)
BEGIN

    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CashItems')
    BEGIN
        CREATE TABLE [CashItems] (
            [Id]          INT IDENTITY(1,1) NOT NULL,
            [Description] NVARCHAR(200)     NOT NULL CONSTRAINT [DF_CashItems_Description] DEFAULT N'CASH',
            [Amount]      DECIMAL(18,4)     NOT NULL DEFAULT 0,
            [AddedAt]     DATETIME2         NOT NULL DEFAULT GETUTCDATE(),
            CONSTRAINT [PK_CashItems] PRIMARY KEY ([Id])
        );
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260820013706_RepairMissingTablesForAzure'
)
BEGIN

    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OptionItems')
    BEGIN
        CREATE TABLE [OptionItems] (
            [Id]                INT IDENTITY(1,1) NOT NULL,
            [UnderlyingTicker]  NVARCHAR(20)  NOT NULL DEFAULT N'',
            [PositionType]      NVARCHAR(10)  NOT NULL DEFAULT N'',
            [ExpirationDate]    DATETIME2     NOT NULL DEFAULT '0001-01-01',
            [Strike]            DECIMAL(18,4) NOT NULL DEFAULT 0,
            [Premium]           DECIMAL(18,4) NOT NULL DEFAULT 0,
            [NumberOfContracts] INT           NOT NULL DEFAULT 0,
            [MarketPrice]       DECIMAL(18,4) NOT NULL DEFAULT 0,
            [AddedAt]           DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
            CONSTRAINT [PK_OptionItems] PRIMARY KEY ([Id])
        );
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260820013706_RepairMissingTablesForAzure'
)
BEGIN

    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AdhocAnalysisSessions')
    BEGIN
        CREATE TABLE [AdhocAnalysisSessions] (
            [Id]                  INT IDENTITY(1,1) NOT NULL,
            [SessionKey]          NVARCHAR(100) NOT NULL CONSTRAINT [DF_Adhoc_SessionKey]  DEFAULT N'default',
            [Symbols]             NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_Adhoc_Symbols]     DEFAULT N'[]',
            [ResultsJson]         NVARCHAR(MAX) NULL,
            [OversoldThreshold]   DECIMAL(5,2)  NOT NULL CONSTRAINT [DF_Adhoc_Oversold]   DEFAULT 30,
            [OverboughtThreshold] DECIMAL(5,2)  NOT NULL CONSTRAINT [DF_Adhoc_Overbought] DEFAULT 75,
            [LogicMode]           NVARCHAR(20)  NOT NULL CONSTRAINT [DF_Adhoc_LogicMode]  DEFAULT N'Legacy',
            [CreatedAt]           DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
            [UpdatedAt]           DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
            CONSTRAINT [PK_AdhocAnalysisSessions] PRIMARY KEY ([Id])
        );
        CREATE INDEX [IX_AdhocAnalysisSessions_SessionKey_UpdatedAt]
            ON [AdhocAnalysisSessions] ([SessionKey], [UpdatedAt]);
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260820013706_RepairMissingTablesForAzure'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260820013706_RepairMissingTablesForAzure', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821160920_AddRsiSnapshotAndUserPreferences'
)
BEGIN

    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RsiScanSnapshots')
    BEGIN
        CREATE TABLE [RsiScanSnapshots] (
            [Id]             INT           NOT NULL CONSTRAINT [PK_RsiScanSnapshots] PRIMARY KEY,
            [SnapshotJson]   NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_RsiSnap_Json] DEFAULT N'{}',
            [ScannedAt]      DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
            [SymbolCount]    INT           NOT NULL DEFAULT 0,
            [OversoldCount]  INT           NOT NULL DEFAULT 0,
            [OverboughtCount] INT          NOT NULL DEFAULT 0
        );
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821160920_AddRsiSnapshotAndUserPreferences'
)
BEGIN

    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UserPreferences')
    BEGIN
        CREATE TABLE [UserPreferences] (
            [Id]              INT IDENTITY(1,1) NOT NULL,
            [UserId]          NVARCHAR(450)     NOT NULL,
            [PreferenceKey]   NVARCHAR(100)     NOT NULL,
            [PreferenceValue] NVARCHAR(MAX)     NOT NULL CONSTRAINT [DF_UserPref_Value] DEFAULT N'',
            [UpdatedAt]       DATETIME2         NOT NULL DEFAULT GETUTCDATE(),
            CONSTRAINT [PK_UserPreferences] PRIMARY KEY ([Id])
        );
        CREATE UNIQUE INDEX [IX_UserPreferences_UserId_PreferenceKey]
            ON [UserPreferences] ([UserId], [PreferenceKey]);
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821160920_AddRsiSnapshotAndUserPreferences'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260821160920_AddRsiSnapshotAndUserPreferences', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822152539_AddPortfolioAndWatchlistSnapshots'
)
BEGIN

    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RsiScanSnapshots')
    BEGIN
        CREATE TABLE [RsiScanSnapshots] (
            [Id]              INT           NOT NULL CONSTRAINT [PK_RsiScanSnapshots] PRIMARY KEY,
            [SnapshotJson]    NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_RsiSnap_Json]      DEFAULT N'{}',
            [ScannedAt]       DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
            [SymbolCount]     INT           NOT NULL DEFAULT 0,
            [OversoldCount]   INT           NOT NULL DEFAULT 0,
            [OverboughtCount] INT           NOT NULL DEFAULT 0
        );
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822152539_AddPortfolioAndWatchlistSnapshots'
)
BEGIN

    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UserPreferences')
    BEGIN
        CREATE TABLE [UserPreferences] (
            [Id]              INT IDENTITY(1,1) NOT NULL,
            [UserId]          NVARCHAR(450)     NOT NULL,
            [PreferenceKey]   NVARCHAR(100)     NOT NULL,
            [PreferenceValue] NVARCHAR(MAX)     NOT NULL CONSTRAINT [DF_UserPref_Value] DEFAULT N'',
            [UpdatedAt]       DATETIME2         NOT NULL DEFAULT GETUTCDATE(),
            CONSTRAINT [PK_UserPreferences] PRIMARY KEY ([Id])
        );
        CREATE UNIQUE INDEX [IX_UserPreferences_UserId_PreferenceKey]
            ON [UserPreferences] ([UserId], [PreferenceKey]);
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822152539_AddPortfolioAndWatchlistSnapshots'
)
BEGIN

    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PortfolioSnapshots')
    BEGIN
        CREATE TABLE [PortfolioSnapshots] (
            [UserId]       NVARCHAR(450) NOT NULL CONSTRAINT [PK_PortfolioSnapshots] PRIMARY KEY,
            [SnapshotJson] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_PortSnap_Json] DEFAULT N'[]',
            [UpdatedAt]    DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
            [ItemCount]    INT           NOT NULL DEFAULT 0
        );
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822152539_AddPortfolioAndWatchlistSnapshots'
)
BEGIN

    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'WatchlistSnapshots')
    BEGIN
        CREATE TABLE [WatchlistSnapshots] (
            [UserId]       NVARCHAR(450) NOT NULL CONSTRAINT [PK_WatchlistSnapshots] PRIMARY KEY,
            [SnapshotJson] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_WlSnap_Json] DEFAULT N'[]',
            [UpdatedAt]    DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
            [ItemCount]    INT           NOT NULL DEFAULT 0
        );
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822152539_AddPortfolioAndWatchlistSnapshots'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260822152539_AddPortfolioAndWatchlistSnapshots', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824202223_AddSectorIndustryConfig'
)
BEGIN
    CREATE TABLE [SectorIndustryConfigs] (
        [Id] int NOT NULL,
        [SectorsJson] nvarchar(max) NOT NULL DEFAULT N'[]',
        [IndustriesJson] nvarchar(max) NOT NULL DEFAULT N'[]',
        [DecisionSourcesJson] nvarchar(max) NOT NULL DEFAULT N'[]',
        CONSTRAINT [PK_SectorIndustryConfigs] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824202223_AddSectorIndustryConfig'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260824202223_AddSectorIndustryConfig', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825142656_AddDashboardSnapshot'
)
BEGIN
    CREATE TABLE [DashboardSnapshots] (
        [UserId] nvarchar(450) NOT NULL,
        [SnapshotJson] nvarchar(max) NOT NULL DEFAULT N'{}',
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_DashboardSnapshots] PRIMARY KEY ([UserId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825142656_AddDashboardSnapshot'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260825142656_AddDashboardSnapshot', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825154106_AddWatchlistEarningsDate'
)
BEGIN
    ALTER TABLE [WatchlistItems] ADD [EarningsDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825154106_AddWatchlistEarningsDate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260825154106_AddWatchlistEarningsDate', N'8.0.10');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825160909_AddEodPositionSizing'
)
BEGIN
    ALTER TABLE [DailySignals] ADD [PositionSizingLimitingReason] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825160909_AddEodPositionSizing'
)
BEGIN
    ALTER TABLE [DailySignals] ADD [PositionSizingPositionValue] decimal(18,4) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825160909_AddEodPositionSizing'
)
BEGIN
    ALTER TABLE [DailySignals] ADD [PositionSizingRiskAmount] decimal(18,4) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825160909_AddEodPositionSizing'
)
BEGIN
    ALTER TABLE [DailySignals] ADD [PositionSizingShares] decimal(18,6) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825160909_AddEodPositionSizing'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260825160909_AddEodPositionSizing', N'8.0.10');
END;
GO

COMMIT;
GO

