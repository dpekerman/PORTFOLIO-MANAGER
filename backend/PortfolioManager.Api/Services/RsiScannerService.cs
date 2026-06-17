using System.Text.Json;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IRsiScannerService
{
    /// <summary>
    /// Scans the default TSX watchlist plus any <paramref name="extraSymbols"/> from the
    /// user's portfolio and watchlist, returning oversold/overbought chains.
    /// </summary>
    Task<ScannerResponse> ScanAsync(IEnumerable<string>? extraSymbols = null, decimal oversoldThreshold = 30m, decimal overboughtThreshold = 75m, string logicMode = "Legacy", CancellationToken ct = default);
    /// <summary>Analyze an ad-hoc list of symbols (e.g. user-entered tickers).</summary>
    Task<List<RsiScanResult>> AnalyzeSymbolsAsync(IEnumerable<string> symbols, decimal oversoldThreshold = 30m, decimal overboughtThreshold = 75m, string logicMode = "Legacy", CancellationToken ct = default);
}

public sealed class RsiScannerService : IRsiScannerService
{
    private readonly HttpClient _http;
    private readonly ILogger<RsiScannerService> _logger;
    private readonly IMarketDataProvider _marketData;

    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    // Liquid TSX large/mid-caps — Yahoo Finance supports .TO symbols on the free tier
    private static readonly string[] TsxWatchlist =
    [
        "RY.TO",  "TD.TO",  "BNS.TO", "BMO.TO", "CM.TO",
        "ENB.TO", "CNQ.TO", "SU.TO",  "TRP.TO", "CVE.TO",
        "BCE.TO", "T.TO",   "SHOP.TO","CP.TO",  "CNR.TO",
        "BAM.TO", "MFC.TO", "SLF.TO", "GWO.TO", "POW.TO",
        "ABX.TO", "WPM.TO", "AEM.TO", "K.TO",   "FM.TO",
        "ATD.TO", "MRU.TO", "L.TO",   "WN.TO",  "CCL.B.TO",
        "TFI.TO", "GFL.TO", "WSP.TO", "STN.TO", "TFII.TO",
        "ALA.TO", "KEY.TO", "NPI.TO", "BEP-UN.TO", "INE.TO",
        "CAR-UN.TO", "DIR-UN.TO", "AP-UN.TO", "IIP-UN.TO", "HR-UN.TO",
        "BRP.TO", "MDA.TO", "OTEX.TO","CSU.TO",  "DSG.TO"
    ];

