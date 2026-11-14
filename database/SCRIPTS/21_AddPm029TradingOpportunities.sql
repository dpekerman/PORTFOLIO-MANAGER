-- =====================================================================================
-- PM-029 Trading Opportunities — schema migration AddPm029TradingOpportunities
-- Generated with: dotnet ef migrations script 20261028213913_CorrectPm033EpisodeIntegrity AddPm029TradingOpportunities --idempotent
-- STATUS: REVIEW ONLY. DO NOT RUN without separate explicit authorization (PM-029 approval register).
-- Additive only: creates IndexConstituents, SectorBenchmarkMappings, OpportunitySnapshots,
-- OpportunityRefreshStates. No existing table/column is altered or dropped.
-- Rollback: dotnet ef database update CorrectPm033EpisodeIntegrity, or restore the PM-029 Phase 0 backup.
-- =====================================================================================
BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114154319_AddPm029TradingOpportunities'
)
BEGIN
    CREATE TABLE [IndexConstituents] (
        [Id] int NOT NULL IDENTITY,
        [IndexCode] nvarchar(20) NOT NULL,
        [Symbol] nvarchar(20) NOT NULL,
        [CompanyName] nvarchar(200) NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        [Source] nvarchar(300) NOT NULL,
        [SourceAsOfDate] date NOT NULL,
        [VerifiedAtUtc] datetime2 NULL,
        [VerifiedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_IndexConstituents] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114154319_AddPm029TradingOpportunities'
)
BEGIN
    CREATE TABLE [OpportunityRefreshStates] (
        [UserId] nvarchar(450) NOT NULL,
        [LastAttemptAtUtc] datetime2 NULL,
        [LastAttemptStatus] nvarchar(20) NULL,
        [ErrorSummary] nvarchar(1000) NULL,
        [LastSuccessfulSnapshotId] int NULL,
        CONSTRAINT [PK_OpportunityRefreshStates] PRIMARY KEY ([UserId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114154319_AddPm029TradingOpportunities'
)
BEGIN
    CREATE TABLE [OpportunitySnapshots] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ScoringVersion] nvarchar(20) NOT NULL,
        [EvaluationId] nvarchar(64) NULL,
        [ComputedAtUtc] datetime2 NOT NULL,
        [FinalActionComputedAtUtc] datetime2 NULL,
        [DataAsOfSession] date NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [ErrorSummary] nvarchar(1000) NULL,
        [SnapshotJson] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_OpportunitySnapshots] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114154319_AddPm029TradingOpportunities'
)
BEGIN
    CREATE TABLE [SectorBenchmarkMappings] (
        [Id] int NOT NULL IDENTITY,
        [Sector] nvarchar(100) NOT NULL,
        [Market] nvarchar(10) NOT NULL,
        [BenchmarkSymbol] nvarchar(20) NOT NULL,
        [BenchmarkName] nvarchar(200) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [ProviderSectorAliasesJson] nvarchar(max) NOT NULL DEFAULT N'[]',
        [IsActive] bit NOT NULL,
        [Notes] nvarchar(500) NULL,
        [Source] nvarchar(300) NULL,
        [SourceAsOfDate] date NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [UpdatedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_SectorBenchmarkMappings] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114154319_AddPm029TradingOpportunities'
)
BEGIN
    CREATE UNIQUE INDEX [IX_IndexConstituents_IndexCode_Symbol_EffectiveFrom] ON [IndexConstituents] ([IndexCode], [Symbol], [EffectiveFrom]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114154319_AddPm029TradingOpportunities'
)
BEGIN
    CREATE INDEX [IX_OpportunitySnapshots_UserId_ComputedAtUtc] ON [OpportunitySnapshots] ([UserId], [ComputedAtUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114154319_AddPm029TradingOpportunities'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_SectorBenchmarkMappings_Sector_Market] ON [SectorBenchmarkMappings] ([Sector], [Market]) WHERE [IsActive] = 1');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114154319_AddPm029TradingOpportunities'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261114154319_AddPm029TradingOpportunities', N'8.0.10');
END;
GO

COMMIT;
GO

