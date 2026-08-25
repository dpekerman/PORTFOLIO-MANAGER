/* Adds persisted EOD position-sizing outputs to DailySignals.
   Safe to run repeatedly on local SQL Server or Azure SQL.
   EF migration: 20260825160859_AddEodPositionSizing
*/
IF COL_LENGTH(N'dbo.DailySignals', N'PositionSizingShares') IS NULL
    ALTER TABLE dbo.DailySignals
        ADD PositionSizingShares DECIMAL (18, 6) NULL;

IF COL_LENGTH(N'dbo.DailySignals', N'PositionSizingRiskAmount') IS NULL
    ALTER TABLE dbo.DailySignals
        ADD PositionSizingRiskAmount DECIMAL (18, 4) NULL;

IF COL_LENGTH(N'dbo.DailySignals', N'PositionSizingPositionValue') IS NULL
    ALTER TABLE dbo.DailySignals
        ADD PositionSizingPositionValue DECIMAL (18, 4) NULL;

IF COL_LENGTH(N'dbo.DailySignals', N'PositionSizingLimitingReason') IS NULL
    ALTER TABLE dbo.DailySignals
        ADD PositionSizingLimitingReason NVARCHAR (200) NULL;