-- Azure SQL Schema Fix Script
-- Run this ONCE in Azure portal Query Editor before running the data migration.
-- All statements use IF NOT EXISTS so it is safe to re-run.

-- WatchlistItems
IF NOT EXISTS (SELECT 1
FROM sys.columns
WHERE object_id = OBJECT_ID('WatchlistItems') AND name = 'IsFavorite')
    ALTER TABLE [WatchlistItems] ADD [IsFavorite] BIT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1
FROM sys.columns
WHERE object_id = OBJECT_ID('WatchlistItems') AND name = 'Role')
    ALTER TABLE [WatchlistItems] ADD [Role] NVARCHAR(20) NOT NULL DEFAULT N'Strategic';

IF NOT EXISTS (SELECT 1
FROM sys.columns
WHERE object_id = OBJECT_ID('WatchlistItems') AND name = 'UserId')
    ALTER TABLE [WatchlistItems] ADD [UserId] NVARCHAR(450) NULL;

-- PortfolioItems
IF NOT EXISTS (SELECT 1
FROM sys.columns
WHERE object_id = OBJECT_ID('PortfolioItems') AND name = 'DecisionSource')
    ALTER TABLE [PortfolioItems] ADD [DecisionSource] NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1
FROM sys.columns
WHERE object_id = OBJECT_ID('PortfolioItems') AND name = 'DecisionSourceClosed')
    ALTER TABLE [PortfolioItems] ADD [DecisionSourceClosed] NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1
FROM sys.columns
WHERE object_id = OBJECT_ID('PortfolioItems') AND name = 'HoldingRole')
    ALTER TABLE [PortfolioItems] ADD [HoldingRole] NVARCHAR(20) NULL;

IF NOT EXISTS (SELECT 1
FROM sys.columns
WHERE object_id = OBJECT_ID('PortfolioItems') AND name = 'UserId')
    ALTER TABLE [PortfolioItems] ADD [UserId] NVARCHAR(450) NULL;

IF NOT EXISTS (SELECT 1
FROM sys.columns
WHERE object_id = OBJECT_ID('PortfolioItems') AND name = 'Notes')
    ALTER TABLE [PortfolioItems] ADD [Notes] NVARCHAR(MAX) NULL;

-- OptionItems
IF NOT EXISTS (SELECT 1
FROM sys.columns
WHERE object_id = OBJECT_ID('OptionItems') AND name = 'DecisionSource')
    ALTER TABLE [OptionItems] ADD [DecisionSource] NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1
FROM sys.columns
WHERE object_id = OBJECT_ID('OptionItems') AND name = 'DecisionSourceClosed')
    ALTER TABLE [OptionItems] ADD [DecisionSourceClosed] NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1
FROM sys.columns
WHERE object_id = OBJECT_ID('OptionItems') AND name = 'Notes')
    ALTER TABLE [OptionItems] ADD [Notes] NVARCHAR(MAX) NULL;

IF NOT EXISTS (SELECT 1
FROM sys.columns
WHERE object_id = OBJECT_ID('OptionItems') AND name = 'UserId')
    ALTER TABLE [OptionItems] ADD [UserId] NVARCHAR(450) NULL;

-- CashItems
IF NOT EXISTS (SELECT 1
FROM sys.columns
WHERE object_id = OBJECT_ID('CashItems') AND name = 'AccountType')
    ALTER TABLE [CashItems] ADD [AccountType] NVARCHAR(30) NULL;

IF NOT EXISTS (SELECT 1
FROM sys.columns
WHERE object_id = OBJECT_ID('CashItems') AND name = 'TransactionDate')
    ALTER TABLE [CashItems] ADD [TransactionDate] DATETIME2 NULL;

IF NOT EXISTS (SELECT 1
FROM sys.columns
WHERE object_id = OBJECT_ID('CashItems') AND name = 'UserId')
    ALTER TABLE [CashItems] ADD [UserId] NVARCHAR(450) NULL;

-- Verify: show all columns for the affected tables
SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('WatchlistItems', 'PortfolioItems', 'OptionItems', 'CashItems')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
