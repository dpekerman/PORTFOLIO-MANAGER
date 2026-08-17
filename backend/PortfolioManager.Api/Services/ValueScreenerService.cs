using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

/// <summary>
/// Capital Valuation Scoring Engine — v2 (Enhanced with real FCF, proper Piotroski, dynamic normalization, sector routing)
///
/// SCORING MATRIX (max 10.0 normalized points):
///   +3.0  F1 — Valuation Anchor  : EarningsYield = EBIT / EnterpriseValue  (or 1/TrailingPE fallback)
///   +2.0  F2 — Cash Sufficiency  : FCF Yield = FreeCashFlow / MarketCap     (real OCF−CapEx, not 1/FwdPE)
///   +1.0  F3 — Asset Utilization : Price-to-Book (skipped / routed for REITs & Financials)
///   +3.0  F4 — Fundamental Health: Real Piotroski F-Score (9 accounting-based signals, no PE dependency)
///   +1.0  F5 — Capital Efficiency: ROIC = EBIT / (TotalAssets − CurrentLiabilities)  (or ROE fallback)
///
/// DYNAMIC NORMALIZATION:
///   Each factor that cannot be calculated (missing data) is excluded from BOTH numerator AND denominator.
///   Final score is rescaled to base-10: Score = (sum of earned pts / max available pts) × 10
///
/// SECTOR ROUTER:
///   Real Estate → use Price/FFO logic (treat OCF as FFO), skip P/B
///   Financial Services (Banks/Insurance) → use ROE-driven scoring, relax P/B threshold
///   Everything else → standard engine
///
/// REAL PIOTROSKI 9 SIGNALS (accounting-based, independent of P/E):
///   F1: Net Income > 0 (profitability)
///   F2: Operating Cash Flow > 0 (cash generation)
///   F3: ROA improved YoY  → proxied: ROA > 0
///   F4: Operating CF > Net Income (accrual quality)
///   F5: Long-term debt ratio decreased → proxied: DebtToEquity < 1.5
///   F6: Current Ratio > 1 (liquidity improving)
///   F7: Shares not diluted → proxied: share-level data unavailable → use RevenueGrowth > 0
///   F8: Gross margin improving → proxied: ProfitMargins > 0
///   F9: Asset turnover improving → proxied: revenue / total assets > 0.3
/// </summary>
public sealed class ValueScreenerService(
    IPortfolioService portfolioService,
    IWatchlistService watchlistService,
    IMarketDataProvider marketData,
    IRsiScannerService rsiScanner,
    ILogger<ValueScreenerService> logger)
{
    public async Task<List<ValueScreenerResult>> RunAsync(
        ValueScreenerRequest request,
        CancellationToken ct = default)
    {
        // 1. Collect symbols + origins
        var symbolsWithOrigin = new Dictionary<string, ValueOrigin>(StringComparer.OrdinalIgnoreCase);

        if (request.IncludeWatchlist)
        {
            var wl = await watchlistService.GetAllAsync(ct);
            foreach (var w in wl) symbolsWithOrigin.TryAdd(w.Symbol, ValueOrigin.Watchlist);
        }
        if (request.IncludePortfolio)
        {
            var portfolio = await portfolioService.GetAllAsync(ct);
            foreach (var p in portfolio.Where(x => !x.IsManual))
                symbolsWithOrigin[p.Symbol] = ValueOrigin.Portfolio;
        }
        foreach (var adhoc in request.AdHocSymbols
            .Select(s => s.Trim().ToUpperInvariant()).Where(s => s.Length > 0))
            symbolsWithOrigin.TryAdd(adhoc, ValueOrigin.Watchlist);

        if (symbolsWithOrigin.Count == 0) return [];

        // 2. Batch-fetch live price quotes (for current price, 52-week range, sector name)
        Dictionary<string, StockQuote> quotes;
        try { quotes = await marketData.GetBatchQuotesAsync(symbolsWithOrigin.Keys, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Value screener: batch quote fetch failed"); quotes = []; }

        // 3. Fetch deep fundamentals per symbol via v10/quoteSummary (parallel, 3 at a time)
        var fundamentals = new Dictionary<string, FundamentalsSnapshot>(StringComparer.OrdinalIgnoreCase);
        var symList = symbolsWithOrigin.Keys.ToList();
        var semaphore = new System.Threading.SemaphoreSlim(3, 3);
        var tasks = symList.Select(async sym =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var snap = await marketData.GetFundamentalsAsync(sym, ct);
                if (snap != null) lock (fundamentals) { fundamentals[sym] = snap; }
            }
            catch (Exception ex) { logger.LogWarning(ex, "Fundamentals fetch failed for {Symbol}", sym); }
            finally { semaphore.Release(); await Task.Delay(200, ct); }
        });
        await Task.WhenAll(tasks);

        // 4. Fetch RSI + technical indicators (Enhanced mode always)
        Dictionary<string, RsiScanResult> rsiMap;
        try
        {
            var rsiResults = await rsiScanner.AnalyzeSymbolsAsync(
                symList, oversoldThreshold: 30m, overboughtThreshold: 75m, logicMode: "Enhanced", ct: ct);
            rsiMap = rsiResults.ToDictionary(r => r.Symbol, r => r, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) { logger.LogWarning(ex, "Value screener: RSI fetch failed"); rsiMap = []; }

        // 5. Build results
        var results = new List<ValueScreenerResult>(symbolsWithOrigin.Count);
        foreach (var (sym, origin) in symbolsWithOrigin)
        {
            quotes.TryGetValue(sym, out var quote);
            fundamentals.TryGetValue(sym, out var snap);
            rsiMap.TryGetValue(sym, out var rsiResult);
            results.Add(BuildResult(sym, origin, quote, snap, rsiResult));
        }

        results.Sort((a, b) =>
        {
            int td = a.Tier.CompareTo(b.Tier);
            return td != 0 ? td : b.Score.CompareTo(a.Score);
        });
        return results;
    }

    // ── Scoring Engine ─────────────────────────────────────────────────────────

    private ValueScreenerResult BuildResult(
        string symbol, ValueOrigin origin,
        StockQuote? quote, FundamentalsSnapshot? snap, RsiScanResult? rsiResult)
    {
        // ── Raw data ───────────────────────────────────────────────────────────
        decimal price       = quote?.CurrentPrice ?? snap?.Price ?? 0m;
        decimal week52High  = snap?.Week52High  > 0 ? snap.Week52High  : quote?.Week52High  ?? 0m;
        decimal week52Low   = snap?.Week52Low   > 0 ? snap.Week52Low   : quote?.Week52Low   ?? 0m;
        decimal rsi         = rsiResult?.Rsi ?? 0m;
        decimal volumeRatio = rsiResult?.VolumeRatio ?? 0m;
        string  description = quote?.CompanyName ?? symbol;
        string  sector      = quote?.Sector ?? snap?.Sector ?? "";

        // Sector router flag
        bool isReit      = sector.Contains("Real Estate", StringComparison.OrdinalIgnoreCase);
        bool isFinancial = sector.Contains("Financial", StringComparison.OrdinalIgnoreCase)
                        || sector.Contains("Bank",       StringComparison.OrdinalIgnoreCase)
                        || sector.Contains("Insurance",  StringComparison.OrdinalIgnoreCase);

        // ── Fundamentals extraction ────────────────────────────────────────────
        long   marketCap     = snap?.MarketCap > 0 ? snap.MarketCap : (long)(quote?.CurrentPrice * 1_000_000m ?? 0m);
        long   ev            = snap?.EnterpriseValue ?? 0L;
        long   ebit          = snap?.Ebit ?? 0L;
        long   fcf           = snap?.FreeCashFlow ?? 0L;
        long   ocf           = snap?.OperatingCashFlow ?? 0L;
        long   netIncome     = snap?.NetIncome ?? 0L;
        long   totalAssets   = snap?.TotalAssets ?? 0L;
        long   currLiab      = snap?.TotalCurrentLiabilities ?? 0L;
        long   equity        = snap?.StockholdersEquity ?? 0L;
        long   revenue       = snap?.TotalRevenue ?? 0L;
        decimal roe          = snap?.ReturnOnEquity ?? 0m;
        decimal roa          = snap?.ReturnOnAssets ?? 0m;
        decimal debtToEquity = snap?.DebtToEquity ?? 0m;
        decimal currentRatio = snap?.CurrentRatio ?? 0m;
        decimal profitMargin = snap?.ProfitMargins ?? 0m;
        decimal revGrowth    = snap?.RevenueGrowth ?? 0m;
        decimal trailingPE   = snap?.TrailingPE > 0 ? snap.TrailingPE  : quote?.TrailingPE ?? 0m;
        decimal forwardPE    = snap?.ForwardPE  > 0 ? snap.ForwardPE   : quote?.ForwardPE  ?? 0m;
        decimal ptb          = snap?.PriceToBook > 0 ? snap.PriceToBook : quote?.PriceToBook ?? 0m;
        decimal divYield     = snap?.DividendYield > 0 ? snap.DividendYield
                             : snap?.TrailingAnnualDividendYield > 0 ? snap.TrailingAnnualDividendYield
                             : quote?.DividendYield ?? 0m;

        // ── F1: Valuation Anchor — Earnings Yield (max +3.0) ──────────────────
        // Primary: EBIT / EV (true EV/EBIT earnings yield)
        // Fallback: 1 / TrailingPE × 100
        decimal earningsYield;
        bool f1Available;
        if (ev > 0 && ebit > 0)
        {
            earningsYield = ((decimal)ebit / ev) * 100m;
            f1Available = true;
        }
        else if (trailingPE > 0)
        {
            earningsYield = (1m / trailingPE) * 100m;
            f1Available = true;
        }
        else
        {
            earningsYield = 0m;
            f1Available = false;
        }

        decimal f1 = !f1Available ? 0m :
                     earningsYield >= 8m ? 3.0m :
                     earningsYield >= 5m ? 1.5m : 0m;
        decimal f1Max = f1Available ? 3.0m : 0m;

        // ── F2: Cash Sufficiency — Real FCF Yield (max +2.0) ──────────────────
        // FCF = Operating Cash Flow − Capital Expenditures  (from cashflowStatementHistory)
        // FCF Yield = FCF / Market Cap
        decimal fcfYield;
        bool f2Available;
        if (fcf != 0 && marketCap > 0)
        {
            fcfYield = ((decimal)fcf / marketCap) * 100m;
            f2Available = true;
        }
        else if (ocf != 0 && marketCap > 0)
        {
            fcfYield = ((decimal)ocf / marketCap) * 100m;
            f2Available = true;
        }
        else
        {
            fcfYield = 0m;
            f2Available = false;
        }

        decimal f2 = !f2Available ? 0m :
                     fcfYield >= 6m ? 2.0m :
                     fcfYield >= 3m ? 1.0m : 0m;
        decimal f2Max = f2Available ? 2.0m : 0m;

        // ── F3: Asset Utilization — P/B (max +1.0; skipped for REITs) ─────────
        // REITs: P/B is meaningless. Use Price/FFO proxy (FFO ≈ OCF for simplicity)
        // Financials: relax threshold to 2.5
        bool f3Available;
        decimal f3;
        if (isReit)
        {
            // Price-to-FFO proxy: FFO ≈ OCF, shares = MarketCap / Price
            if (ocf > 0 && marketCap > 0 && price > 0)
            {
                long sharesEst    = (long)(marketCap / (double)price);
                decimal ffoShare  = sharesEst > 0 ? (decimal)ocf / sharesEst : 0m;
                decimal pffRatio  = ffoShare > 0 ? price / ffoShare : 0m;
                f3 = pffRatio is > 0 and <= 12m ? 1.0m : 0m;
                f3Available = ocf > 0 && marketCap > 0 && price > 0;
            }
            else { f3 = 0m; f3Available = false; }
        }
        else if (isFinancial)
        {
            f3Available = ptb > 0;
            f3 = ptb is > 0 and <= 2.5m ? 1.0m : 0m;
        }
        else
        {
            f3Available = ptb > 0;
            f3 = ptb is > 0 and <= 1.5m ? 1.0m : 0m;
        }
        decimal f3Max = f3Available ? 1.0m : 0m;

        // ── F4: Fundamental Health — Real Piotroski (max +3.0) ────────────────
        // 9 accounting-based signals, NONE depend on market multiples (no PE usage)
        int pio = 0;
        int pioAvailable = 0;

        // Signal 1: Net Income > 0  (profitability — ROA positive)
        if (netIncome != 0 || roa != 0m) { pioAvailable++; if (netIncome > 0 || roa > 0m) pio++; }
        // Signal 2: Operating Cash Flow > 0  (actual cash generation)
        if (ocf != 0) { pioAvailable++; if (ocf > 0) pio++; }
        // Signal 3: ROA > 0  (asset efficiency, mirrors Piotroski's ΔRoa signal)
        if (roa != 0m) { pioAvailable++; if (roa > 0.01m) pio++; }
        // Signal 4: Cash flow quality — OCF > Net Income (accrual reversal)
        if (ocf != 0 && netIncome != 0) { pioAvailable++; if (ocf > netIncome) pio++; }
        // Signal 5: Leverage declining — DebtToEquity < 1.5 (or equity > 0 and no long-term debt explosion)
        if (debtToEquity != 0m || equity != 0) { pioAvailable++; if (debtToEquity >= 0m && debtToEquity < 1.5m) pio++; }
        // Signal 6: Liquidity improving — Current Ratio > 1.0
        if (currentRatio != 0m) { pioAvailable++; if (currentRatio > 1.0m) pio++; }
        // Signal 7: No equity dilution — proxy: positive revenue growth (indirect health signal)
        if (revGrowth != 0m) { pioAvailable++; if (revGrowth > 0m) pio++; }
        // Signal 8: Gross margin positive — company has pricing power
        if (profitMargin != 0m) { pioAvailable++; if (profitMargin > 0m) pio++; }
        // Signal 9: Asset turnover > 0.3 (revenue generating relative to asset base)
        if (revenue > 0 && totalAssets > 0)
        {
            pioAvailable++;
            decimal assetTurnover = (decimal)revenue / totalAssets;
            if (assetTurnover > 0.3m) pio++;
        }

        bool f4Available = pioAvailable >= 4; // at least half the signals are verifiable
        decimal pioRatio = pioAvailable > 0 ? (decimal)pio / pioAvailable : 0m;
        decimal f4 = !f4Available ? 0m :
                     pioRatio >= 0.77m ? 3.0m :   // 7+/9 or equivalent proportion
                     pioRatio >= 0.44m ? 1.5m :   // 4+/9 equivalent
                     0m;
        decimal f4Max = f4Available ? 3.0m : 0m;

        // ── F5: Capital Efficiency — ROIC (max +1.0) ──────────────────────────
        // Primary: ROIC = EBIT / Invested Capital  where IC = TotalAssets − CurrentLiabilities
        // Fallback: ROE from financialData (for financials where ROIC is less meaningful)
        // Fallback 2: EarningsYield / PriceToBook
        decimal roic;
        bool f5Available;
        long investedCapital = totalAssets - currLiab;
        if (ebit != 0 && investedCapital > 0)
        {
            roic = ((decimal)ebit / investedCapital) * 100m;
            f5Available = true;
        }
        else if (roe != 0m)
        {
            roic = roe * 100m;  // convert ratio to percentage
            f5Available = true;
        }
        else if (ptb > 0 && earningsYield > 0)
        {
            roic = earningsYield / ptb;  // simplified proxy
            f5Available = true;
        }
        else { roic = 0m; f5Available = false; }

        decimal f5 = !f5Available ? 0m : (roic >= 10m ? 1.0m : 0m);
        decimal f5Max = f5Available ? 1.0m : 0m;

        // ── Dynamic Normalization ──────────────────────────────────────────────
        decimal earnedPts = f1 + f2 + f3 + f4 + f5;
        decimal maxAvail  = f1Max + f2Max + f3Max + f4Max + f5Max;
        decimal totalScore = maxAvail > 0 ? Math.Round((earnedPts / maxAvail) * 10m, 1) : 0m;
        totalScore = Math.Min(totalScore, 10m);

        // ── Technical State ───────────────────────────────────────────────────
        bool nearLow         = week52Low  > 0 && price > 0 && price <= week52Low  * 1.20m;
        bool nearHigh        = week52High > 0 && price > 0 && price >= week52High * 0.90m;
        bool pullingFromHigh = week52High > 0 && price > 0 && price >= week52High * 0.80m;

        TechnicalState techState;
        if (volumeRatio >= 2.5m && rsi >= 65m)           techState = TechnicalState.HighVolumeExhaustion;
        else if (rsi is > 0 and < 35m && nearLow)        techState = TechnicalState.DeepValueReversal;
        else if (rsi >= 72m && nearHigh)                 techState = TechnicalState.OverboughtMomentum;
        else if (rsi is >= 55m and < 72m && pullingFromHigh) techState = TechnicalState.OverboughtPullback;
        else if (rsi is >= 42m and <= 62m)               techState = TechnicalState.SidewaysConsolidation;
        else                                              techState = TechnicalState.MeanReversion;

        // ── Tier Classification ────────────────────────────────────────────────
        ValueTier tier = totalScore >= 8m ? ValueTier.HighConviction
                       : totalScore >= 5m ? ValueTier.FairValue
                       : ValueTier.ValueTrap;

        // ── Action Trigger ─────────────────────────────────────────────────────
        ActionTrigger action;
        decimal divPct = divYield >= 0.01m ? divYield * 100m : divYield; // normalize to %
        bool meaningfulDiv = divPct >= 2m || divYield >= 0.02m;
        if (tier == ValueTier.ValueTrap)
            action = ActionTrigger.ValueTrapWarning;
        else if (techState == TechnicalState.OverboughtMomentum)
            action = ActionTrigger.HoldRideTrend;
        else if (tier == ValueTier.HighConviction)
        {
            if (techState == TechnicalState.DeepValueReversal && meaningfulDiv)
                action = ActionTrigger.AccumulateYield;
            else if (techState == TechnicalState.DeepValueReversal && pio >= 7)
                action = ActionTrigger.AccumulateValue;
            else
                action = ActionTrigger.BuyLimitAlert;
        }
        else
            action = ActionTrigger.Observe;

        logger.LogDebug(
            "Screener {Sym}: score={Score} tier={Tier} f1={F1}/{F1Max} f2={F2}/{F2Max} f3={F3}/{F3Max} f4={F4}/{F4Max} f5={F5}/{F5Max} pio={Pio}/{PioAvail}",
            symbol, totalScore, tier, f1, f1Max, f2, f2Max, f3, f3Max, f4, f4Max, f5, f5Max, pio, pioAvailable);

        return new ValueScreenerResult
        {
            Symbol             = symbol,
            Description        = description,
            Origin             = origin,
            TechnicalState     = techState,
            Tier               = tier,
            Score              = totalScore,
            ActionTrigger      = action,
            ScoreEarningsYield = f1,
            ScoreFcfYield      = f2,
            ScorePriceToBook   = f3,
            ScorePiotroski     = f4,
            ScoreRoic          = f5,
            EarningsYield      = Math.Round(earningsYield, 2),
            FcfYieldProxy      = Math.Round(fcfYield, 2),
            PriceToBook        = ptb,
            PiotroskiScore     = pio,
            RoicProxy          = Math.Round(roic, 2),
            DividendYield      = divYield >= 0.5m ? Math.Round(divYield, 2) : Math.Round(divYield * 100m, 2), // normalize to %
            CurrentPrice       = price,
            CurrentRsi         = Math.Round(rsi, 1),
            Week52High         = week52High,
            Week52Low          = week52Low,
            Sector             = sector,
            AnalyzedAt         = DateTime.UtcNow
        };
    }
}
