BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006143817_AddAccountTypes'
)
BEGIN
    CREATE TABLE [AccountTypes] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(30) NOT NULL,
        [NormalizedName] nvarchar(30) NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AccountTypes] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006143817_AddAccountTypes'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'NormalizedName', N'Version') AND [object_id] = OBJECT_ID(N'[AccountTypes]'))
        SET IDENTITY_INSERT [AccountTypes] ON;
    EXEC(N'INSERT INTO [AccountTypes] ([Id], [Name], [NormalizedName], [Version])
    VALUES (1, N''TFSA_L_RBC'', N''TFSA_L_RBC'', ''00000000-0000-0000-0000-000000000001''),
    (2, N''TFSA_L_TD'', N''TFSA_L_TD'', ''00000000-0000-0000-0000-000000000002''),
    (3, N''TFSA_D_TD'', N''TFSA_D_TD'', ''00000000-0000-0000-0000-000000000003''),
    (4, N''Margin_L_TD'', N''MARGIN_L_TD'', ''00000000-0000-0000-0000-000000000004''),
    (5, N''Margin_L_RBC'', N''MARGIN_L_RBC'', ''00000000-0000-0000-0000-000000000005''),
    (6, N''Margin_D_TD'', N''MARGIN_D_TD'', ''00000000-0000-0000-0000-000000000006''),
    (7, N''Corp_TD'', N''CORP_TD'', ''00000000-0000-0000-0000-000000000007'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'NormalizedName', N'Version') AND [object_id] = OBJECT_ID(N'[AccountTypes]'))
        SET IDENTITY_INSERT [AccountTypes] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006143817_AddAccountTypes'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AccountTypes_NormalizedName] ON [AccountTypes] ([NormalizedName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006143817_AddAccountTypes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006143817_AddAccountTypes', N'8.0.10');
END;
GO

COMMIT;
GO

