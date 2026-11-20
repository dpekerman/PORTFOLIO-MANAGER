-- READ ONLY. Review all nonzero conflict sets before applying corrective migration SQL.
SET NOCOUNT ON;
SELECT UserId, AccountType, Symbol, TradeEpisodeId, COUNT_BIG(*) AS Conflicts
FROM dbo.RiskPlans
GROUP BY UserId, AccountType, Symbol, TradeEpisodeId HAVING COUNT_BIG(*) > 1;

SELECT p.Id AS PortfolioItemId, p.TradeEpisodeId
FROM dbo.PortfolioItems p LEFT JOIN dbo.TradeEpisodes e ON e.Id = p.TradeEpisodeId
WHERE p.TradeEpisodeId IS NOT NULL AND (e.Id IS NULL OR p.Symbol <> e.Symbol
    OR ISNULL(p.AccountType, '') <> e.AccountType
    OR ISNULL(p.UserId, '') <> ISNULL(e.UserId, ''));

SELECT s.Id AS BrokerStopRecordId, s.TradeEpisodeId, 'Existing association requires review; do not reassign automatically' AS Note
FROM dbo.BrokerStopRecords s LEFT JOIN dbo.TradeEpisodes e ON e.Id = s.TradeEpisodeId
WHERE s.TradeEpisodeId IS NOT NULL AND (e.Id IS NULL OR s.Symbol <> e.Symbol OR s.AccountType <> e.AccountType
    OR ISNULL(s.UserId, '') <> ISNULL(e.UserId, ''));

SELECT p.Id AS RiskPlanId FROM dbo.RiskPlans p
LEFT JOIN dbo.TradeEpisodes e ON e.Id = p.TradeEpisodeId
WHERE p.TradeEpisodeId IS NOT NULL AND (e.Id IS NULL OR p.Symbol <> e.Symbol
    OR p.AccountType <> e.AccountType OR ISNULL(p.UserId, '') <> ISNULL(e.UserId, ''));

SELECT a.Id AS AuditEventId FROM dbo.RiskAuditEvents a
LEFT JOIN dbo.RiskPlans p ON p.Id = a.RiskPlanId
LEFT JOIN dbo.BrokerStopRecords s ON s.Id = a.BrokerStopRecordId
WHERE (a.RiskPlanId IS NOT NULL AND p.Id IS NULL)
    OR (a.BrokerStopRecordId IS NOT NULL AND s.Id IS NULL);

IF COL_LENGTH(N'dbo.RiskAuditEvents', N'TradeEpisodeId') IS NOT NULL
    EXEC sys.sp_executesql N'
        SELECT a.Id AS OrphanAuditEpisodeId FROM dbo.RiskAuditEvents a
        LEFT JOIN dbo.TradeEpisodes e ON e.Id = a.TradeEpisodeId
        WHERE a.TradeEpisodeId IS NOT NULL AND e.Id IS NULL;';
