-- Adds the analysis ticker/currency used for stored DailySignals prices.
-- Idempotent; mirrors the columns in EF migration 20261002140402_AddAnalysisCurrencyToDailySignals.
IF COL_LENGTH('dbo.DailySignals', 'AnalysisCurrency') IS NULL
    ALTER TABLE dbo.DailySignals
        ADD AnalysisCurrency NVARCHAR (10) NULL;

IF COL_LENGTH('dbo.DailySignals', 'AnalysisTicker') IS NULL
    ALTER TABLE dbo.DailySignals
        ADD AnalysisTicker NVARCHAR (20) NULL;


GO
UPDATE ds
SET    ds.AnalysisTicker = COALESCE(
           ds.AnalysisTicker,
           CASE WHEN m.UseUnderlyingForAnalysis = 1 AND m.UnderlyingTicker IS NOT NULL
               THEN m.UnderlyingTicker ELSE ds.Symbol END),
       ds.AnalysisCurrency = COALESCE(
           ds.AnalysisCurrency,
           CASE WHEN m.UseUnderlyingForAnalysis = 1 AND m.UnderlyingTicker IS NOT NULL THEN 'USD'
               WHEN UPPER(ds.Symbol) LIKE '%.TO' THEN 'CAD' ELSE 'USD' END)
FROM   dbo.DailySignals AS ds
       LEFT JOIN dbo.SecurityAnalysisMappings AS m
           ON m.TradingTicker = ds.Symbol
          AND m.UserId IS NULL
          AND m.ResolutionStatus = 1
WHERE  ds.AnalysisTicker IS NULL OR ds.AnalysisCurrency IS NULL;