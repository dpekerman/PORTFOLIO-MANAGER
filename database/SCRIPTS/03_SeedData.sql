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
    ('AAPL', 'Watching for oversold entry', 'Strategic'),
    ('MSFT', '', 'Core'),
    ('BNS.TO', 'Bank of Nova Scotia – dividend tracking', 'Core')
) AS source ([Symbol], [Notes], [Role])
ON target.[Symbol] = source.[Symbol]
WHEN NOT MATCHED THEN
    INSERT ([Symbol], [Notes], [Role], [AddedAt])
    VALUES (source.[Symbol], source.[Notes], source.[Role], GETUTCDATE());
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
    ('20260615120000_AddSectorOverride', '8.0.0'),
    ('20260618010551_AddCashOptionAndAdhocTables', '8.0.0'),
    ('20260619000000_AddTransactionFields', '8.0.0'),
    ('20260619000002_RemovePortfolioSymbolUniqueConstraint', '8.0.0'),
    ('20260622183326_AddRoleAndHoldingRole', '8.0.0')
) AS source ([MigrationId], [ProductVersion])
ON target.[MigrationId] = source.[MigrationId]
WHEN NOT MATCHED THEN
    INSERT ([MigrationId], [ProductVersion])
    VALUES (source.[MigrationId], source.[ProductVersion]);
PRINT 'EF Migrations history stamped (8 entries).';
GO

PRINT '';
PRINT '=== Seed data applied successfully ===';
GO

-- ────────────────────────────────────────────────────────────────────────────
-- Seed: Allocation & Risk default data (2026-07-08)
-- Only inserts if the tables are empty to avoid duplicating on re-runs.
-- ────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1
FROM [dbo].[AllocationRiskTargets])
BEGIN
    INSERT INTO [dbo].[AllocationRiskTargets]
        ([Role],[TargetPct],[DisplayOrder])
    VALUES
        ('Core', 40, 1),
        ('Strategic', 15, 2),
        ('Strategic-Income', 5, 3),
        ('Swing', 20, 4),
        ('Speculative', 10, 5),
        ('Options', 5, 6),
        ('Cash', 5, 7);
    PRINT 'AllocationRiskTargets seeded with defaults.';
END
GO

IF NOT EXISTS (SELECT 1
FROM [dbo].[AllocationSectorTargets])
BEGIN
    INSERT INTO [dbo].[AllocationSectorTargets]
        ([Sector],[TargetPct],[DisplayOrder])
    VALUES
        ('Energy', 20, 1),
        ('Industrials', 20, 2),
        ('Financial Services', 15, 3),
        ('Communication Services', 5, 4),
        ('Utilities', 10, 5),
        ('Technology', 10, 6),
        ('Healthcare', 5, 7),
        ('Consumer Defensive', 10, 8),
        ('Materials', 3, 9),
        ('Cash', 2, 10);
    PRINT 'AllocationSectorTargets seeded with defaults.';
END
GO

IF NOT EXISTS (SELECT 1
FROM [dbo].[SinglePositionLimits])
BEGIN
    INSERT INTO [dbo].[SinglePositionLimits]
        ([Role],[TargetPct],[DisplayOrder])
    VALUES
        ('Core', 5, 1),
        ('Strategic', 5, 2),
        ('Strategic-Income', 5, 3),
        ('Swing', 2, 4),
        ('Speculative', 2, 5),
        ('Options', 1, 6);
    PRINT 'SinglePositionLimits seeded with defaults.';
END
GO
