-- ============================================================
-- 13_Rollback_StagedSignals.sql
-- Rolls back all changes from 13_AddStagedSignals.sql
-- ============================================================

USE PortfolioManagerDb;
GO

-- Remove DailySignals columns
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailySignals') AND name = 'Sma200')
    ALTER TABLE dbo.DailySignals DROP COLUMN Sma200;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailySignals') AND name = 'RiskPerShare')
    ALTER TABLE dbo.DailySignals DROP COLUMN RiskPerShare;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailySignals') AND name = 'StopLossPrice')
    ALTER TABLE dbo.DailySignals DROP COLUMN StopLossPrice;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailySignals') AND name = 'EntryPrice')
    ALTER TABLE dbo.DailySignals DROP COLUMN EntryPrice;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailySignals') AND name = 'RsiDelta1D')
    ALTER TABLE dbo.DailySignals DROP COLUMN RsiDelta1D;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailySignals') AND name = 'TrendShift')
    ALTER TABLE dbo.DailySignals DROP COLUMN TrendShift;

PRINT 'Rolled back DailySignals columns';

-- Drop StagedSignals table
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StagedSignals')
BEGIN
    DROP TABLE dbo.StagedSignals;
    PRINT 'Dropped dbo.StagedSignals';
END
GO
