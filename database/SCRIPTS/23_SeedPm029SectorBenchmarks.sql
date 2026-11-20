-- =====================================================================================
-- PM-029 — Seed sector benchmark mappings (SectorBenchmarkMappings) for Sector Heat
-- STATUS: REVIEW ONLY. DO NOT RUN without separate explicit authorization (PM-029 U7).
-- Requires migration AddPm029TradingOpportunities (21_AddPm029TradingOpportunities.sql).
--
-- Verification (2026-11-14): every benchmark symbol below was confirmed against public Yahoo Finance
-- chart metadata (longName, currency, exchange, instrumentType = ETF). No application data was sent.
-- Sector = canonical GICS name; ProviderSectorAliasesJson = sector names used by Yahoo / this app's
-- PortfolioItems / SectorIndustryConfig that map to it.
--
-- Deliberately UNMAPPED (Sector Heat shows "Unavailable — no benchmark"):
--   Tsx  Consumer Discretionary  — XCD.TO is a *global* CAD-hedged fund, not a TSX sector benchmark.
--   Tsx  Communication Services  — no verified S&P/TSX capped communication-services ETF.
--   Tsx  Health Care             — no verified S&P/TSX health-care sector ETF.
--   ETFs & Funds (any market)    — not a sector.
-- Flagged for your review (marked in Notes):
--   Tsx  Real Estate  -> XRE.TO tracks the S&P/TSX Capped REIT index (REITs only; non-REIT real estate, e.g. FSV, differs).
--   Tsx  Industrials  -> ZIN.TO is BMO *equal-weight* Canadian industrials (no cap-weighted iShares equivalent verified).
-- Idempotent: inserts only (Sector, Market) pairs without an active mapping.
-- =====================================================================================
SET NOCOUNT ON;
DECLARE @AsOf date = '2026-11-14';
DECLARE @Src nvarchar(300) = N'Issuer fund name verified via Yahoo Finance chart metadata 2026-11-14';

BEGIN TRANSACTION;
INSERT INTO SectorBenchmarkMappings (Sector, Market, BenchmarkSymbol, BenchmarkName, Currency, ProviderSectorAliasesJson, IsActive, Notes, Source, SourceAsOfDate, UpdatedAtUtc, UpdatedBy)
SELECT v.Sector, v.Market, v.Symbol, v.Name, v.Currency, v.Aliases, 1, v.Notes, @Src, @AsOf, SYSUTCDATETIME(), N'PM-029 seed'
FROM (VALUES
    (N'Financials',             N'Tsx',  N'XFN.TO', N'iShares S&P/TSX Capped Financials Index ETF',             N'CAD', N'["Financials","Financial Services","Financial"]', NULL),
    (N'Energy',                 N'Tsx',  N'XEG.TO', N'iShares S&P/TSX Capped Energy Index ETF',                 N'CAD', N'["Energy"]', NULL),
    (N'Materials',              N'Tsx',  N'XMA.TO', N'iShares S&P/TSX Capped Materials Index ETF',              N'CAD', N'["Materials","Basic Materials"]', NULL),
    (N'Information Technology', N'Tsx',  N'XIT.TO', N'iShares S&P/TSX Capped Information Technology Index ETF', N'CAD', N'["Information Technology","Technology"]', NULL),
    (N'Utilities',              N'Tsx',  N'XUT.TO', N'iShares S&P/TSX Capped Utilities Index ETF',              N'CAD', N'["Utilities"]', NULL),
    (N'Consumer Staples',       N'Tsx',  N'XST.TO', N'iShares S&P/TSX Capped Consumer Staples Index ETF',       N'CAD', N'["Consumer Staples","Consumer Defensive"]', NULL),
    (N'Real Estate',            N'Tsx',  N'XRE.TO', N'iShares S&P/TSX Capped REIT Index ETF',                   N'CAD', N'["Real Estate"]', N'REVIEW: REIT-only index'),
    (N'Industrials',            N'Tsx',  N'ZIN.TO', N'BMO Equal Weight Industrials Index ETF',                  N'CAD', N'["Industrials"]', N'REVIEW: equal-weight index'),
    (N'Financials',             N'Nyse', N'XLF',    N'Financial Select Sector SPDR ETF',                        N'USD', N'["Financials","Financial Services","Financial"]', NULL),
    (N'Energy',                 N'Nyse', N'XLE',    N'Energy Select Sector SPDR ETF',                           N'USD', N'["Energy"]', NULL),
    (N'Materials',              N'Nyse', N'XLB',    N'Materials Select Sector SPDR ETF',                        N'USD', N'["Materials","Basic Materials"]', NULL),
    (N'Information Technology', N'Nyse', N'XLK',    N'Technology Select Sector SPDR ETF',                       N'USD', N'["Information Technology","Technology"]', NULL),
    (N'Utilities',              N'Nyse', N'XLU',    N'Utilities Select Sector SPDR ETF',                        N'USD', N'["Utilities"]', NULL),
    (N'Consumer Staples',       N'Nyse', N'XLP',    N'Consumer Staples Select Sector SPDR ETF',                 N'USD', N'["Consumer Staples","Consumer Defensive"]', NULL),
    (N'Consumer Discretionary', N'Nyse', N'XLY',    N'Consumer Discretionary Select Sector SPDR ETF',           N'USD', N'["Consumer Discretionary","Consumer Cyclical"]', NULL),
    (N'Communication Services', N'Nyse', N'XLC',    N'Communication Services Select Sector SPDR ETF',           N'USD', N'["Communication Services","Communication"]', NULL),
    (N'Industrials',            N'Nyse', N'XLI',    N'Industrial Select Sector SPDR ETF',                       N'USD', N'["Industrials"]', NULL),
    (N'Health Care',            N'Nyse', N'XLV',    N'Health Care Select Sector SPDR ETF',                      N'USD', N'["Health Care","Healthcare"]', NULL),
    (N'Real Estate',            N'Nyse', N'XLRE',   N'Real Estate Select Sector SPDR ETF',                      N'USD', N'["Real Estate"]', NULL)
) AS v(Sector, Market, Symbol, Name, Currency, Aliases, Notes)
WHERE NOT EXISTS (SELECT 1 FROM SectorBenchmarkMappings m WHERE m.Sector = v.Sector AND m.Market = v.Market AND m.IsActive = 1);

SELECT Market, COUNT(*) AS ActiveMappings FROM SectorBenchmarkMappings WHERE IsActive = 1 GROUP BY Market;
COMMIT TRANSACTION;
