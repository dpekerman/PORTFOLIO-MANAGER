-- REVIEW ONLY: execute solely against an explicitly authorized isolated database.
-- One batch: a client continuing after THROW cannot reach a later destructive batch.
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NULL
        THROW 51033, 'PM-033 rollback refused: unknown migration history.', 1;
    IF EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory
               WHERE MigrationId > N'20261028145833_AddTradeEpisodesAndRiskTracking')
        THROW 51033, 'PM-033 rollback refused: later migrations exist.', 1;
    IF EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory
               WHERE MigrationId = N'20261028145833_AddTradeEpisodesAndRiskTracking')
    BEGIN
        IF OBJECT_ID(N'dbo.TradeEpisodes') IS NULL OR OBJECT_ID(N'dbo.RiskPlans') IS NULL
            OR OBJECT_ID(N'dbo.BrokerStopRecords') IS NULL OR OBJECT_ID(N'dbo.RiskAuditEvents') IS NULL
            THROW 51033, 'PM-033 rollback refused: incomplete schema.', 1;
        EXEC sys.sp_executesql N'
        IF EXISTS (SELECT 1 FROM dbo.BrokerStopRecords) OR EXISTS (SELECT 1 FROM dbo.RiskPlans)
            OR EXISTS (SELECT 1 FROM dbo.RiskAuditEvents) OR EXISTS (SELECT 1 FROM dbo.TradeEpisodes)
            OR EXISTS (SELECT 1 FROM dbo.PortfolioItems WHERE TradeEpisodeId IS NOT NULL)
            THROW 51033, ''PM-033 rollback refused: populated trade/risk history. Use verified database recovery.'', 1;
        DROP TABLE dbo.RiskAuditEvents;
        DROP TABLE dbo.BrokerStopRecords;
        DROP TABLE dbo.RiskPlans;
        DROP TABLE dbo.TradeEpisodes;
        DROP INDEX IX_PortfolioItems_TradeEpisodeId ON dbo.PortfolioItems;
        ALTER TABLE dbo.PortfolioItems DROP COLUMN TradeEpisodeId;
        DELETE FROM dbo.__EFMigrationsHistory
        WHERE MigrationId = N''20261028145833_AddTradeEpisodesAndRiskTracking'';';
    END;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
