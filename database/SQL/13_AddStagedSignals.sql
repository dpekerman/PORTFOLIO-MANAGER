-- ============================================================
-- 13_AddStagedSignals.sql
-- RSI Day-over-Day Momentum Tracking
--
-- 1. Creates dbo.StagedSignals  — active tracking memory
-- 2. Alters  dbo.DailySignals   — permanent confirmation snapshot
--
-- ROLLBACK: run 13_Rollback_StagedSignals.sql
-- ============================================================

USE PortfolioManagerDb;
GO

-- ── 1. Create dbo.StagedSignals ─────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StagedSignals')
BEGIN
    CREATE TABLE dbo.StagedSignals
    (
        StagedId        INT IDENTITY(1,1) PRIMARY KEY,

        Symbol          VARCHAR(20)    NOT NULL,
        ScanType        VARCHAR(20)    NOT NULL,   -- Oversold | Overbought

        BasePrice       DECIMAL(18,4)  NOT NULL,
        BaseRsi         DECIMAL(18,4)  NOT NULL,
        BaseHigh        DECIMAL(18,4)  NOT NULL,
        BaseLow         DECIMAL(18,4)  NOT NULL,

        PreviousPrice   DECIMAL(18,4)  NULL,
        PreviousRsi     DECIMAL(18,4)  NULL,

        CurrentPrice    DECIMAL(18,4)  NULL,
        CurrentRsi      DECIMAL(18,4)  NULL,

        RsiDelta1D      DECIMAL(18,4)  NULL,

        ExtremeLow      DECIMAL(18,4)  NULL,
        ExtremeHigh     DECIMAL(18,4)  NULL,

        StagedDate      DATE           NOT NULL,
        LastEvaluatedDate DATE         NULL,

        IsActiveWatch   BIT            NOT NULL DEFAULT 1,

        CreatedAt       DATETIME2      NOT NULL DEFAULT SYSDATETIME(),
        UpdatedAt       DATETIME2      NOT NULL DEFAULT SYSDATETIME()
    );

    -- One active record per symbol + scan type
    SET QUOTED_IDENTIFIER ON;
    CREATE UNIQUE INDEX UX_StagedSignals_Symbol_ScanType_Active
        ON dbo.StagedSignals (Symbol, ScanType)
        WHERE IsActiveWatch = 1;

    CREATE INDEX IX_StagedSignals_Symbol         ON dbo.StagedSignals (Symbol);
    CREATE INDEX IX_StagedSignals_IsActiveWatch  ON dbo.StagedSignals (IsActiveWatch);

    PRINT 'Created dbo.StagedSignals';
END
ELSE
    PRINT 'dbo.StagedSignals already exists — skipped';
GO

-- ── 2. Alter dbo.DailySignals — add confirmation snapshot columns ────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailySignals') AND name = 'TrendShift')
    ALTER TABLE dbo.DailySignals ADD TrendShift VARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailySignals') AND name = 'RsiDelta1D')
    ALTER TABLE dbo.DailySignals ADD RsiDelta1D DECIMAL(18,4) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailySignals') AND name = 'EntryPrice')
    ALTER TABLE dbo.DailySignals ADD EntryPrice DECIMAL(18,4) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailySignals') AND name = 'StopLossPrice')
    ALTER TABLE dbo.DailySignals ADD StopLossPrice DECIMAL(18,4) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailySignals') AND name = 'RiskPerShare')
    ALTER TABLE dbo.DailySignals ADD RiskPerShare DECIMAL(18,4) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailySignals') AND name = 'Sma200')
    ALTER TABLE dbo.DailySignals ADD Sma200 DECIMAL(18,4) NULL;

PRINT 'DailySignals columns added (or already existed)';
GO
