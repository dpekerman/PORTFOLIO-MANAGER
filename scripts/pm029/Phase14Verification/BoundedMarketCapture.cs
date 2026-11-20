using System.Collections.Immutable;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.Opportunities;

internal static class BoundedMarketCapture
{
    private static readonly string[] Symbols = ["NPI.TO", "NVDA", "SMH"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public static async Task<int> RunAsync(string scorerIdentity, string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scorerIdentity);
        if (Directory.Exists(outputDirectory) || File.Exists(outputDirectory))
            throw new IOException("Capture output must be a new directory.");
        Directory.CreateDirectory(outputDirectory);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        using var handler = new PublicResponseCapture(outputDirectory);
        using var http = new HttpClient(handler) { BaseAddress = new("https://query1.finance.yahoo.com/"), Timeout = TimeSpan.FromSeconds(45) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 PortfolioManager-PM029-ReadOnly");
        var market = new YahooFinanceService(http, new YahooCrumbService(NullLogger<YahooCrumbService>.Instance),
            NullLogger<YahooFinanceService>.Instance);
        var scanner = new RsiScannerService(http, NullLogger<RsiScannerService>.Instance, market, new ScannerRuntimeConfig(),
            Deny<IStagedSignalService>(), new ChannelAnalysisService(), Deny<ITechnicalChannelPersistenceService>(), Deny<ISecurityAnalysisResolver>());
        var listings = ImmutableArray.CreateBuilder<SwingReplayListing>();
        foreach (var symbol in Symbols)
        {
            var scan = await scanner.AnalyzeSymbolAsync(symbol, 30m, 75m, "Enhanced", deadline.Token)
                ?? throw new InvalidDataException($"Actual scanner rejected {symbol}; no evidence was fabricated.");
            var chart = handler.Captures.Last(capture => capture.Path.EndsWith("/" + symbol, StringComparison.Ordinal)
                && capture.Path.Contains("/chart/", StringComparison.Ordinal));
            using var document = JsonDocument.Parse(File.ReadAllBytes(chart.File));
            var result = document.RootElement.GetProperty("chart").GetProperty("result")[0];
            var meta = result.GetProperty("meta");
            if (meta.GetProperty("symbol").GetString() != symbol || scan.IntradayRangeReconstructed)
                throw new InvalidDataException($"{symbol}: listing identity or original daily OHLC cannot be verified.");
            var currency = meta.GetProperty("currency").GetString() ?? throw new InvalidDataException("Currency is absent.");
            var exchange = meta.GetProperty("exchangeName").GetString() ?? throw new InvalidDataException("Exchange is absent.");
            var instrument = meta.GetProperty("instrumentType").GetString() ?? throw new InvalidDataException("Instrument type is absent.");
            var analysisMarket = currency == "CAD" && exchange == "TOR" ? "TSX"
                : currency == "USD" && exchange is "NMS" or "NGM" or "NCM" or "NYQ" or "ASE" or "PCX" ? "US"
                : throw new InvalidDataException("This bounded capture does not support the returned market.");
            var history = ReadOriginalBars(result);
            scan.AnalysisTicker = symbol;
            scan.AnalysisMarket = analysisMarket;
            scan.AnalysisCurrency = currency;
            scan.InstrumentType = instrument;
            var expected = TradingCalendar.LastCompletedSession(DateTime.UtcNow, TradingCalendar.ExchangeFor(symbol, analysisMarket));
            if (scan.TradingDate != expected || history[^1].Date != expected || scan.CurrentPrice != history[^1].Close)
                throw new InvalidDataException($"{symbol}: the scan and raw chart do not share the completed session.");
            var quote = await market.GetQuoteAsync(symbol, deadline.Token);
            await scanner.EnrichWithQuoteDataAsync([scan], deadline.Token);
            var snapshot = instrument == "EQUITY" ? await market.GetFundamentalsAsync(symbol, deadline.Token) : null;
            ValueScreenerResult? fundamentals = null;
            if (instrument == "EQUITY")
            {
                if (snapshot is null || quote is null)
                    Console.Error.WriteLine($"{symbol}: fundamentals/quote acquisition is incomplete; Strategic remains evidence-limited.");
                else
                    fundamentals = ValueScreenerService.BuildResult(symbol, ValueOrigin.Watchlist, quote, snapshot, scan,
                        NullLogger<ValueScreenerService>.Instance);
            }
            var available = DateTimeOffset.UtcNow;
            listings.Add(new(symbol + ":" + exchange, symbol, exchange, currency, expected,
                JsonSerializer.Serialize(scan, Json), "Yahoo original daily chart + actual RsiScannerService", available,
                null, false, null, null, "Yahoo original daily chart", chart.AvailableAtUtc,
                history.ToImmutableArray(), [], "Yahoo chart metadata", chart.AvailableAtUtc,
                fundamentals, instrument, fundamentals is null ? null : "Yahoo captured quote and quoteSummary",
                fundamentals is null ? null : available));
            await Task.Delay(300, deadline.Token);
        }
        var package = new SwingReplayPackage("VerifiedReal", scorerIdentity, DateTimeOffset.UtcNow, listings.Count, listings.ToImmutable());
        var runner = new SwingReplayRunner();
        var evaluation = JsonSerializer.Serialize(runner.Evaluate(package), Json);
        if (evaluation != JsonSerializer.Serialize(runner.Evaluate(package), Json))
            throw new InvalidOperationException("Actual replay is not deterministic.");
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "replay-input.json"), JsonSerializer.Serialize(package, Json), deadline.Token);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "replay-result.json"), evaluation, deadline.Token);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "source-manifest.json"),
            JsonSerializer.Serialize(handler.Captures.Select(capture => capture with { File = Path.GetFileName(capture.File) }), Json), deadline.Token);
        Console.WriteLine("Captured exactly NPI.TO, NVDA and SMH. No database, resolver, staged-signal service, persistence service, refresh or API host was used.");
        return 0;
    }

    private static MarketDailyClose[] ReadOriginalBars(JsonElement result)
    {
        var times = result.GetProperty("timestamp");
        var quote = result.GetProperty("indicators").GetProperty("quote")[0];
        foreach (var field in new[] { "open", "high", "low", "close", "volume" })
            if (quote.GetProperty(field).GetArrayLength() != times.GetArrayLength()
                || quote.GetProperty(field).EnumerateArray().Any(value => value.ValueKind != JsonValueKind.Number))
                throw new InvalidDataException($"Original {field} observations are incomplete.");
        return times.EnumerateArray().Select((time, index) => new MarketDailyClose(
            TradingCalendar.TorontoDate(DateTimeOffset.FromUnixTimeSeconds(time.GetInt64()).UtcDateTime),
            quote.GetProperty("close")[index].GetDecimal(), quote.GetProperty("open")[index].GetDecimal(),
            quote.GetProperty("high")[index].GetDecimal(), quote.GetProperty("low")[index].GetDecimal(),
            quote.GetProperty("volume")[index].GetInt64(), true, result.GetProperty("meta").GetProperty("currency").GetString())).ToArray();
    }

    private static T Deny<T>() where T : class => DispatchProxy.Create<T, ForbiddenCaptureService>();
}

public class ForbiddenCaptureService : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        throw new InvalidOperationException($"Forbidden capture dependency was invoked: {targetMethod?.Name}");
}

internal sealed record CapturedResponse(string Path, string File, int Status, DateTimeOffset AvailableAtUtc, string Sha256);

internal sealed class PublicResponseCapture(string directory) : DelegatingHandler(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
{
    public List<CapturedResponse> Captures { get; } = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? throw new InvalidOperationException("Capture URI is absent.");
        if (!path.Contains("/finance/chart/", StringComparison.Ordinal) && !path.Contains("/finance/quote", StringComparison.Ordinal))
            throw new InvalidOperationException("Only public market responses may be captured.");
        var response = await base.SendAsync(request, cancellationToken);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var file = Path.Combine(directory, $"public-response-{Captures.Count + 1:D2}.json");
        await File.WriteAllBytesAsync(file, bytes, cancellationToken);
        Captures.Add(new(path, file, (int)response.StatusCode, DateTimeOffset.UtcNow, Convert.ToHexString(SHA256.HashData(bytes))));
        return response;
    }
}
