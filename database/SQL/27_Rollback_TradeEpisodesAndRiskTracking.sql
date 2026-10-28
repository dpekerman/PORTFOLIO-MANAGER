BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028145833_AddTradeEpisodesAndRiskTracking'
)
BEGIN

    IF EXISTS (SELECT 1 FROM [BrokerStopRecords]) OR EXISTS (SELECT 1 FROM [RiskPlans])
       OR EXISTS (SELECT 1 FROM [RiskAuditEvents]) OR EXISTS (SELECT 1 FROM [TradeEpisodes])
       OR EXISTS (SELECT 1 FROM [PortfolioItems] WHERE [TradeEpisodeId] IS NOT NULL)
        THROW 51033, 'PM-033 rollback refused: risk or trade-episode data exists. Restore from the pre-migration backup instead.', 1;
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028145833_AddTradeEpisodesAndRiskTracking'
)
BEGIN
    DROP TABLE [RiskAuditEvents];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028145833_AddTradeEpisodesAndRiskTracking'
)
BEGIN
    DROP TABLE [TradeEpisodes];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028145833_AddTradeEpisodesAndRiskTracking'
)
BEGIN
    DROP TABLE [BrokerStopRecords];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028145833_AddTradeEpisodesAndRiskTracking'
)
BEGIN
    DROP TABLE [RiskPlans];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028145833_AddTradeEpisodesAndRiskTracking'
)
BEGIN
    DROP INDEX [IX_PortfolioItems_TradeEpisodeId] ON [PortfolioItems];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028145833_AddTradeEpisodesAndRiskTracking'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PortfolioItems]') AND [c].[name] = N'TradeEpisodeId');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [PortfolioItems] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [PortfolioItems] DROP COLUMN [TradeEpisodeId];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028145833_AddTradeEpisodesAndRiskTracking'
)
BEGIN
    DELETE FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028145833_AddTradeEpisodesAndRiskTracking';
END;
GO

COMMIT;
GO

