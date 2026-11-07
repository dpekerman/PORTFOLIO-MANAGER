-- REVIEW ONLY. This is the supported rollback path, not an unguarded EF database downgrade.
-- Never execute against PortfolioManagerLocal without separate approval.
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NULL
        THROW 51033, 'Corrective rollback refused: unknown migration history.', 1;
    IF EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory
        WHERE MigrationId > N'20261028213913_CorrectPm033EpisodeIntegrity')
        THROW 51033, 'Corrective rollback refused: later migrations exist.', 1;
    IF EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory
        WHERE MigrationId = N'20261028213913_CorrectPm033EpisodeIntegrity')
    BEGIN
        IF COL_LENGTH(N'dbo.RiskAuditEvents', N'TradeEpisodeId') IS NULL
            OR OBJECT_ID(N'dbo.BrokerStopRecords') IS NULL OR OBJECT_ID(N'dbo.RiskPlans') IS NULL
            OR OBJECT_ID(N'dbo.TradeEpisodes') IS NULL
            THROW 51033, 'Corrective rollback refused: incomplete schema.', 1;
        EXEC sys.sp_executesql N'
        IF EXISTS (SELECT 1 FROM dbo.RiskAuditEvents) OR EXISTS (SELECT 1 FROM dbo.RiskPlans)
            OR EXISTS (SELECT 1 FROM dbo.BrokerStopRecords) OR EXISTS (SELECT 1 FROM dbo.TradeEpisodes)
            OR EXISTS (SELECT 1 FROM dbo.PortfolioItems WHERE TradeEpisodeId IS NOT NULL)
            THROW 51033, ''Corrective rollback refused: populated trade/risk history. Use verified database recovery.'', 1;
        DROP INDEX IX_RiskPlans_UserId_AccountType_Symbol_TradeEpisodeId ON dbo.RiskPlans;
        DROP INDEX IX_RiskAuditEvents_UserId_TradeEpisodeId_OccurredAtUtc ON dbo.RiskAuditEvents;
        ALTER TABLE dbo.RiskAuditEvents DROP COLUMN TradeEpisodeId;
        CREATE UNIQUE INDEX IX_RiskPlans_UserId_AccountType_Symbol ON dbo.RiskPlans(UserId, AccountType, Symbol)
            WHERE UserId IS NOT NULL;
        DELETE FROM dbo.__EFMigrationsHistory
            WHERE MigrationId = N''20261028213913_CorrectPm033EpisodeIntegrity'';';
    END;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
