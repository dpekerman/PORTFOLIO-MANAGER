-- ============================================================
-- 01_CreateDatabase.sql
-- Creates the PortfolioManagerDb database if it does not exist
-- Run as: sa or a login with dbcreator role
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
    PRINT 'Database PortfolioManagerDb already exists - skipping creation.';
END
GO
