-- =============================================================================
-- 09_SetStrategicIncomeRole.sql
-- Sets the HoldingRole to 'Strategic-Income' for specific tickers:
-- BANK.TO, SIXY.TO, T.TO, HMAX.TO
-- Run this after applying the 20260707000001_AddWatchlistFavorite migration.
-- =============================================================================

-- Update PortfolioItems for the specified tickers
UPDATE PortfolioItems
SET HoldingRole = 'Strategic-Income'
WHERE Symbol IN ('BANK.TO', 'SIXY.TO', 'T.TO', 'HMAX.TO')
    AND (TransactionType IS NULL OR TransactionType = 'OPEN');

-- Verify the updates
SELECT Symbol, AccountType, HoldingRole, TransactionType
FROM PortfolioItems
WHERE Symbol IN ('BANK.TO', 'SIXY.TO', 'T.TO', 'HMAX.TO')
ORDER BY Symbol, AccountType;

PRINT 'Strategic-Income role assignment complete.';
