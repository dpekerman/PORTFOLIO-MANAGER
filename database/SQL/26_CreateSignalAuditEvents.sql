-- SignalAuditEvents: historical recommendation audit (Signals Audit dashboard).
-- Mirrors EF migrations 20261024210430_AddSignalAuditEvents and 20261024221705_AddSignalAuditEligibilityAndBasis.
-- Idempotent and safe on a new or existing database: the table is created only when missing, and the
-- later columns are added only when missing, so running it twice (or after the master script) changes nothing.
-- Run against the explicitly selected application database; no USE override.
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.SignalAuditEvents', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SignalAuditEvents] (
        [Id]                  BIGINT IDENTITY(1,1) NOT NULL,
        [UserId]              NVARCHAR(450)  NOT NULL,
        [Symbol]              NVARCHAR(30)   NOT NULL,
        [AnalysisSymbol]      NVARCHAR(30)   NOT NULL,
        [Currency]            NVARCHAR(10)   NULL,
        [SignalType]          NVARCHAR(60)   NOT NULL,
        [SignalCategory]      NVARCHAR(20)   NOT NULL,
        [Direction]           NVARCHAR(10)   NOT NULL,
        [SignalDate]          DATE           NOT NULL,
        [CapturedAtUtc]       DATETIME2(3)   NOT NULL,
        [SignalPrice]         DECIMAL(18,6)  NOT NULL,
        [Score]               DECIMAL(9,2)   NULL,
        [Role]                NVARCHAR(30)   NULL,
        [Source]              NVARCHAR(20)   NOT NULL,
        [Origin]              NVARCHAR(20)   NOT NULL,
        [RuleVersion]         NVARCHAR(40)   NULL,
        [EpisodeKey]          NVARCHAR(120)  NOT NULL,
        [EpisodeEndedDate]    DATE           NULL,
        [ContextJson]         NVARCHAR(MAX)  NULL,
        [MyAction]            NVARCHAR(20)   NOT NULL,
        [MyActionNote]        NVARCHAR(1000) NULL,
        [MyActionUpdatedUtc]  DATETIME2(3)   NULL,
        [LinkedTransactionId] INT            NULL,
        [EligibilityStatus]   NVARCHAR(20)   NOT NULL CONSTRAINT [DF_SignalAuditEvents_EligibilityStatus] DEFAULT (N'Unverified'),
        [BasisClose]          DECIMAL(18,6)  NULL,
        CONSTRAINT [PK_SignalAuditEvents] PRIMARY KEY ([Id])
    );
END;

-- Upgrade path for databases that already have the first version of the table.
IF COL_LENGTH(N'dbo.SignalAuditEvents', N'BasisClose') IS NULL
    ALTER TABLE [dbo].[SignalAuditEvents] ADD [BasisClose] DECIMAL(18,6) NULL;
IF COL_LENGTH(N'dbo.SignalAuditEvents', N'EligibilityStatus') IS NULL
    ALTER TABLE [dbo].[SignalAuditEvents] ADD [EligibilityStatus] NVARCHAR(20) NOT NULL
        CONSTRAINT [DF_SignalAuditEvents_EligibilityStatus] DEFAULT (N'Unverified');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SignalAuditEvents') AND name = N'IX_SignalAuditEvents_UserId_EpisodeKey')
    CREATE UNIQUE INDEX [IX_SignalAuditEvents_UserId_EpisodeKey] ON [dbo].[SignalAuditEvents] ([UserId], [EpisodeKey]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SignalAuditEvents') AND name = N'IX_SignalAuditEvents_UserId_SignalDate')
    CREATE INDEX [IX_SignalAuditEvents_UserId_SignalDate] ON [dbo].[SignalAuditEvents] ([UserId] ASC, [SignalDate] DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SignalAuditEvents') AND name = N'IX_SignalAuditEvents_UserId_Symbol_SignalType')
    CREATE INDEX [IX_SignalAuditEvents_UserId_Symbol_SignalType] ON [dbo].[SignalAuditEvents] ([UserId], [Symbol], [SignalType]);

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20261024210430_AddSignalAuditEvents')
        INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20261024210430_AddSignalAuditEvents', N'8.0.0');
    IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20261024221705_AddSignalAuditEligibilityAndBasis')
        INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20261024221705_AddSignalAuditEligibilityAndBasis', N'8.0.0');
END;
