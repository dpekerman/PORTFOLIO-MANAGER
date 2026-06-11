-- ============================================================
-- 04_DropAll.sql
-- Drops all objects created by this project.
-- USE WITH CAUTION - for development/reset only.
-- ============================================================

USE master;
GO

-- Close all connections first
IF EXISTS (
    SELECT name FROM sys.databases WHERE name = N'PortfolioManagerDb'
)
BEGIN
    ALTER DATABASE PortfolioManagerDb SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE PortfolioManagerDb;
    PRINT 'Database PortfolioManagerDb dropped.';
END
ELSE
BEGIN
    PRINT 'Database PortfolioManagerDb does not exist.';
END
GO
