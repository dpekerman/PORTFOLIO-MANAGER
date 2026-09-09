-- ============================================================
-- SCRIPTS/14_AddFibonacciToDailySignals.sql
-- Adds optional Fibonacci snapshot columns to DailySignals.
-- These columns store the Fib context at signal generation time
-- for historical reference (informational only, NOT a promotion gate).
-- Safe to re-run: guarded by IF NOT EXISTS checks.
-- ============================================================

USE PortfolioManagerDb;
GO

-- Fib61_8AtSignal: Fibonacci 61.8% level at the moment the signal was generated
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[DailySignals]') AND name = N'Fib61_8AtSignal'
)
BEGIN
    ALTER TABLE [dbo].[DailySignals]
    ADD [Fib61_8AtSignal] DECIMAL(18,4) NULL;
    PRINT 'Added column Fib61_8AtSignal to DailySignals.';
END
GO

-- FibZoneAtSignal: Fibonacci zone at the moment the signal was generated
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[DailySignals]') AND name = N'FibZoneAtSignal'
)
BEGIN
    ALTER TABLE [dbo].[DailySignals]
    ADD [FibZoneAtSignal] NVARCHAR(30) NULL;
    PRINT 'Added column FibZoneAtSignal to DailySignals.';
END
GO

-- FibStatusAtSignal: Fibonacci status (vs 61.8%) at the moment the signal was generated
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[DailySignals]') AND name = N'FibStatusAtSignal'
)
BEGIN
    ALTER TABLE [dbo].[DailySignals]
    ADD [FibStatusAtSignal] NVARCHAR(30) NULL;
    PRINT 'Added column FibStatusAtSignal to DailySignals.';
END
GO

PRINT 'Script 14_AddFibonacciToDailySignals completed.';
GO
