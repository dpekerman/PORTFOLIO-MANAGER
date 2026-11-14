BEGIN TRANSACTION;
GO

DROP INDEX [IX_RiskPlans_UserId_AccountType_Symbol] ON [RiskPlans];
GO

ALTER TABLE [RiskAuditEvents] ADD [TradeEpisodeId] uniqueidentifier NULL;
GO

CREATE UNIQUE INDEX [IX_RiskPlans_UserId_AccountType_Symbol_TradeEpisodeId] ON [RiskPlans] ([UserId], [AccountType], [Symbol], [TradeEpisodeId]);
GO

CREATE INDEX [IX_RiskAuditEvents_UserId_TradeEpisodeId_OccurredAtUtc] ON [RiskAuditEvents] ([UserId], [TradeEpisodeId], [OccurredAtUtc]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261028213913_CorrectPm033EpisodeIntegrity', N'8.0.10');
GO

COMMIT;
GO

