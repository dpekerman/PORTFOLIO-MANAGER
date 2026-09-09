/* Adds persisted earnings dates to watchlist items. Safe for SQL Server and Azure SQL. */
IF COL_LENGTH(N'dbo.WatchlistItems', N'EarningsDate') IS NULL
    BEGIN
        ALTER TABLE dbo.WatchlistItems
            ADD EarningsDate DATETIME2 NULL;
    END