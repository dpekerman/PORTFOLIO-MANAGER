-- =====================================================================================
-- PM-029 — Seed S&P/TSX 60 constituents (IndexConstituents, IndexCode = 'TSX60')
-- STATUS: REVIEW ONLY. DO NOT RUN without separate explicit authorization (PM-029 U7).
-- Requires migration AddPm029TradingOpportunities (21_AddPm029TradingOpportunities.sql).
--
-- Sources (public, fetched 2026-11-14; no application data sent):
--   1. iShares S&P/TSX 60 Index ETF (XIU) holdings file, "Fund Holdings as of Nov 12, 2026"
--      https://www.blackrock.com/ca/investors/en/products/239832/ishares-sptsx-60-index-etf
--      60 equity lines + 1 non-tradable line (2299955D, Constellation Software right) excluded.
--   2. Wikipedia "S&P/TSX 60" constituent table, as of 2026-01-31 (citing TMX).
--   Both sources list the identical 60 TSX symbols.
-- Symbols are stored in Yahoo Finance form (class/unit suffix '.' -> '-', '.TO' exchange suffix).
-- Idempotent: inserts only symbols without a current (EffectiveTo IS NULL) TSX60 row.
-- =====================================================================================
SET NOCOUNT ON;
DECLARE @EffectiveFrom date = '2026-11-12';
DECLARE @Source nvarchar(300) = N'iShares XIU holdings 2026-11-12 cross-checked with Wikipedia S&P/TSX 60 (2026-01-31)';

BEGIN TRANSACTION;
INSERT INTO IndexConstituents (IndexCode, Symbol, CompanyName, EffectiveFrom, EffectiveTo, Source, SourceAsOfDate, VerifiedAtUtc, VerifiedBy)
SELECT N'TSX60', v.Symbol, v.CompanyName, @EffectiveFrom, NULL, @Source, @EffectiveFrom, NULL, NULL
FROM (VALUES
    (N'AEM.TO', N'Agnico Eagle Mines Limited'),
    (N'ATD.TO', N'Alimentation Couche-Tard Inc.'),
    (N'BMO.TO', N'Bank of Montreal'),
    (N'BNS.TO', N'Bank of Nova Scotia'),
    (N'ABX.TO', N'Barrick Mining Corporation'),
    (N'BCE.TO', N'BCE Inc.'),
    (N'BAM.TO', N'Brookfield Asset Management Ltd.'),
    (N'BN.TO', N'Brookfield Corporation'),
    (N'BIP-UN.TO', N'Brookfield Infrastructure Partners L.P.'),
    (N'CAE.TO', N'CAE Inc.'),
    (N'CCO.TO', N'Cameco Corporation'),
    (N'CM.TO', N'Canadian Imperial Bank of Commerce'),
    (N'CNR.TO', N'Canadian National Railway Company'),
    (N'CNQ.TO', N'Canadian Natural Resources Limited'),
    (N'CP.TO', N'Canadian Pacific Kansas City Limited'),
    (N'CTC-A.TO', N'Canadian Tire Corporation Limited'),
    (N'CCL-B.TO', N'CCL Industries Inc.'),
    (N'CLS.TO', N'Celestica Inc.'),
    (N'CVE.TO', N'Cenovus Energy Inc.'),
    (N'GIB-A.TO', N'CGI Inc.'),
    (N'CSU.TO', N'Constellation Software Inc.'),
    (N'DOL.TO', N'Dollarama Inc.'),
    (N'EMA.TO', N'Emera Incorporated'),
    (N'ENB.TO', N'Enbridge Inc.'),
    (N'FFH.TO', N'Fairfax Financial Holdings Limited'),
    (N'FM.TO', N'First Quantum Minerals Ltd.'),
    (N'FSV.TO', N'FirstService Corporation'),
    (N'FTS.TO', N'Fortis Inc.'),
    (N'FNV.TO', N'Franco-Nevada Corporation'),
    (N'WN.TO', N'George Weston Limited'),
    (N'GIL.TO', N'Gildan Activewear Inc.'),
    (N'H.TO', N'Hydro One Limited'),
    (N'IMO.TO', N'Imperial Oil Limited'),
    (N'IFC.TO', N'Intact Financial Corporation'),
    (N'K.TO', N'Kinross Gold Corporation'),
    (N'L.TO', N'Loblaw Companies Limited'),
    (N'MG.TO', N'Magna International Inc.'),
    (N'MFC.TO', N'Manulife Financial Corporation'),
    (N'MRU.TO', N'Metro Inc.'),
    (N'NA.TO', N'National Bank of Canada'),
    (N'NTR.TO', N'Nutrien Ltd.'),
    (N'OTEX.TO', N'Open Text Corporation'),
    (N'PPL.TO', N'Pembina Pipeline Corporation'),
    (N'POW.TO', N'Power Corporation of Canada'),
    (N'QSR.TO', N'Restaurant Brands International Inc.'),
    (N'RCI-B.TO', N'Rogers Communications Inc.'),
    (N'RY.TO', N'Royal Bank of Canada'),
    (N'SAP.TO', N'Saputo Inc.'),
    (N'SHOP.TO', N'Shopify Inc.'),
    (N'SLF.TO', N'Sun Life Financial Inc.'),
    (N'SU.TO', N'Suncor Energy Inc.'),
    (N'TRP.TO', N'TC Energy Corporation'),
    (N'TECK-B.TO', N'Teck Resources Limited'),
    (N'T.TO', N'Telus Corporation'),
    (N'TRI.TO', N'Thomson Reuters Corporation'),
    (N'TD.TO', N'Toronto-Dominion Bank'),
    (N'TOU.TO', N'Tourmaline Oil Corp.'),
    (N'WCN.TO', N'Waste Connections Inc.'),
    (N'WPM.TO', N'Wheaton Precious Metals Corp.'),
    (N'WSP.TO', N'WSP Global Inc.')
) AS v(Symbol, CompanyName)
WHERE NOT EXISTS (SELECT 1 FROM IndexConstituents c WHERE c.IndexCode = N'TSX60' AND c.Symbol = v.Symbol AND c.EffectiveTo IS NULL);

SELECT COUNT(*) AS CurrentTsx60Members FROM IndexConstituents WHERE IndexCode = N'TSX60' AND EffectiveTo IS NULL;
COMMIT TRANSACTION;