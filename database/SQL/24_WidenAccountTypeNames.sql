BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006150738_WidenAccountTypeNames'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PortfolioItems]') AND [c].[name] = N'AccountType');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [PortfolioItems] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [PortfolioItems] ALTER COLUMN [AccountType] nvarchar(120) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006150738_WidenAccountTypeNames'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OptionItems]') AND [c].[name] = N'AccountType');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [OptionItems] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [OptionItems] ALTER COLUMN [AccountType] nvarchar(120) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006150738_WidenAccountTypeNames'
)
BEGIN
    DROP INDEX [IX_CashItems_Account_OpeningBalance] ON [CashItems];
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CashItems]') AND [c].[name] = N'AccountType');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [CashItems] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [CashItems] ALTER COLUMN [AccountType] nvarchar(120) NULL;
    EXEC(N'CREATE UNIQUE INDEX [IX_CashItems_Account_OpeningBalance] ON [CashItems] ([AccountType], [TransactionDate]) WHERE [CashFlowType] = ''OpeningBalance''');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006150738_WidenAccountTypeNames'
)
BEGIN
    DROP INDEX [IX_AccountTypes_NormalizedName] ON [AccountTypes];
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AccountTypes]') AND [c].[name] = N'NormalizedName');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [AccountTypes] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [AccountTypes] ALTER COLUMN [NormalizedName] nvarchar(120) NOT NULL;
    CREATE UNIQUE INDEX [IX_AccountTypes_NormalizedName] ON [AccountTypes] ([NormalizedName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006150738_WidenAccountTypeNames'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AccountTypes]') AND [c].[name] = N'Name');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [AccountTypes] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [AccountTypes] ALTER COLUMN [Name] nvarchar(120) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006150738_WidenAccountTypeNames'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006150738_WidenAccountTypeNames', N'8.0.10');
END;
GO

COMMIT;
GO

