SET NOCOUNT ON;
IF DB_NAME() <> 'PortfolioManagerLocal'
    THROW 51015, 'This read-only fingerprint query targets the approved local database only.', 1;

SELECT 'CashItems' AS TableName, COUNT_BIG(*) AS [RowCount],
    CONVERT(varchar(64), HASHBYTES('SHA2_256',
        (SELECT * FROM dbo.CashItems ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)), 2) AS Digest
FROM dbo.CashItems
UNION ALL SELECT 'OptionItems', COUNT_BIG(*),
    CONVERT(varchar(64), HASHBYTES('SHA2_256',
        (SELECT * FROM dbo.OptionItems ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)), 2)
FROM dbo.OptionItems
UNION ALL SELECT 'PortfolioItems', COUNT_BIG(*),
    CONVERT(varchar(64), HASHBYTES('SHA2_256',
        (SELECT * FROM dbo.PortfolioItems ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)), 2)
FROM dbo.PortfolioItems
UNION ALL SELECT 'WatchlistItems', COUNT_BIG(*),
    CONVERT(varchar(64), HASHBYTES('SHA2_256',
        (SELECT * FROM dbo.WatchlistItems ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)), 2)
FROM dbo.WatchlistItems
UNION ALL SELECT 'OpportunitySnapshots', COUNT_BIG(*),
    CONVERT(varchar(64), HASHBYTES('SHA2_256',
        (SELECT * FROM dbo.OpportunitySnapshots ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)), 2)
FROM dbo.OpportunitySnapshots
UNION ALL SELECT 'OpportunityRefreshStates', COUNT_BIG(*),
    CONVERT(varchar(64), HASHBYTES('SHA2_256',
        (SELECT * FROM dbo.OpportunityRefreshStates ORDER BY UserId FOR JSON PATH, INCLUDE_NULL_VALUES)), 2)
FROM dbo.OpportunityRefreshStates
UNION ALL SELECT 'RsiScanSnapshots', COUNT_BIG(*),
    CONVERT(varchar(64), HASHBYTES('SHA2_256',
        (SELECT * FROM dbo.RsiScanSnapshots ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)), 2)
FROM dbo.RsiScanSnapshots
UNION ALL SELECT 'ValueScreenerSnapshots', COUNT_BIG(*),
    CONVERT(varchar(64), HASHBYTES('SHA2_256',
        (SELECT * FROM dbo.ValueScreenerSnapshots ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)), 2)
FROM dbo.ValueScreenerSnapshots;
