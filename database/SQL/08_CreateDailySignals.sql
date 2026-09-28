-- ============================================================
-- SCRIPTS/08_CreateDailySignals.sql
-- Creates the DailySignals table for the EOD Signals Dashboard.
-- Safe to re-run: guarded by IF NOT EXISTS checks.
-- ============================================================

USE PortfolioManagerDb;
GO

-- ────────────────────────────────────────────────────────────────────────────
-- TABLE: DailySignals
-- Stores all persisted EOD CONFIRM / Confirmed signals for full history
-- tracking on the EOD Signals Dashboard. Populated automatically when the
-- EOD confirmation window closes each trading day.
-- ────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[DailySignals]') AND type = N'U'
)
BEGIN
    CREATE TABLE [dbo].[DailySignals]
    (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [Symbol] NVARCHAR(20) NOT NULL,
        [CompanyName] NVARCHAR(200) NOT NULL DEFAULT '',
        [ScanType] NVARCHAR(20) NOT NULL,
        -- Oversold | Overbought
        [SignalType] NVARCHAR(30) NOT NULL,
        -- EodConfirm | Confirmed | EarlyWarning
        [Rsi] DECIMAL(7,4) NOT NULL,
        [Price] DECIMAL(18,4) NOT NULL,
        [TriggerDetails] NVARCHAR(1000) NOT NULL DEFAULT '',
        [SignalDate] NVARCHAR(10) NOT NULL,
        -- yyyy-MM-dd (ET)
        [RecordedAt] DATETIME2 NOT NULL,
        [RuleVersion] NVARCHAR(20) NOT NULL DEFAULT 'Legacy',
        [SignalState] NVARCHAR(30) NOT NULL DEFAULT 'Active',
        -- Active | FollowThrough | Invalidated | Expired | Reversed
        [Sector] NVARCHAR(100) NOT NULL DEFAULT '',
        [ReversalProbability] NVARCHAR(20) NOT NULL DEFAULT '',
        [VolumeSignal] NVARCHAR(30) NOT NULL DEFAULT '',
        [Notes] NVARCHAR(MAX) NULL,
        [UpdatedAt] DATETIME2 NULL,

        CONSTRAINT [PK_DailySignals] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE NONCLUSTERED INDEX [IX_DailySignals_Symbol]
        ON [dbo].[DailySignals] ([Symbol] ASC);

    CREATE NONCLUSTERED INDEX [IX_DailySignals_SignalDate]
        ON [dbo].[DailySignals] ([SignalDate] ASC);

    CREATE NONCLUSTERED INDEX [IX_DailySignals_Symbol_SignalDate]
        ON [dbo].[DailySignals] ([Symbol] ASC, [SignalDate] ASC);

    PRINT 'Table DailySignals created with indexes.';
END
ELSE
BEGIN
    PRINT 'Table DailySignals already exists – skipping.';
END
GO
