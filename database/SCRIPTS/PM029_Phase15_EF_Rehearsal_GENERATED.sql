BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    DROP INDEX [IX_CashItems_Account_OpeningBalance] ON [CashItems];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    ALTER TABLE [OptionItems] ADD [ContractIdentity] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    ALTER TABLE [OptionItems] ADD [ContractMultiplier] decimal(18,6) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    ALTER TABLE [OptionItems] ADD [CurrencyCode] char(3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    ALTER TABLE [OptionItems] ADD [CurrencyEvidenceSource] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    ALTER TABLE [OptionItems] ADD [CurrencyVerificationStatus] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    ALTER TABLE [OptionItems] ADD [CurrencyVerifiedAtUtc] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    ALTER TABLE [OptionItems] ADD [PositionAsOfUtc] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    ALTER TABLE [OptionItems] ADD [PositionSide] nvarchar(5) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    ALTER TABLE [OptionItems] ADD [PositionSource] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    ALTER TABLE [OptionItems] ADD [PositionState] nvarchar(6) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    ALTER TABLE [OptionItems] ADD [QuoteAsOfUtc] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    ALTER TABLE [OptionItems] ADD [QuoteCurrency] char(3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    ALTER TABLE [OptionItems] ADD [QuoteSource] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    ALTER TABLE [OptionItems] ADD [SignedOpenContracts] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    ALTER TABLE [OptionItems] ADD [TermsSource] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    ALTER TABLE [OptionItems] ADD [TermsVerifiedAtUtc] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    ALTER TABLE [OptionItems] ADD [ValuationAccountId] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    ALTER TABLE [CashItems] ADD [CurrencyCode] char(3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    ALTER TABLE [CashItems] ADD [CurrencyEvidenceSource] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    ALTER TABLE [CashItems] ADD [CurrencyVerificationStatus] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    ALTER TABLE [CashItems] ADD [CurrencyVerifiedAtUtc] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    ALTER TABLE [CashItems] ADD [ValuationAccountId] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    CREATE TABLE [ValuationCurrencyEvidence] (
        [Id] bigint NOT NULL IDENTITY,
        [EntityKind] nvarchar(10) NOT NULL,
        [EntityId] int NOT NULL,
        [ValuationAccountId] nvarchar(100) NULL,
        [CurrencyCode] char(3) NULL,
        [VerificationStatus] nvarchar(20) NOT NULL,
        [EvidenceSource] nvarchar(200) NOT NULL,
        [EffectiveFromUtc] datetime2 NOT NULL,
        [ObservedAtUtc] datetime2 NOT NULL,
        [SupersedesEvidenceId] bigint NULL,
        CONSTRAINT [PK_ValuationCurrencyEvidence] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ValuationEvidence_Dates] CHECK ([ObservedAtUtc] >= [EffectiveFromUtc]),
        CONSTRAINT [CK_ValuationEvidence_EntityKind] CHECK ([EntityKind] IN ('Cash','Option')),
        CONSTRAINT [CK_ValuationEvidence_VerifiedCurrency] CHECK ([VerificationStatus] <> 'Verified' OR ([CurrencyCode] IS NOT NULL AND [CurrencyCode] IN ('CAD','USD') AND [ValuationAccountId] IS NOT NULL AND LTRIM(RTRIM([ValuationAccountId])) <> '' AND LTRIM(RTRIM([EvidenceSource])) <> '')),
        CONSTRAINT [FK_ValuationCurrencyEvidence_ValuationCurrencyEvidence_SupersedesEvidenceId] FOREIGN KEY ([SupersedesEvidenceId]) REFERENCES [ValuationCurrencyEvidence] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_OptionItems_ValuationOpenContract] ON [OptionItems] ([UserId], [ValuationAccountId], [ContractIdentity]) WHERE [PositionState] = ''Open'' AND [ContractIdentity] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    EXEC(N'ALTER TABLE [OptionItems] ADD CONSTRAINT [CK_OptionItems_Multiplier] CHECK ([ContractMultiplier] IS NULL OR [ContractMultiplier] > 0)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    EXEC(N'ALTER TABLE [OptionItems] ADD CONSTRAINT [CK_OptionItems_PositionSide] CHECK ([PositionSide] IS NULL OR [PositionSide] IN (''LONG'',''SHORT''))');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    EXEC(N'ALTER TABLE [OptionItems] ADD CONSTRAINT [CK_OptionItems_PositionState] CHECK ([PositionState] IS NULL OR [PositionState] IN (''Open'',''Closed''))');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    EXEC(N'ALTER TABLE [OptionItems] ADD CONSTRAINT [CK_OptionItems_QuoteCurrency] CHECK ([QuoteCurrency] IS NULL OR ([CurrencyCode] IS NOT NULL AND [QuoteCurrency] = [CurrencyCode]))');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    EXEC(N'ALTER TABLE [OptionItems] ADD CONSTRAINT [CK_OptionItems_SignedPosition] CHECK ([SignedOpenContracts] IS NULL OR ([PositionState] IS NOT NULL AND (([PositionState] = ''Closed'' AND [SignedOpenContracts] = 0) OR ([PositionState] = ''Open'' AND [PositionSide] IS NOT NULL AND (([PositionSide] = ''LONG'' AND [SignedOpenContracts] > 0) OR ([PositionSide] = ''SHORT'' AND [SignedOpenContracts] < 0))))))');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    EXEC(N'ALTER TABLE [OptionItems] ADD CONSTRAINT [CK_OptionItems_VerifiedCurrency] CHECK ([CurrencyVerificationStatus] IS NULL OR [CurrencyVerificationStatus] <> ''Verified'' OR ([CurrencyCode] IS NOT NULL AND [CurrencyCode] IN (''CAD'',''USD'') AND [ValuationAccountId] IS NOT NULL AND LTRIM(RTRIM([ValuationAccountId])) <> '''' AND [CurrencyEvidenceSource] IS NOT NULL AND LTRIM(RTRIM([CurrencyEvidenceSource])) <> '''' AND [CurrencyVerifiedAtUtc] IS NOT NULL))');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CashItems_Account_OpeningBalance] ON [CashItems] ([AccountType], [TransactionDate]) WHERE [CashFlowType] = ''OpeningBalance'' AND [CurrencyCode] IS NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CashItems_Currency_OpeningBalance] ON [CashItems] ([UserId], [ValuationAccountId], [CurrencyCode], [TransactionDate]) WHERE [CashFlowType] = ''OpeningBalance'' AND [CurrencyCode] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    EXEC(N'ALTER TABLE [CashItems] ADD CONSTRAINT [CK_CashItems_VerifiedCurrency] CHECK ([CurrencyVerificationStatus] IS NULL OR [CurrencyVerificationStatus] <> ''Verified'' OR ([CurrencyCode] IS NOT NULL AND [CurrencyCode] IN (''CAD'',''USD'') AND [ValuationAccountId] IS NOT NULL AND LTRIM(RTRIM([ValuationAccountId])) <> '''' AND [CurrencyEvidenceSource] IS NOT NULL AND LTRIM(RTRIM([CurrencyEvidenceSource])) <> '''' AND [CurrencyVerifiedAtUtc] IS NOT NULL))');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    CREATE INDEX [IX_ValuationCurrencyEvidence_EntityKind_EntityId_EffectiveFromUtc] ON [ValuationCurrencyEvidence] ([EntityKind], [EntityId], [EffectiveFromUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    CREATE INDEX [IX_ValuationCurrencyEvidence_SupersedesEvidenceId] ON [ValuationCurrencyEvidence] ([SupersedesEvidenceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115173118_Phase15CurrencyProvenance'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261115173118_Phase15CurrencyProvenance', N'8.0.10');
END;
GO

COMMIT;
GO

