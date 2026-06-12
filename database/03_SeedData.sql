-- ============================================================
-- 03_SeedData.sql
-- Optional demo portfolio entries so the dashboard is not empty
-- on first launch (before Yahoo Finance data has been fetched).
-- Safe to re-run (uses MERGE to avoid duplicates).
-- ============================================================

USE PortfolioManagerDb;
GO

MERGE [dbo].[PortfolioItems] AS target
USING (
    VALUES
    ('AAPL', 'Apple Inc.', 10.0000, 172.5000),
    ('MSFT', 'Microsoft Corporation', 5.0000, 310.2500),
    ('GOOGL', 'Alphabet Inc. Class A', 2.0000, 125.7500),
    ('AMZN', 'Amazon.com Inc.', 3.0000, 178.0000),
    ('NVDA', 'NVIDIA Corporation', 8.0000, 480.5000)
) AS source ([Symbol], [CompanyName], [Shares], [AverageCostBasis])
ON target.[Symbol] = source.[Symbol]
WHEN NOT MATCHED THEN
    INSERT ([Symbol], [CompanyName], [Shares], [AverageCostBasis], [AddedAt])
    VALUES (source.[Symbol], source.[CompanyName], source.[Shares], source.[AverageCostBasis], GETUTCDATE());
GO

PRINT 'Seed data applied (5 demo portfolio items).';
GO
