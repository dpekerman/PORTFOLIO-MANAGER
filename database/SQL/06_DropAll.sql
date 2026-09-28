-- ============================================================
-- SCRIPTS/06_DropAll.sql
-- Drops the ENTIRE PortfolioManagerDb database.
-- USE WITH EXTREME CAUTION – all data will be permanently lost.
-- Run 01_CreateDatabase.sql + 02_CreateTables.sql + 03_SeedData.sql
-- to recreate from scratch.
-- ============================================================

USE master;
GO

IF EXISTS (
    SELECT name FROM sys.databases WHERE name = N'PortfolioManagerDb'
)
BEGIN
    -- Kick all active connections before dropping
    ALTER DATABASE PortfolioManagerDb SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE PortfolioManagerDb;
    PRINT 'Database PortfolioManagerDb dropped successfully.';
END
ELSE
BEGIN
    PRINT 'Database PortfolioManagerDb does not exist – nothing to drop.';
END
GO
