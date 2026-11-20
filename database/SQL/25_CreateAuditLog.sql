-- Audit log table. Standalone/idempotent; mirrors EF migration 20261015173504_AddAuditLog.
-- Idempotent. Run against the explicitly selected application database; no USE override.
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.AuditLogs', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AuditLogs] (
        [Id]            BIGINT IDENTITY(1,1) NOT NULL,
        [OccurredAtUtc] DATETIME2(3)  NOT NULL,
        [UserId]        NVARCHAR(450) NULL,
        [UserName]      NVARCHAR(256) NULL,
        [Source]        NVARCHAR(20)  NOT NULL,
        [Category]      NVARCHAR(20)  NOT NULL,
        [Action]        NVARCHAR(20)  NOT NULL,
        [EntityType]    NVARCHAR(60)  NOT NULL,
        [EntityId]      NVARCHAR(64)  NULL,
        [Symbol]        NVARCHAR(30)  NULL,
        [AccountType]   NVARCHAR(120) NULL,
        [Summary]       NVARCHAR(500) NOT NULL,
        [ChangesJson]   NVARCHAR(MAX) NULL,
        [CorrelationId] NVARCHAR(64)  NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AuditLogs') AND name = N'IX_AuditLogs_OccurredAtUtc')
    CREATE INDEX [IX_AuditLogs_OccurredAtUtc] ON [dbo].[AuditLogs] ([OccurredAtUtc] DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AuditLogs') AND name = N'IX_AuditLogs_UserId_OccurredAtUtc')
    CREATE INDEX [IX_AuditLogs_UserId_OccurredAtUtc] ON [dbo].[AuditLogs] ([UserId] ASC, [OccurredAtUtc] DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AuditLogs') AND name = N'IX_AuditLogs_Category_OccurredAtUtc')
    CREATE INDEX [IX_AuditLogs_Category_OccurredAtUtc] ON [dbo].[AuditLogs] ([Category] ASC, [OccurredAtUtc] DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AuditLogs') AND name = N'IX_AuditLogs_Symbol_OccurredAtUtc')
    CREATE INDEX [IX_AuditLogs_Symbol_OccurredAtUtc] ON [dbo].[AuditLogs] ([Symbol] ASC, [OccurredAtUtc] DESC);

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20261015173504_AddAuditLog')
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20261015173504_AddAuditLog', N'8.0.0');