    private static readonly Dictionary<string, string> CompanyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RY.TO"]      = "Royal Bank of Canada",      ["TD.TO"]      = "TD Bank",
        ["BNS.TO"]     = "Bank of Nova Scotia",        ["BMO.TO"]     = "Bank of Montreal",
        ["CM.TO"]      = "CIBC",                       ["ENB.TO"]     = "Enbridge",
        ["CNQ.TO"]     = "Canadian Natural Resources", ["SU.TO"]      = "Suncor Energy",
        ["TRP.TO"]     = "TC Energy",                  ["CVE.TO"]     = "Cenovus Energy",
        ["BCE.TO"]     = "BCE Inc.",                   ["T.TO"]       = "Telus",
        ["SHOP.TO"]    = "Shopify",                    ["CP.TO"]      = "Canadian Pacific Kansas City",
        ["CNR.TO"]     = "CN Rail",                    ["BAM.TO"]     = "Brookfield Asset Mgmt",
        ["MFC.TO"]     = "Manulife Financial",         ["SLF.TO"]     = "Sun Life Financial",
        ["GWO.TO"]     = "Great-West Lifeco",          ["POW.TO"]     = "Power Corp",
        ["ABX.TO"]     = "Barrick Gold",               ["WPM.TO"]     = "Wheaton Precious Metals",
        ["AEM.TO"]     = "Agnico Eagle Mines",         ["K.TO"]       = "Kinross Gold",
        ["FM.TO"]      = "First Quantum Minerals",     ["ATD.TO"]     = "Alimentation Couche-Tard",
        ["MRU.TO"]     = "Metro Inc.",                 ["L.TO"]       = "Loblaw Companies",
        ["WN.TO"]      = "George Weston",              ["CCL.B.TO"]   = "CCL Industries",
        ["TFI.TO"]     = "TFI International",          ["GFL.TO"]     = "GFL Environmental",
        ["WSP.TO"]     = "WSP Global",                 ["STN.TO"]     = "Stantec",
        ["TFII.TO"]    = "TFI International (Dual)",   ["ALA.TO"]     = "AltaGas",
        ["KEY.TO"]     = "Keyera Corp",                ["NPI.TO"]     = "Northland Power",
        ["BEP-UN.TO"]  = "Brookfield Renewable",       ["INE.TO"]     = "Innergex Renewable",
        ["CAR-UN.TO"]  = "Canadian Apartment REIT",    ["DIR-UN.TO"]  = "Dream Industrial REIT",
        ["AP-UN.TO"]   = "Allied Properties REIT",     ["IIP-UN.TO"]  = "InterRent REIT",
        ["HR-UN.TO"]   = "H&R REIT",                   ["BRP.TO"]     = "BRP Inc.",
        ["MDA.TO"]     = "MDA Space",                  ["OTEX.TO"]    = "Open Text",
        ["CSU.TO"]     = "Constellation Software",     ["DSG.TO"]     = "Descartes Systems"
    };

    public RsiScannerService(HttpClient http, ILogger<RsiScannerService> logger, IMarketDataProvider marketData)
    {
        _http = http;
        _logger = logger;
        _marketData = marketData;
    }

    public async Task<ScannerResponse> ScanAsync(IEnumerable<string>? extraSymbols = null, decimal oversoldThreshold = 30m, decimal overboughtThreshold = 75m, string logicMode = "Legacy", CancellationToken ct = default)
    {
        // Merge the default TSX universe with user-provided portfolio/watchlist symbols.
        var symbolsToScan = TsxWatchlist
            .Concat(extraSymbols ?? Enumerable.Empty<string>())
            .Select(s => s.Trim().ToUpperInvariant())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Yahoo Finance requires no API key — go straight to live scan.
        // If Yahoo is unreachable (network error), fall back to demo data.
        try
        {
            _logger.LogInformation("Starting live TSX scan via Yahoo Finance ({Count} symbols, {Extra} from portfolio/watchlist). Oversold<{OS} Overbought>{OB} Mode={Mode}",
                symbolsToScan.Length, symbolsToScan.Length - TsxWatchlist.Length, oversoldThreshold, overboughtThreshold, logicMode);
            return await RunLiveScanAsync(symbolsToScan, oversoldThreshold, overboughtThreshold, logicMode, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TSX scan failed — falling back to demo data.");
            return BuildDemoResponse();
        }
    }

    // ── Live scan ─────────────────────────────────────────────────────────────
    public async Task<List<RsiScanResult>> AnalyzeSymbolsAsync(
        IEnumerable<string> symbols, decimal oversoldThreshold = 30m, decimal overboughtThreshold = 75m, string logicMode = "Legacy", CancellationToken ct = default)
    {
        var results = new List<RsiScanResult>();
        var distinct = symbols
            .Select(s => s.Trim().ToUpperInvariant())
            .Where(s => s.Length > 0)
            .Distinct()
            .ToArray();

        // Batch of 3 with polite delay — same strategy as the main scan
        var batches = distinct
            .Select((sym, i) => new { sym, i })
            .GroupBy(x => x.i / 3)
            .Select(g => g.Select(x => x.sym).ToArray());

        foreach (var batch in batches)
        {
            var tasks = batch.Select(sym => AnalyzeSymbolAsync(sym, oversoldThreshold, overboughtThreshold, logicMode, ct)).ToArray();
            var batchResults = await Task.WhenAll(tasks);
            results.AddRange(batchResults.Where(r => r is not null)!);
            if (distinct.Length > 3) await Task.Delay(1500, ct);
        }

        await EnrichWithQuoteDataAsync(results, ct);
        return results.OrderBy(r => r.Status != SignalStatus.Confirmed ? 1 : 0).ThenBy(r => r.Rsi).ToList();
    }

    private async Task<ScannerResponse> RunLiveScanAsync(string[] symbolsToScan, decimal oversoldThreshold, decimal overboughtThreshold, string logicMode, CancellationToken ct)
    {
        var oversold  = new List<RsiScanResult>();
        var overbought = new List<RsiScanResult>();

        // Yahoo Finance has no hard rate limit; ~2 req/s is courteous.
        // 3 symbols/batch with 1.5s delay → 50 symbols in ~25s.
        var batches = symbolsToScan
            .Select((sym, i) => new { sym, i })
            .GroupBy(x => x.i / 3)
            .Select(g => g.Select(x => x.sym).ToArray());

        foreach (var batch in batches)
        {
            var tasks = batch.Select(sym => AnalyzeSymbolAsync(sym, oversoldThreshold, overboughtThreshold, logicMode, ct)).ToArray();
            var results = await Task.WhenAll(tasks);
            foreach (var r in results.Where(r => r is not null))
            {
                if (r!.ScanType == ScanType.Oversold) oversold.Add(r);
                else if (r.ScanType == ScanType.Overbought) overbought.Add(r);
                // Neutral results are not shown in main TSX scan chains
            }
            await Task.Delay(1500, ct);
        }

        // Enrich with analyst targets and 52-week range in a single batch call
        var allResults = oversold.Concat(overbought).ToList();
        await EnrichWithQuoteDataAsync(allResults, ct);

        return new ScannerResponse
        {
            OversoldChain  = SortResults(oversold, ScanType.Oversold),
            OverboughtChain = SortResults(overbought, ScanType.Overbought),
            ScannedAt = DateTime.UtcNow,
            IsDemo = false,
            Market = "TSX"
        };
    }

    /// <summary>Enrich scan results with analyst target and 52-week range from Yahoo v7/quote,
    /// with a fallback to quoteSummary/financialData for analyst targets (more reliable for TSX).</summary>
    private async Task EnrichWithQuoteDataAsync(List<RsiScanResult> results, CancellationToken ct)
    {
        if (results.Count == 0) return;
        var symbols = results.Select(r => r.Symbol).Distinct().ToList();
        try
        {
            var quotes = await _marketData.GetBatchQuotesAsync(symbols, ct);
            foreach (var r in results)
            {
                if (!quotes.TryGetValue(r.Symbol, out var q)) continue;
                r.Week52High = q.Week52High;
                r.Week52Low  = q.Week52Low;
                if (q.TargetMeanPrice > 0 && r.CurrentPrice > 0)
                {
                    r.AnalystTargetPrice  = Math.Round(q.TargetMeanPrice, 2);
                    r.AnalystTargetUpside = Math.Round((q.TargetMeanPrice - r.CurrentPrice) / r.CurrentPrice * 100m, 1);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EnrichWithQuoteDataAsync (v7) failed — analyst targets may be unavailable");
        }

        // For any result still missing an analyst target, fall back to quoteSummary/financialData.
        // This is the primary data source for TSX stocks where the v7 field is often 0.
        var missingTarget = results.Where(r => r.AnalystTargetPrice == 0).ToList();
        if (missingTarget.Count > 0)
        {
            try
            {
                var targets = await _marketData.GetAnalystTargetsAsync(
                    missingTarget.Select(r => r.Symbol), ct);
                foreach (var r in missingTarget)
                {
                    if (targets.TryGetValue(r.Symbol, out var tp) && tp > 0 && r.CurrentPrice > 0)
                    {
                        r.AnalystTargetPrice  = Math.Round(tp, 2);
                        r.AnalystTargetUpside = Math.Round((tp - r.CurrentPrice) / r.CurrentPrice * 100m, 1);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "EnrichWithQuoteDataAsync (quoteSummary fallback) failed");
            }
        }
    }

    /// <summary>Sort: Confirmed first, then by RSI (ascending for oversold, descending for overbought).</summary>
    private static IReadOnlyList<RsiScanResult> SortResults(List<RsiScanResult> list, ScanType type)
    {
        if (type == ScanType.Oversold)
            return list.OrderBy(r => r.Status != SignalStatus.Confirmed ? 1 : 0)
                       .ThenBy(r => r.Rsi)
                       .ToList();
        return list.OrderBy(r => r.Status != SignalStatus.Confirmed ? 1 : 0)
                   .ThenByDescending(r => r.Rsi)
                   .ToList();
    }

    /// <summary>Fetches candle data with up to 2 retries on HTTP 429 (rate-limited).</summary>
    private async Task<HttpResponseMessage?> FetchWithRetryAsync(string url, CancellationToken ct)
    {
        const int maxRetries = 3;
        int delayMs = 2000;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            var resp = await _http.GetAsync(url, ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Yahoo Finance rate limited (429) on attempt {Attempt}. Waiting {Delay}ms.", attempt + 1, delayMs);
                await Task.Delay(delayMs, ct);
                delayMs *= 2;
                continue;
            }
            return resp;
        }
        _logger.LogError("Yahoo Finance 429 persisted after {MaxRetries} retries for URL: {Url}", maxRetries, url);
        return null;
    }

    private async Task<RsiScanResult?> AnalyzeSymbolAsync(string symbol, decimal oversoldThreshold, decimal overboughtThreshold, string logicMode, CancellationToken ct)
    {
        try
        {
            // Yahoo Finance: 2y of daily candles (enough for 200 DMA + MACD + BB)
            var url  = $"v8/finance/chart/{Uri.EscapeDataString(symbol)}?interval=1d&range=2y";

            var resp = await FetchWithRetryAsync(url, ct);
            if (resp is null || !resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<YahooChartResponse>(json, _json);
            var chartResult = data?.Chart?.Result?.FirstOrDefault();
            if (chartResult is null) return null;

            var qd = chartResult.Indicators?.Quote?.FirstOrDefault();
            if (qd is null) return null;

            // Filter out null slots (non-trading days Yahoo sometimes returns as null)
            var closes  = qd.Close.Where(c => c.HasValue).Select(c => c!.Value).ToList();
            var highs   = qd.High.Where(h => h.HasValue).Select(h => h!.Value).ToList();
            var lows    = qd.Low.Where(l => l.HasValue).Select(l => l!.Value).ToList();
            var volumes = qd.Volume.Where(v => v.HasValue).Select(v => v!.Value).ToList();

            if (closes.Count < 30) return null;

            // ── Core RSI ────────────────────────────────────────────────────
            decimal rsi = CalculateRsi(closes, 14);

            // ── RSI Signal: 9-period EMA of RSI(14) ─────────────────────────
            var (rsiSignalValue, rsiSignalAvailable) = CalculateRsiSignal(closes);

            // ── Volume ratio ────────────────────────────────────────────────
            decimal avgVol = volumes.Count >= 21
                ? volumes.TakeLast(21).SkipLast(1).Select(v => (decimal)v).Average()
                : volumes.Select(v => (decimal)v).Average();
            decimal todayVol   = volumes.Count > 0 ? volumes.Last() : 0L;
            decimal volRatio   = avgVol > 0 ? todayVol / avgVol : 1m;

            var opens = qd.Open.Where(o => o.HasValue).Select(o => o!.Value).ToList();

            // ── Candle data ─────────────────────────────────────────────────
            decimal todayOpen  = opens.Count  > 0 ? opens.Last()   : closes.Last();
            decimal todayHigh  = highs.Last();
            decimal todayLow   = lows.Last();
            decimal todayClose = closes.Last();
            decimal prevHigh   = highs.Count  >= 2 ? highs[^2]  : todayHigh;
            decimal prevLow    = lows.Count   >= 2 ? lows[^2]   : todayLow;
            decimal prevClose  = closes.Count >= 2 ? closes[^2] : todayClose;
            decimal change     = todayClose - prevClose;
            decimal changePct  = prevClose > 0 ? (change / prevClose) * 100m : 0m;
            decimal range      = todayHigh - todayLow;

            // ── Indicator 1: Stochastics ────────────────────────────────────
            decimal stochK = CalculateStochasticK(highs, lows, closes, 14);
            bool stochConfirm = rsi < oversoldThreshold ? stochK < 20 : stochK > 80;

            // ── Indicator 2: MACD ───────────────────────────────────────────
            var (macdVal, macdSig, macdHist, prevMacdHist) = CalculateMacdFull(closes);
            decimal macdHistDelta = macdHist - prevMacdHist;
            // Slope: histogram shrinking toward zero from the negative side → Rising (bullish shift)
            string macdHistSlope = macdHistDelta > 0.0001m ? "Rising"
                                 : macdHistDelta < -0.0001m ? "Falling"
                                 : "Neutral";
            string macdCrossover = DetermineMacdCrossover(macdVal, macdSig, prevMacdHist, macdHist);

            // ── Indicator 3: Bollinger Bands ────────────────────────────────
            var (bbUpper, _, bbLower) = CalculateBollingerBands(closes, 20);
            bool bbBreakout = rsi < oversoldThreshold ? todayClose < bbLower : todayClose > bbUpper;
            string bbPosition = todayClose < bbLower ? "Below Lower"
                              : todayClose > bbUpper ? "Above Upper"
                              : "Inside";

            // ── Indicator 4: Volume Signal ──────────────────────────────────
            string volumeSignal = volRatio >= 1.3m ? "Validated" : volRatio < 0.8m ? "Low-Volume Trap" : "Neutral";

            // ── Indicator 5: 50 / 200 DMA ───────────────────────────────────
            decimal dma50Dev  = 0m;
            decimal dma200Dev = 0m;
            bool has200Dma    = closes.Count >= 200;
            if (closes.Count >= 50)
                dma50Dev  = Math.Round(((todayClose - CalculateSma(closes, 50))  / CalculateSma(closes, 50))  * 100m, 2);
            if (has200Dma)
                dma200Dev = Math.Round(((todayClose - CalculateSma(closes, 200)) / CalculateSma(closes, 200)) * 100m, 2);

            ScanType scanType;
            string probability;
            SignalStatus status;
            string trigger;

            bool enhanced = string.Equals(logicMode, "Enhanced", StringComparison.OrdinalIgnoreCase);

            if (rsi <= oversoldThreshold)
            {
                scanType = ScanType.Oversold;
                probability = enhanced
                    ? CalculateReversalProbabilityEnhanced(rsi, ScanType.Oversold, stochConfirm, macdHistSlope, bbBreakout, volumeSignal)
                    : CalculateReversalProbability(rsi, ScanType.Oversold, stochConfirm, macdCrossover, bbBreakout, volumeSignal);
                (status, trigger) = enhanced
                    ? ClassifyOversoldEnhanced(todayOpen, todayHigh, todayLow, todayClose, prevHigh, range, volRatio, rsi, macdHistSlope, macdHistDelta, bbBreakout)
                    : ClassifyOversold(todayOpen, todayHigh, todayLow, todayClose, prevHigh, range, volRatio, rsi);
            }
            else if (rsi >= overboughtThreshold)
            {
                scanType = ScanType.Overbought;
                probability = enhanced
                    ? CalculateReversalProbabilityEnhanced(rsi, ScanType.Overbought, stochConfirm, macdHistSlope, bbBreakout, volumeSignal)
                    : CalculateReversalProbability(rsi, ScanType.Overbought, stochConfirm, macdCrossover, bbBreakout, volumeSignal);
                (status, trigger) = enhanced
                    ? ClassifyOverboughtEnhanced(todayOpen, todayHigh, todayLow, todayClose, prevLow, range, rsi, macdHistSlope, macdHistDelta, bbBreakout, volRatio)
                    : ClassifyOverbought(todayOpen, todayHigh, todayLow, todayClose, prevLow, range, rsi);
            }
            else
            {
                scanType = ScanType.Neutral;
                probability = "Low";
                status = SignalStatus.Neutral;
                trigger = $"RSI {Math.Round(rsi, 1)} — neutral range ({oversoldThreshold}–{overboughtThreshold}). No directional signal. Technical indicators shown for reference.";
            }

            return new RsiScanResult
            {
                Symbol = symbol,
                CompanyName = CompanyNames.TryGetValue(symbol, out var name) ? name : symbol,
                Rsi = Math.Round(rsi, 2),
                RsiSignal = rsiSignalAvailable ? rsiSignalValue : null,
                RsiSignalAvailable = rsiSignalAvailable,
                CurrentPrice = todayClose,
                Change = Math.Round(change, 2),
                ChangePercent = Math.Round(changePct, 2),
                ScanType = scanType,
                Status = status,
                TriggerDetails = trigger,
                Volume = todayVol,
                VolumeRatio = Math.Round(volRatio, 2),
                StochasticK = Math.Round(stochK, 1),
                StochasticsConfirm = stochConfirm,
                MacdValue = Math.Round(macdVal, 4),
                MacdSignalLine = Math.Round(macdSig, 4),
                MacdCrossover = macdCrossover,
                BollingerBreakout = bbBreakout,
                BollingerPosition = bbPosition,
                VolumeSignal = volumeSignal,
                Dma50Deviation = dma50Dev,
                Dma200Deviation = dma200Dev,
                Has200Dma = has200Dma,
                ReversalProbability = probability,
                MacdHistogram = Math.Round(macdHist, 4),
                MacdHistDelta = Math.Round(macdHistDelta, 4),
                MacdHistSlope = macdHistSlope,
                LogicMode = enhanced ? "Enhanced" : "Legacy",
                IsDemo = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to analyze {Symbol}", symbol);
        }

        return null;
    }

    // ── RSI full series (Wilder's smoothed method) ───────────────────────────
    /// <summary>Returns the complete RSI series for use in further calculations (e.g. RSI Signal EMA).</summary>
    private static List<decimal> CalculateRsiSeries(List<decimal> closes, int period)
    {
        var result = new List<decimal>();
        if (closes.Count < period + 1) return result;

        decimal avgGain = 0, avgLoss = 0;
        for (int i = 1; i <= period; i++)
        {
            var diff = closes[i] - closes[i - 1];
            if (diff >= 0) avgGain += diff; else avgLoss -= diff;
        }
        avgGain /= period;
        avgLoss /= period;

        decimal rs = avgLoss == 0 ? 100m : avgGain / avgLoss;
        result.Add(100m - (100m / (1m + rs)));

        for (int i = period + 1; i < closes.Count; i++)
        {
            var diff = closes[i] - closes[i - 1];
            decimal gain = diff >= 0 ? diff : 0;
            decimal loss = diff < 0 ? -diff : 0;
            avgGain = (avgGain * (period - 1) + gain) / period;
            avgLoss = (avgLoss * (period - 1) + loss) / period;
            rs = avgLoss == 0 ? 100m : avgGain / avgLoss;
            result.Add(100m - (100m / (1m + rs)));
        }

        return result;
    }

    /// <summary>Calculates the RSI Signal: a 9-period EMA of the RSI(14) series.
    /// Returns (value, available=true) or (0, available=false) when data is insufficient.</summary>
    private static (decimal value, bool available) CalculateRsiSignal(List<decimal> closes, int rsiPeriod = 14, int emaPeriod = 9)
    {
        var rsiSeries = CalculateRsiSeries(closes, rsiPeriod);
        if (rsiSeries.Count < emaPeriod) return (0m, false);

        decimal mult = 2m / (emaPeriod + 1);                        // multiplier = 0.2 for period=9
        decimal ema  = rsiSeries.Take(emaPeriod).Average();          // seed: simple average of first 9 RSI values
        for (int i = emaPeriod; i < rsiSeries.Count; i++)
            ema = rsiSeries[i] * mult + ema * (1m - mult);

        return (Math.Round(ema, 2), true);
    }

    // ── RSI Calculation (Wilder's smoothed method) ────────────────────────────
    private static decimal CalculateRsi(List<decimal> closes, int period)
    {
        if (closes.Count < period + 1) return 50m;

        decimal avgGain = 0, avgLoss = 0;
        for (int i = 1; i <= period; i++)
        {
            var diff = closes[i] - closes[i - 1];
            if (diff >= 0) avgGain += diff; else avgLoss -= diff;
        }
        avgGain /= period;
        avgLoss /= period;

        for (int i = period + 1; i < closes.Count; i++)
        {
            var diff = closes[i] - closes[i - 1];
            decimal gain = diff >= 0 ? diff : 0;
            decimal loss = diff < 0 ? -diff : 0;
            avgGain = (avgGain * (period - 1) + gain) / period;
            avgLoss = (avgLoss * (period - 1) + loss) / period;
        }

        if (avgLoss == 0) return 100m;
        decimal rs = avgGain / avgLoss;
        return 100m - (100m / (1m + rs));
    }

    // ── Stochastic Fast %K ────────────────────────────────────────────────────
    private static decimal CalculateStochasticK(
        List<decimal> highs, List<decimal> lows, List<decimal> closes, int period = 14)
    {
        if (closes.Count < period) return 50m;
        decimal highestHigh = highs.TakeLast(period).Max();
        decimal lowestLow   = lows.TakeLast(period).Min();
        decimal rng = highestHigh - lowestLow;
        return rng > 0 ? ((closes.Last() - lowestLow) / rng) * 100m : 50m;
    }

    // ── EMA helper ────────────────────────────────────────────────────────────
    private static decimal CalculateEma(IReadOnlyList<decimal> values, int period)
    {
        if (values.Count < period) return values[^1];
        decimal mult = 2m / (period + 1);
        decimal ema  = values.Take(period).Average();
        for (int i = period; i < values.Count; i++)
            ema = values[i] * mult + ema * (1 - mult);
        return ema;
    }

    // ── MACD (12,26,9) — Full with histogram series ───────────────────────────
    private static (decimal macd, decimal signal, decimal hist, decimal prevHist) CalculateMacdFull(List<decimal> closes)
    {
        if (closes.Count < 35) return (0m, 0m, 0m, 0m);

        decimal mult12 = 2m / 13m, mult26 = 2m / 27m;
        decimal ema12  = closes.Take(12).Average();
        decimal ema26  = closes.Take(26).Average();
        var macdLine   = new List<decimal>();

        for (int i = 12; i < closes.Count; i++)
        {
            ema12 = closes[i] * mult12 + ema12 * (1 - mult12);
            if (i >= 25)
            {
                ema26 = closes[i] * mult26 + ema26 * (1 - mult26);
                macdLine.Add(ema12 - ema26);
            }
        }

        if (macdLine.Count < 9) return (macdLine[^1], macdLine[^1], 0m, 0m);

        decimal sigMult   = 2m / 10m;
        decimal sigLine   = macdLine.Take(9).Average();
        var     histLine  = new List<decimal>();

        for (int i = 9; i < macdLine.Count; i++)
        {
            sigLine = macdLine[i] * sigMult + sigLine * (1 - sigMult);
            histLine.Add(macdLine[i] - sigLine);
        }

        decimal hist     = histLine.Count > 0 ? histLine[^1] : 0m;
        decimal prevHist = histLine.Count > 1 ? histLine[^2] : hist;

        return (macdLine[^1], sigLine, hist, prevHist);
    }

    // Legacy wrapper (kept for backward compat)
    private static (decimal macd, decimal signal) CalculateMacd(List<decimal> closes)
    {
        var (macd, sig, _, _) = CalculateMacdFull(closes);
        return (macd, sig);
    }

    /// <summary>Detects whether MACD recently crossed above/below its signal line.
    /// Uses histogram sign-change as a more direct indicator than line re-computation.</summary>
    private static string DetermineMacdCrossover(decimal macdNow, decimal sigNow, decimal prevHist, decimal currHist)
    {
        // Fresh crossover: histogram changed sign this bar
        bool crossedBullish = prevHist < 0 && currHist >= 0;
        bool crossedBearish = prevHist > 0 && currHist <= 0;

        if (crossedBullish) return "Bullish";
        if (crossedBearish) return "Bearish";

        // Fallback: current position of lines
        if (macdNow > sigNow) return "Bullish";
        if (macdNow < sigNow) return "Bearish";
        return "Neutral";
    }

    // ── Bollinger Bands (20, ±2σ) ─────────────────────────────────────────────
    private static (decimal upper, decimal middle, decimal lower) CalculateBollingerBands(
        List<decimal> closes, int period = 20)
    {
        if (closes.Count < period) return (0m, 0m, 0m);
        var recent  = closes.TakeLast(period).ToList();
        decimal sma = recent.Average();
        decimal variance = recent.Select(c => (c - sma) * (c - sma)).Average();
        decimal stdDev   = (decimal)Math.Sqrt((double)variance);
        return (sma + 2 * stdDev, sma, sma - 2 * stdDev);
    }

    // ── Simple Moving Average ─────────────────────────────────────────────────
    private static decimal CalculateSma(List<decimal> values, int period)
        => values.Count >= period ? values.TakeLast(period).Average() : values.Average();

    // ── Reversal Probability ──────────────────────────────────────────────────
    private static string CalculateReversalProbability(
        decimal rsi, ScanType scanType, bool stochConfirm,
        string macdCrossover, bool bbBreakout, string volumeSignal)
    {
        int score = 0;
        if (scanType == ScanType.Oversold)
        {
            if (rsi < 25) score++;
            if (stochConfirm) score++;
            if (macdCrossover == "Bullish") score++;
            if (bbBreakout) score++;
            if (volumeSignal == "Validated") score++;
        }
        else
        {
            if (rsi > 80) score++;
            if (stochConfirm) score++;
            if (macdCrossover == "Bearish") score++;
            if (bbBreakout) score++;
            if (volumeSignal == "Validated") score++;
        }

        return score switch { >= 4 => "High", >= 2 => "Medium", _ => "Low" };
    }

    // ── Enhanced Reversal Probability (uses histogram slope instead of crossover) ──
    private static string CalculateReversalProbabilityEnhanced(
        decimal rsi, ScanType scanType, bool stochConfirm,
        string macdHistSlope, bool bbBreakout, string volumeSignal)
    {
        int score = 0;
        if (scanType == ScanType.Oversold)
        {
            if (rsi < 25) score++;
            if (stochConfirm) score++;
            if (macdHistSlope == "Rising") score++;   // histogram shrinking from negative side
            if (bbBreakout) score++;
            if (volumeSignal == "Validated") score++;
        }
        else
        {
            if (rsi > 80) score++;
            if (stochConfirm) score++;
            if (macdHistSlope == "Falling") score++;  // histogram shrinking from positive side
            if (bbBreakout) score++;
            if (volumeSignal == "Validated") score++;
        }

        return score switch { >= 4 => "High", >= 2 => "Medium", _ => "Low" };
    }

    // ── Signal classification helpers ─────────────────────────────────────────
    private static (SignalStatus status, string trigger) ClassifyOversold(
        decimal open, decimal high, decimal low, decimal close,
        decimal prevHigh, decimal range, decimal volRatio, decimal rsi)
    {
        bool brokeAbovePrevHigh = close > prevHigh;
        bool strongBullish = range > 0 && (close - low) / range >= 0.75m;
        bool gapDownReversal = open < low * 1.005m && close > open * 1.01m;

        if (brokeAbovePrevHigh && volRatio >= 1.3m)
            return (SignalStatus.Confirmed, "Trigger 1 – Break of prior day's high on elevated volume (" + volRatio.ToString("0.0") + "x avg)");
        if (strongBullish)
            return (SignalStatus.Confirmed, "Trigger 2 – Wide-range bullish candle closing at " + ((close - low) / range * 100m).ToString("0") + "% of day's range");
        if (gapDownReversal)
            return (SignalStatus.Confirmed, "Trigger 3 – Gap-down open followed by aggressive intraday reversal");
        if (rsi < 25)
            return (SignalStatus.EarlyWarning, "Extreme oversold (RSI " + rsi.ToString("0.0") + ") – momentum decelerating, awaiting pivot confirmation");

        return (SignalStatus.EarlyWarning, "RSI below 30 – downside momentum slowing, no price-action trigger yet");
    }

    private static (SignalStatus status, string trigger) ClassifyOverbought(
        decimal open, decimal high, decimal low, decimal close,
        decimal prevLow, decimal range, decimal rsi)
    {
        bool brokeBelowPrevLow = close < prevLow;
        bool strongBearish = range > 0 && (high - close) / range >= 0.75m;
        bool gapUpReversal = open > high * 0.995m && close < open * 0.99m;

        if (brokeBelowPrevLow)
            return (SignalStatus.Confirmed, "Trigger 1 – Break of prior day's low; institutional distribution confirmed");
        if (strongBearish)
            return (SignalStatus.Confirmed, "Trigger 2 – Wide-range bearish candle closing at " + ((high - close) / range * 100m).ToString("0") + "% from top");
        if (gapUpReversal)
            return (SignalStatus.Confirmed, "Trigger 3 – Gap-up open followed by immediate reversal; buying exhaustion");
        if (rsi > 80)
            return (SignalStatus.EarlyWarning, "Extreme overbought (RSI " + rsi.ToString("0.0") + ") – parabolic extension, velocity flattening");

        return (SignalStatus.EarlyWarning, "RSI above 75 – buying velocity decelerating, no distribution trigger yet");
    }

    // ── Enhanced Signal Classification — Strict State Machine ─────────────────
    /// <summary>
    /// Enhanced Oversold — EarlyWarning is the default price-discovery phase.
    /// Confirmed is ONLY issued when the daily candle closes with structural
    /// absorption: close above the session midpoint (buyers defended price) AND
    /// at least one of: volume validates OR histogram slope has turned positive
    /// (internal momentum shift before MACD lines cross).
    /// </summary>
    private static (SignalStatus status, string trigger) ClassifyOversoldEnhanced(
        decimal open, decimal high, decimal low, decimal close,
        decimal prevHigh, decimal range, decimal volRatio, decimal rsi,
        string macdHistSlope, decimal macdHistDelta, bool bbBreakout)
    {
        decimal midpoint   = (high + low) / 2m;
        bool bullishClose  = close > midpoint;                          // candle closed in upper half
        bool histRising    = macdHistSlope == "Rising";                 // Δhist > 0: selling pressure easing
        bool volumeValid   = volRatio >= 1.3m;
        bool structuralAbs = bullishClose && (volumeValid || histRising);

        if (structuralAbs && close > prevHigh)
            return (SignalStatus.Confirmed,
                $"Candle closed above prior day's high with bullish structure. " +
                $"Hist Δ={macdHistDelta:+0.0000;-0.0000} ({macdHistSlope}), Vol={volRatio:0.0}x. " +
                "Selling pressure absorbed; execution phase.");

        if (structuralAbs)
            return (SignalStatus.Confirmed,
                $"Candle closed in upper half of range ({((close - low) / range * 100m):0}%) with momentum shift. " +
                $"Hist Δ={macdHistDelta:+0.0000;-0.0000} ({macdHistSlope}), Vol={volRatio:0.0}x. " +
                "Structural absorption of selling pressure validated.");

        if (bbBreakout && histRising)
            return (SignalStatus.EarlyWarning,
                $"Bollinger breakout with histogram slope turning positive (Δ={macdHistDelta:+0.0000;-0.0000}). " +
                "Price discovery phase: volatility bands pierced, momentum shifting. Awaiting candle close confirmation.");

        if (rsi < 25 && histRising)
            return (SignalStatus.EarlyWarning,
                $"Extreme RSI ({rsi:0.0}) with histogram internally reversing (Δ={macdHistDelta:+0.0000;-0.0000}). " +
                "Momentum has shifted days before MACD crossover. Capital not deployed until candle close confirms.");

        string histNote = macdHistSlope == "Falling"
            ? $"Hist slope still falling (Δ={macdHistDelta:+0.0000;-0.0000}) — selling momentum intact."
            : $"Hist neutral (Δ={macdHistDelta:+0.0000;-0.0000}).";
        return (SignalStatus.EarlyWarning,
            $"RSI {rsi:0.0} below oversold threshold. {histNote} " +
            "No structural absorption detected. Waiting for candle close to validate.");
    }

    /// <summary>
    /// Enhanced Overbought — Confirmed ONLY when the daily candle closes with structural
    /// distribution: close below the session midpoint (sellers defended price) AND
    /// at least one of: volume validates OR histogram slope has turned negative.
    /// </summary>
    private static (SignalStatus status, string trigger) ClassifyOverboughtEnhanced(
        decimal open, decimal high, decimal low, decimal close,
        decimal prevLow, decimal range, decimal rsi,
        string macdHistSlope, decimal macdHistDelta, bool bbBreakout, decimal volRatio)
    {
        decimal midpoint    = (high + low) / 2m;
        bool bearishClose   = close < midpoint;                          // candle closed in lower half
        bool histFalling    = macdHistSlope == "Falling";                // Δhist < 0: buying pressure waning
        bool volumeValid    = volRatio >= 1.3m;
        bool structuralDist = bearishClose && (volumeValid || histFalling);

        if (structuralDist && close < prevLow)
            return (SignalStatus.Confirmed,
                $"Candle closed below prior day's low with bearish structure. " +
                $"Hist Δ={macdHistDelta:+0.0000;-0.0000} ({macdHistSlope}), Vol={volRatio:0.0}x. " +
                "Institutional distribution confirmed; execution phase.");

        if (structuralDist)
            return (SignalStatus.Confirmed,
                $"Candle closed in lower half of range ({((high - close) / range * 100m):0}% from top) with distribution signal. " +
                $"Hist Δ={macdHistDelta:+0.0000;-0.0000} ({macdHistSlope}), Vol={volRatio:0.0}x. " +
                "Structural distribution of buying pressure validated.");

        if (bbBreakout && histFalling)
            return (SignalStatus.EarlyWarning,
                $"Extended above upper Bollinger Band with histogram slope turning negative (Δ={macdHistDelta:+0.0000;-0.0000}). " +
                "Price discovery phase: parabolic extension, internal momentum waning. Awaiting candle close confirmation.");

        if (rsi > 80 && histFalling)
            return (SignalStatus.EarlyWarning,
                $"Extreme RSI ({rsi:0.0}) with histogram internally reversing (Δ={macdHistDelta:+0.0000;-0.0000}). " +
                "Buying velocity decelerating before MACD crossover. Capital not deployed until candle close confirms.");

        string histNote = macdHistSlope == "Rising"
            ? $"Hist slope still rising (Δ={macdHistDelta:+0.0000;-0.0000}) — buying momentum intact."
            : $"Hist neutral (Δ={macdHistDelta:+0.0000;-0.0000}).";
        return (SignalStatus.EarlyWarning,
            $"RSI {rsi:0.0} above overbought threshold. {histNote} " +
            "No structural distribution detected. Waiting for candle close to validate.");
    }

    // ── Demo data (shown when no API key) ─────────────────────────────────────
    private static ScannerResponse BuildDemoResponse() => new()
    {
        OversoldChain =
        [
            new RsiScanResult
            {
                Symbol = "AP-UN.TO", CompanyName = "Allied Properties REIT",
                Rsi = 22.15m, CurrentPrice = 14.88m, Change = -0.62m, ChangePercent = -4.00m,
                ScanType = ScanType.Oversold, Status = SignalStatus.Confirmed, Sector = "Real Estate",
                Volume = 2_200_000, VolumeRatio = 2.1m, IsDemo = true,
                TriggerDetails = "Trigger 1 – Break of prior day's high on elevated volume (2.1x avg)",
                StochasticK = 9.8m, StochasticsConfirm = true,
                MacdValue = -0.124m, MacdSignalLine = -0.091m, MacdCrossover = "Neutral",
                BollingerBreakout = true, BollingerPosition = "Below Lower",
                VolumeSignal = "Validated",
                Dma50Deviation = -16.2m, Dma200Deviation = -24.1m, Has200Dma = true,
                ReversalProbability = "High"
            },
            new RsiScanResult
            {
                Symbol = "BRP.TO", CompanyName = "BRP Inc.",
                Rsi = 25.30m, CurrentPrice = 42.50m, Change = -1.80m, ChangePercent = -4.06m,
                ScanType = ScanType.Oversold, Status = SignalStatus.EarlyWarning, Sector = "Consumer Discretionary",
                Volume = 750_000, VolumeRatio = 0.9m, IsDemo = true,
                TriggerDetails = "Inside-bar profile below lower Bollinger Band – awaiting breakout above prior day's high",
                StochasticK = 17.3m, StochasticsConfirm = true,
                MacdValue = -0.310m, MacdSignalLine = -0.265m, MacdCrossover = "Bearish",
                BollingerBreakout = true, BollingerPosition = "Below Lower",
                VolumeSignal = "Low-Volume Trap",
                Dma50Deviation = -14.8m, Dma200Deviation = -20.5m, Has200Dma = true,
                ReversalProbability = "Medium"
            },
            new RsiScanResult
            {
                Symbol = "IIP-UN.TO", CompanyName = "InterRent REIT",
                Rsi = 28.90m, CurrentPrice = 9.41m, Change = -0.22m, ChangePercent = -2.28m,
                ScanType = ScanType.Oversold, Status = SignalStatus.EarlyWarning, Sector = "Real Estate",
                Volume = 980_000, VolumeRatio = 1.1m, IsDemo = true,
                TriggerDetails = "Momentum flattening near multi-year structural support – no price-action trigger yet",
                StochasticK = 22.5m, StochasticsConfirm = false,
                MacdValue = -0.041m, MacdSignalLine = -0.038m, MacdCrossover = "Neutral",
                BollingerBreakout = false, BollingerPosition = "Inside",
                VolumeSignal = "Neutral",
                Dma50Deviation = -8.1m, Dma200Deviation = -11.3m, Has200Dma = true,
                ReversalProbability = "Low"
            },
            new RsiScanResult
            {
                Symbol = "DIR-UN.TO", CompanyName = "Dream Industrial REIT",
                Rsi = 26.40m, CurrentPrice = 11.24m, Change = -0.38m, ChangePercent = -3.27m,
                ScanType = ScanType.Oversold, Status = SignalStatus.Confirmed, Sector = "Real Estate",
                Volume = 1_640_000, VolumeRatio = 1.6m, IsDemo = true,
                TriggerDetails = "Trigger 3 – Gap-down open followed by aggressive intraday reversal",
                StochasticK = 14.2m, StochasticsConfirm = true,
                MacdValue = -0.082m, MacdSignalLine = -0.051m, MacdCrossover = "Neutral",
                BollingerBreakout = true, BollingerPosition = "Below Lower",
                VolumeSignal = "Validated",
                Dma50Deviation = -12.4m, Dma200Deviation = -18.7m, Has200Dma = true,
                ReversalProbability = "High"
            }
        ],
        OverboughtChain =
        [
            new RsiScanResult
            {
                Symbol = "TFII.TO", CompanyName = "TFI International",
                Rsi = 78.45m, CurrentPrice = 194.30m, Change = -2.10m, ChangePercent = -1.07m,
                ScanType = ScanType.Overbought, Status = SignalStatus.Confirmed, Sector = "Industrials",
                Volume = 1_100_000, VolumeRatio = 1.4m, IsDemo = true,
                TriggerDetails = "Trigger 1 – Break of prior day's low on high volume; institutional distribution active",
                StochasticK = 83.6m, StochasticsConfirm = true,
                MacdValue = 0.218m, MacdSignalLine = 0.183m, MacdCrossover = "Bearish",
                BollingerBreakout = true, BollingerPosition = "Above Upper",
                VolumeSignal = "Validated",
                Dma50Deviation = 11.2m, Dma200Deviation = 17.4m, Has200Dma = true,
                ReversalProbability = "High"
            },
            new RsiScanResult
            {
                Symbol = "RY.TO", CompanyName = "Royal Bank of Canada",
                Rsi = 76.10m, CurrentPrice = 178.60m, Change = -1.40m, ChangePercent = -0.78m,
                ScanType = ScanType.Overbought, Status = SignalStatus.Confirmed, Sector = "Financials",
                Volume = 3_800_000, VolumeRatio = 1.3m, IsDemo = true,
                TriggerDetails = "Trigger 2 – Wide-range bearish engulfing candle closing at 4% from session high",
                StochasticK = 81.4m, StochasticsConfirm = true,
                MacdValue = 0.312m, MacdSignalLine = 0.295m, MacdCrossover = "Neutral",
                BollingerBreakout = false, BollingerPosition = "Inside",
                VolumeSignal = "Validated",
                Dma50Deviation = 8.6m, Dma200Deviation = 13.1m, Has200Dma = true,
                ReversalProbability = "Medium"
            },
            new RsiScanResult
            {
                Symbol = "GWO.TO", CompanyName = "Great-West Lifeco",
                Rsi = 77.90m, CurrentPrice = 52.18m, Change = 0.28m, ChangePercent = 0.54m,
                ScanType = ScanType.Overbought, Status = SignalStatus.EarlyWarning, Sector = "Financials",
                Volume = 820_000, VolumeRatio = 0.8m, IsDemo = true,
                TriggerDetails = "Marginal new 52-wk high, but RSI momentum diverging – unconfirmed bearish divergence",
                StochasticK = 76.2m, StochasticsConfirm = false,
                MacdValue = 0.091m, MacdSignalLine = 0.097m, MacdCrossover = "Neutral",
                BollingerBreakout = false, BollingerPosition = "Inside",
                VolumeSignal = "Low-Volume Trap",
                Dma50Deviation = 6.3m, Dma200Deviation = 9.8m, Has200Dma = true,
                ReversalProbability = "Low"
            },
            new RsiScanResult
            {
                Symbol = "ALA.TO", CompanyName = "AltaGas",
                Rsi = 75.80m, CurrentPrice = 34.75m, Change = 0.45m, ChangePercent = 1.31m,
                ScanType = ScanType.Overbought, Status = SignalStatus.EarlyWarning, Sector = "Utilities",
                Volume = 610_000, VolumeRatio = 0.7m, IsDemo = true,
                TriggerDetails = "Extended above upper Bollinger Band – volume thinning on higher ticks; mean-reversion risk",
                StochasticK = 79.1m, StochasticsConfirm = false,
                MacdValue = 0.055m, MacdSignalLine = 0.048m, MacdCrossover = "Neutral",
                BollingerBreakout = true, BollingerPosition = "Above Upper",
                VolumeSignal = "Low-Volume Trap",
                Dma50Deviation = 9.4m, Dma200Deviation = 14.2m, Has200Dma = true,
                ReversalProbability = "Medium"
            }
        ],
        ScannedAt = DateTime.UtcNow,
        IsDemo = true,
        Market = "TSX (Demo)"
    };
}
