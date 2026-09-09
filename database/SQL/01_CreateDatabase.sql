-- ============================================================
-- SCRIPTS/01_CreateDatabase.sql
-- Creates the PortfolioManagerDb database if it doesn't exist.
-- Safe to re-run.
-- ============================================================

USE master;
GO

IF NOT EXISTS (
    SELECT name FROM sys.databases WHERE name = N'PortfolioManagerDb'
)
BEGIN
    CREATE DATABASE PortfolioManagerDb
        COLLATE SQL_Latin1_General_CP1_CI_AS;
    PRINT 'Database PortfolioManagerDb created.';
END
ELSE
BEGIN
    PRINT 'Database PortfolioManagerDb already exists - skipping.';
END
GO
