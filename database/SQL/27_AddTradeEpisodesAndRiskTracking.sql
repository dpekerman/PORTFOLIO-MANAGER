BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028145833_AddTradeEpisodesAndRiskTracking'
)
BEGIN
    ALTER TABLE [PortfolioItems] ADD [TradeEpisodeId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028145833_AddTradeEpisodesAndRiskTracking'
)
BEGIN
    CREATE TABLE [BrokerStopRecords] (
        [Id] int NOT NULL IDENTITY,
        [OrderIdentity] uniqueidentifier NOT NULL,
        [ImportKey] nvarchar(200) NULL,
        [UserId] nvarchar(450) NULL,
        [AccountType] nvarchar(120) NOT NULL,
        [Symbol] nvarchar(20) NOT NULL,
        [OrderType] nvarchar(20) NOT NULL,
        [TriggerPrice] decimal(18,4) NOT NULL,
        [LimitPrice] decimal(18,4) NULL,
        [Currency] nvarchar(3) NOT NULL,
        [UserStatus] nvarchar(30) NOT NULL,
        [BrokerVerificationStatus] nvarchar(20) NOT NULL,
        [CoveredQuantity] decimal(18,6) NULL,
        [PlacedDate] datetime2 NULL,
        [BrokerOrderRef] nvarchar(100) NULL,
        [LastUserVerifiedDate] datetime2 NULL,
        [ConfirmationSource] nvarchar(30) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [TradeEpisodeId] uniqueidentifier NULL,
        [EpisodeOpenDate] datetime2 NULL,
        [ReplacedByRecordId] int NULL,
        [BrokerCancellationUnverified] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_BrokerStopRecords] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028145833_AddTradeEpisodesAndRiskTracking'
)
BEGIN
    CREATE TABLE [RiskPlans] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NULL,
        [AccountType] nvarchar(120) NOT NULL,
        [Symbol] nvarchar(20) NOT NULL,
        [PlannedStopPrice] decimal(18,4) NULL,
        [TargetPrice] decimal(18,4) NULL,
        [StopType] nvarchar(10) NOT NULL,
        [StopRationale] nvarchar(1000) NULL,
        [TradeRationale] nvarchar(1000) NULL,
        [RiskNotes] nvarchar(2000) NULL,
        [Currency] nvarchar(3) NULL,
        [TradeEpisodeId] uniqueidentifier NULL,
        [EpisodeOpenDate] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_RiskPlans] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028145833_AddTradeEpisodesAndRiskTracking'
)
BEGIN
    CREATE TABLE [TradeEpisodes] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] nvarchar(450) NULL,
        [AccountType] nvarchar(120) NOT NULL,
        [Symbol] nvarchar(20) NOT NULL,
        [OpenDate] datetime2 NOT NULL,
        [ClosedDate] datetime2 NULL,
        [Status] nvarchar(10) NOT NULL,
        [Source] nvarchar(20) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_TradeEpisodes] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028145833_AddTradeEpisodesAndRiskTracking'
)
BEGIN
    CREATE TABLE [RiskAuditEvents] (
        [Id] bigint NOT NULL IDENTITY,
        [OccurredAtUtc] datetime2 NOT NULL,
        [UserId] nvarchar(450) NULL,
        [UserName] nvarchar(256) NULL,
        [Source] nvarchar(10) NOT NULL,
        [Symbol] nvarchar(20) NOT NULL,
        [AccountType] nvarchar(120) NOT NULL,
        [RiskPlanId] int NULL,
        [BrokerStopRecordId] int NULL,
        [EventType] nvarchar(40) NOT NULL,
        [ChangesJson] nvarchar(max) NULL,
        [Reason] nvarchar(1000) NULL,
        [CorrelationId] nvarchar(64) NULL,
        CONSTRAINT [PK_RiskAuditEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RiskAuditEvents_BrokerStopRecords_BrokerStopRecordId] FOREIGN KEY ([BrokerStopRecordId]) REFERENCES [BrokerStopRecords] ([Id]),
        CONSTRAINT [FK_RiskAuditEvents_RiskPlans_RiskPlanId] FOREIGN KEY ([RiskPlanId]) REFERENCES [RiskPlans] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028145833_AddTradeEpisodesAndRiskTracking'
)
BEGIN
    CREATE INDEX [IX_PortfolioItems_TradeEpisodeId] ON [PortfolioItems] ([TradeEpisodeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028145833_AddTradeEpisodesAndRiskTracking'
)
BEGIN
    CREATE INDEX [IX_BrokerStopRecords_AccountType_Symbol_UserStatus] ON [BrokerStopRecords] ([AccountType], [Symbol], [UserStatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028145833_AddTradeEpisodesAndRiskTracking'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BrokerStopRecords_OrderIdentity] ON [BrokerStopRecords] ([OrderIdentity]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028145833_AddTradeEpisodesAndRiskTracking'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_BrokerStopRecords_ImportKey] ON [BrokerStopRecords] ([ImportKey]) WHERE [ImportKey] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028145833_AddTradeEpisodesAndRiskTracking'
)
BEGIN
    CREATE INDEX [IX_RiskAuditEvents_AccountType_Symbol_OccurredAtUtc] ON [RiskAuditEvents] ([AccountType], [Symbol], [OccurredAtUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028145833_AddTradeEpisodesAndRiskTracking'
)
BEGIN
    CREATE INDEX [IX_RiskAuditEvents_BrokerStopRecordId] ON [RiskAuditEvents] ([BrokerStopRecordId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028145833_AddTradeEpisodesAndRiskTracking'
)
BEGIN
    CREATE INDEX [IX_RiskAuditEvents_RiskPlanId] ON [RiskAuditEvents] ([RiskPlanId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028145833_AddTradeEpisodesAndRiskTracking'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_RiskPlans_UserId_AccountType_Symbol] ON [RiskPlans] ([UserId], [AccountType], [Symbol]) WHERE [UserId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028145833_AddTradeEpisodesAndRiskTracking'
)
BEGIN
    CREATE INDEX [IX_TradeEpisodes_AccountType_Symbol_Status] ON [TradeEpisodes] ([AccountType], [Symbol], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028145833_AddTradeEpisodesAndRiskTracking'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261028145833_AddTradeEpisodesAndRiskTracking', N'8.0.10');
END;
GO

COMMIT;
GO

