-- ============================================================
-- SCRIPTS/03_SeedData.sql
-- Optional demo data for first-launch / development.
-- Safe to re-run: uses MERGE to avoid duplicates.
-- ============================================================

USE PortfolioManagerDb;
GO

-- ── Demo Portfolio Positions ──────────────────────────────────────────────────
MERGE [dbo].[PortfolioItems] AS target
USING (
    VALUES
    ('RY.TO', 'Royal Bank of Canada', 30.0000, 135.5000, 'Financial Services', 'Banks – Diversified', 0, NULL, 0),
    ('TD.TO', 'Toronto-Dominion Bank', 25.0000, 82.2500, 'Financial Services', 'Banks – Diversified', 0, NULL, 0),
    ('ENB.TO', 'Enbridge Inc.', 40.0000, 48.7500, 'Energy', 'Oil & Gas Midstream', 0, NULL, 0),
    ('CNR.TO', 'Canadian National Railway Co', 15.0000, 162.0000, 'Industrials', 'Railroads', 0, NULL, 0),
    ('SHOP.TO', 'Shopify Inc.', 10.0000, 95.4000, 'Technology', 'Software', 0, NULL, 0)
) AS source (
    [Symbol], [CompanyName], [Shares], [AverageCostBasis],
    [Sector], [Industry], [IsManual], [ManualMarketValue], [SectorIsOverridden]
)
ON target.[Symbol] = source.[Symbol]
WHEN NOT MATCHED THEN
    INSERT (
        [Symbol], [CompanyName], [Shares], [AverageCostBasis],
        [Sector], [Industry], [IsManual], [ManualMarketValue], [SectorIsOverridden], [AddedAt]
    )
    VALUES (
        source.[Symbol], source.[CompanyName], source.[Shares], source.[AverageCostBasis],
        source.[Sector], source.[Industry], source.[IsManual], source.[ManualMarketValue],
        source.[SectorIsOverridden], GETUTCDATE()
    );
PRINT 'Demo PortfolioItems applied (5 rows).';
GO

-- ── Demo Watchlist ────────────────────────────────────────────────────────────
MERGE [dbo].[WatchlistItems] AS target
USING (
    VALUES
    ('AAPL', 'Watching for oversold entry'),
    ('MSFT', ''),
    ('BNS.TO', 'Bank of Nova Scotia – dividend tracking')
) AS source ([Symbol], [Notes])
ON target.[Symbol] = source.[Symbol]
WHEN NOT MATCHED THEN
    INSERT ([Symbol], [Notes], [AddedAt])
    VALUES (source.[Symbol], source.[Notes], GETUTCDATE());
PRINT 'Demo WatchlistItems applied (3 rows).';
GO

-- ── EF Core Migrations History ────────────────────────────────────────────────
-- Inserts migration records so EF doesn't try to re-run them after a manual setup.
MERGE [dbo].[__EFMigrationsHistory] AS target
USING (
    VALUES
    ('20260611201226_InitialCreate', '8.0.0'),
    ('20260611235305_AddWatchlistAndSector', '8.0.0'),
    ('20260612030112_AddManualPosition', '8.0.0'),
    ('20260615120000_AddSectorOverride', '8.0.0')
) AS source ([MigrationId], [ProductVersion])
ON target.[MigrationId] = source.[MigrationId]
WHEN NOT MATCHED THEN
    INSERT ([MigrationId], [ProductVersion])
    VALUES (source.[MigrationId], source.[ProductVersion]);
PRINT 'EF Migrations history stamped (4 entries).';
GO

PRINT '';
PRINT '=== Seed data applied successfully ===';
GO
