SET NOCOUNT ON;
IF DB_NAME() <> 'PortfolioManagerLocal'
    THROW 51015, 'This read-only evidence query targets the approved local database only.', 1;
IF COL_LENGTH('dbo.CashItems', 'CurrencyCode') IS NOT NULL
    THROW 51016, 'Currency schema has changed; reassess evidence before exporting.', 1;

SELECT
    CONVERT(varchar(33), SYSUTCDATETIME(), 126) + 'Z' AS CapturedAtUtc,
    JSON_QUERY((
        SELECT Id, CASE WHEN UserId IS NULL THEN 1 ELSE 0 END AS Shared,
            CASE WHEN NULLIF(AccountType, '') IS NULL THEN 0 ELSE 1 END AS HasAccountLabel,
            CASE WHEN TransactionDate IS NULL THEN 0 ELSE 1 END AS HasEffectiveDate,
            CashFlowType, SourceType, SourceItemId
        FROM dbo.CashItems ORDER BY Id FOR JSON PATH
    )) AS Cash,
    JSON_QUERY((
        SELECT Id, TransactionType, NumberOfContracts,
            CASE WHEN NULLIF(AccountType, '') IS NULL THEN 0 ELSE 1 END AS HasAccountLabel,
            CASE WHEN Strike > 0 AND ExpirationDate IS NOT NULL
                AND PositionType IN ('CALL','PUT') THEN 1 ELSE 0 END AS HasCandidateTerms,
            CONVERT(varchar(64), HASHBYTES('SHA2_256', CONCAT(
                UserId, '|', AccountType, '|', UnderlyingTicker, '|',
                CONVERT(varchar(33), ExpirationDate, 126), '|', Strike, '|', PositionType)), 2) AS CandidateGroup
        FROM dbo.OptionItems ORDER BY Id FOR JSON PATH
    )) AS Options,
    (SELECT COUNT(*) FROM (
        SELECT UPPER(LTRIM(RTRIM(Symbol))) AS Symbol FROM dbo.WatchlistItems
        UNION SELECT UPPER(LTRIM(RTRIM(Symbol))) FROM dbo.PortfolioItems
            WHERE IsManual = 0 AND (TransactionType IS NULL OR TransactionType = 'OPEN')
        UNION SELECT UPPER(LTRIM(RTRIM(Symbol))) FROM dbo.IndexConstituents
            WHERE IndexCode = 'TSX60' AND EffectiveTo IS NULL
    ) AS universe WHERE Symbol <> '') AS UniverseListings,
    (SELECT COUNT(*) FROM dbo.BrokerStopRecords WHERE BrokerVerificationStatus = 'Verified') AS VerifiedBrokerStops,
    (SELECT COUNT(*) FROM (
        SELECT SourceType, SourceItemId FROM dbo.CashItems WHERE SourceType IS NOT NULL
        GROUP BY SourceType, SourceItemId HAVING COUNT(*) > 1
    ) AS duplicates) AS DuplicateTradeLinks
FOR JSON PATH, WITHOUT_ARRAY_WRAPPER;
