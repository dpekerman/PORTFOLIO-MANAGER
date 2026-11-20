SET NOCOUNT ON;
IF COL_LENGTH('dbo.CashItems', 'CurrencyCode') IS NOT NULL
    THROW 51002, 'Currency schema exists; use the evidence-aware classifier instead.', 1;
-- Existing schema has no authoritative cash currency or option terms fields.
-- Counts are candidate classifications, not currency inference or backfill.
SELECT 'UnknownCurrency' AS Classification, COUNT(*) AS CashCandidates,
    SUM(CASE WHEN UserId IS NULL THEN 1 ELSE 0 END) AS SharedCandidates,
    SUM(CASE WHEN CashFlowType = 'OpeningBalance' THEN 1 ELSE 0 END) AS OpeningCandidates,
    SUM(CASE WHEN SourceType IS NOT NULL THEN 1 ELSE 0 END) AS TradeLinkedCandidates
FROM dbo.CashItems;
SELECT 'RequiresBrokerOrOwnerConfirmation' AS Classification,
    COUNT(*) AS OptionCandidates,
    SUM(CASE WHEN TransactionType = 'OPEN' THEN 1 ELSE 0 END) AS OpenCandidates,
    SUM(CASE WHEN TransactionType = 'CLOSE' THEN 1 ELSE 0 END) AS ClosedCandidates,
    SUM(CASE WHEN MarketPrice <= 0 THEN 1 ELSE 0 END) AS NonpositiveMarks
FROM dbo.OptionItems;
SELECT SourceType, COUNT(*) AS DuplicateLinkGroups
FROM (
    SELECT SourceType, SourceItemId FROM dbo.CashItems WHERE SourceType IS NOT NULL
    GROUP BY SourceType, SourceItemId HAVING COUNT(*) > 1
) AS duplicates GROUP BY SourceType;
