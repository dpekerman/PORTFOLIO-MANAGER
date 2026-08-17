-- ============================================================
-- SCRIPTS/07_AdhocAnalysisSession.sql
-- Creates the AdhocAnalysisSessions table to persist ad-hoc
-- symbol analysis state across page navigations.
-- Safe to re-run: guarded by IF NOT EXISTS checks.
-- ============================================================

USE PortfolioManagerDb;
GO

-- ────────────────────────────────────────────────────────────────────────────
-- TABLE: AdhocAnalysisSessions
-- Stores the most-recent ad-hoc symbol analysis session per user/client.
-- Each time the user navigates away or analyzes symbols the row is upserted.
-- Columns:
--   Id            : surrogate PK
--   SessionKey    : logical key – 'default' for single-user app, or a user id
--   Symbols       : JSON array of ticker strings  e.g. '["RY.TO","AAPL"]'
--   ResultsJson   : full JSON array of RsiScanResult objects (may be NULL when
--                   the user entered symbols but did not yet run analysis)
--   OversoldThreshold  : oversold RSI threshold used for the last analysis
--   OverboughtThreshold: overbought RSI threshold used for the last analysis
--   LogicMode     : "Legacy" | "Enhanced"
--   CreatedAt     : timestamp of first creation
--   UpdatedAt     : timestamp of last save (used to restore the most recent)
-- ────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1
FROM sys.objects
WHERE object_id = OBJECT_ID(N'[dbo].[AdhocAnalysisSessions]') AND type = N'U'
)
BEGIN
    CREATE TABLE [dbo].[AdhocAnalysisSessions]
    (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [SessionKey] NVARCHAR(100) NOT NULL DEFAULT 'default',
        [Symbols] NVARCHAR(MAX) NOT NULL DEFAULT '[]',
        -- JSON array
        [ResultsJson] NVARCHAR(MAX) NULL,
        -- JSON array of RsiScanResult
        [OversoldThreshold] DECIMAL(5,2) NOT NULL DEFAULT 30.00,
        [OverboughtThreshold] DECIMAL(5,2) NOT NULL DEFAULT 75.00,
        [LogicMode] NVARCHAR(20) NOT NULL DEFAULT 'Legacy',
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT [PK_AdhocAnalysisSessions] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    -- Index for fast lookup by session key (most queries will be
    -- SELECT TOP 1 ... WHERE SessionKey = 'default' ORDER BY UpdatedAt DESC)
    CREATE NONCLUSTERED INDEX [IX_AdhocAnalysisSessions_SessionKey_UpdatedAt]
        ON [dbo].[AdhocAnalysisSessions] ([SessionKey] ASC, [UpdatedAt] DESC);

    PRINT 'Table AdhocAnalysisSessions created.';
END
ELSE
BEGIN
    PRINT 'Table AdhocAnalysisSessions already exists – checking for missing columns...';

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[AdhocAnalysisSessions]') AND name = N'OversoldThreshold')
    BEGIN
        ALTER TABLE [dbo].[AdhocAnalysisSessions] ADD [OversoldThreshold] DECIMAL(5,2) NOT NULL DEFAULT 30.00;
        PRINT '  + Column OversoldThreshold added.';
    END

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[AdhocAnalysisSessions]') AND name = N'OverboughtThreshold')
    BEGIN
        ALTER TABLE [dbo].[AdhocAnalysisSessions] ADD [OverboughtThreshold] DECIMAL(5,2) NOT NULL DEFAULT 75.00;
        PRINT '  + Column OverboughtThreshold added.';
    END

    IF NOT EXISTS (SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[AdhocAnalysisSessions]') AND name = N'LogicMode')
    BEGIN
        ALTER TABLE [dbo].[AdhocAnalysisSessions] ADD [LogicMode] NVARCHAR(20) NOT NULL DEFAULT 'Legacy';
        PRINT '  + Column LogicMode added.';
    END
END
GO

-- ────────────────────────────────────────────────────────────────────────────
-- STORED PROCEDURE: usp_SaveAdhocSession
-- Upserts the latest ad-hoc session for a given SessionKey.
-- Called by the API whenever the user navigates away or triggers analysis.
-- ────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'[dbo].[usp_SaveAdhocSession]', N'P') IS NOT NULL
    DROP PROCEDURE [dbo].[usp_SaveAdhocSession];
GO

CREATE PROCEDURE [dbo].[usp_SaveAdhocSession]
    @SessionKey          NVARCHAR(100) = 'default',
    @Symbols             NVARCHAR(MAX),
    @ResultsJson         NVARCHAR(MAX) = NULL,
    @OversoldThreshold   DECIMAL(5,2)  = 30.00,
    @OverboughtThreshold DECIMAL(5,2)  = 75.00,
    @LogicMode           NVARCHAR(20)  = 'Legacy'
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1
    FROM [dbo].[AdhocAnalysisSessions]
    WHERE [SessionKey] = @SessionKey)
    BEGIN
        UPDATE [dbo].[AdhocAnalysisSessions]
        SET    [Symbols]             = @Symbols,
               [ResultsJson]        = @ResultsJson,
               [OversoldThreshold]  = @OversoldThreshold,
               [OverboughtThreshold]= @OverboughtThreshold,
               [LogicMode]          = @LogicMode,
               [UpdatedAt]          = GETUTCDATE()
        WHERE  [SessionKey]         = @SessionKey;
    END
    ELSE
    BEGIN
        INSERT INTO [dbo].[AdhocAnalysisSessions]
            ([SessionKey], [Symbols], [ResultsJson], [OversoldThreshold], [OverboughtThreshold], [LogicMode])
        VALUES
            (@SessionKey, @Symbols, @ResultsJson, @OversoldThreshold, @OverboughtThreshold, @LogicMode);
    END
END
GO

-- ────────────────────────────────────────────────────────────────────────────
-- STORED PROCEDURE: usp_LoadAdhocSession
-- Returns the most recent session for the given SessionKey.
-- ────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'[dbo].[usp_LoadAdhocSession]', N'P') IS NOT NULL
    DROP PROCEDURE [dbo].[usp_LoadAdhocSession];
GO

CREATE PROCEDURE [dbo].[usp_LoadAdhocSession]
    @SessionKey NVARCHAR(100) = 'default'
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        [Id],
        [SessionKey],
        [Symbols],
        [ResultsJson],
        [OversoldThreshold],
        [OverboughtThreshold],
        [LogicMode],
        [UpdatedAt]
    FROM [dbo].[AdhocAnalysisSessions]
    WHERE  [SessionKey] = @SessionKey
    ORDER  BY [UpdatedAt] DESC;
END
GO

PRINT 'Script 07_AdhocAnalysisSession.sql completed successfully.';
GO
