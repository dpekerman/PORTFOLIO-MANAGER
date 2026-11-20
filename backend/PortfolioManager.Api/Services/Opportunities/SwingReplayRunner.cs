using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.Opportunities;

public sealed record SwingReplayListing(
    string ListingId, string Symbol, string ListingExchange, string ListingCurrency,
    DateOnly ExpectedSession, string ScanJson, string ScanSource,
    DateTimeOffset EvidenceAvailableAtUtc,
    SectorBenchmarkInfo? Benchmark, bool BenchmarkVerified,
    string? MappingSource, DateTimeOffset? MappingAvailableAtUtc,
    string? HistorySource, DateTimeOffset? HistoryAvailableAtUtc,
    ImmutableArray<MarketDailyClose> AnalysisHistory,
    ImmutableArray<MarketDailyClose> BenchmarkHistory,
    string ListingIdentitySource, DateTimeOffset IdentityAvailableAtUtc,
    ValueScreenerResult? Fundamentals = null, string? InstrumentType = null,
    string? FundamentalsSource = null, DateTimeOffset? FundamentalsAvailableAtUtc = null);

public sealed record SwingReplayPackage(
    string DatasetKind, string ScorerCommit, DateTimeOffset AsOfUtc,
    int ExpectedUniqueListings, ImmutableArray<SwingReplayListing> Listings);

public sealed record SwingReplayRow(
    string ListingId, string Symbol, ScoreResult Score,
    SectorRelativeResult? Sector, TradeSetupResult Setup,
    ImmutableArray<string> PackageIssueCodes,
    ScoreResult? Strategic = null, SetupQualityResult? SetupQuality = null);

public sealed record SwingReplayResult(
    string DatasetKind, string ScorerCommit, DateTimeOffset AsOfUtc,
    int UniqueListings, int ScoredListings,
    IReadOnlyDictionary<string, int> StatusCounts,
    ImmutableArray<SwingReplayRow> Rows);

public interface ISwingReplayRunner
{
    SwingReplayResult Evaluate(SwingReplayPackage package);
}

public sealed class SwingReplayRunner : ISwingReplayRunner
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private static readonly string[] RequiredScanFields =
    [
        "symbol", "analysisTicker", "analysisMarket", "analysisCurrency", "usesUnderlyingSecurity",
        "tradingDate", "scannedAt", "currentPrice", "rsi", "rsiSignal", "rsiSignalAvailable",
        "macdHistogram", "macdHistDelta", "ema20Price", "sma50Price", "sma200", "has200Dma",
        "volume", "volumeRatio", "volumeSignal", "changePercent", "dailyAtr", "priceStructure",
        "chaseRisk", "fibZone"
    ];

    public SwingReplayResult Evaluate(SwingReplayPackage package)
    {
        if (package.AsOfUtc.Offset != TimeSpan.Zero || string.IsNullOrWhiteSpace(package.ScorerCommit)
            || package.DatasetKind is not ("Synthetic" or "VerifiedReal"))
            throw new ArgumentException("Replay requires a UTC cutoff, scorer commit and explicit dataset kind.", nameof(package));
        if (package.Listings.IsDefaultOrEmpty || package.ExpectedUniqueListings != package.Listings.Length)
            throw new ArgumentException("Replay membership does not match its declared unique listing count.", nameof(package));
        if (package.Listings.Any(row => string.IsNullOrWhiteSpace(row.ListingId) || string.IsNullOrWhiteSpace(row.Symbol))
            || package.Listings.Select(row => row.ListingId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != package.Listings.Length
            || package.Listings.Select(row => (row.Symbol.Trim().ToUpperInvariant(), row.ListingExchange))
                .Distinct().Count() != package.Listings.Length)
            throw new ArgumentException("Replay listing identities must be unique and nonempty.", nameof(package));
        var rows = package.Listings.OrderBy(row => row.ListingId, StringComparer.Ordinal)
            .Select(row => EvaluateRow(row, package.AsOfUtc)).ToImmutableArray();
        return new(package.DatasetKind, package.ScorerCommit, package.AsOfUtc, rows.Length,
            rows.Count(row => row.Score.Score.HasValue),
            rows.GroupBy(row => row.Score.Status.ToString()).OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal), rows);
    }

    private static SwingReplayRow EvaluateRow(SwingReplayListing row, DateTimeOffset asOfUtc)
    {
        var issues = ImmutableArray.CreateBuilder<string>();
        using var raw = JsonDocument.Parse(row.ScanJson);
        foreach (var name in RequiredScanFields)
            if (!raw.RootElement.TryGetProperty(name, out var value)
                || value.ValueKind == JsonValueKind.Null && name != "rsiSignal")
                issues.Add($"RawEvidenceMissing:{name}");
        if (raw.RootElement.TryGetProperty("priceStructure", out var structure))
            foreach (var name in new[] { "primaryPatternState", "keyLevelState", "calculatedAt" })
                if (structure.ValueKind != JsonValueKind.Object || !structure.TryGetProperty(name, out _))
                    issues.Add($"RawEvidenceMissing:priceStructure.{name}");
        RsiScanResult? scan = null;
        if (issues.Count == 0)
        {
            scan = JsonSerializer.Deserialize<RsiScanResult>(row.ScanJson, Json)
                ?? throw new InvalidDataException("Replay scan is empty.");
            if (scan.ScannedAt.Kind != DateTimeKind.Utc)
                issues.Add("ScanTimestampNotUtc");
            if (scan.PriceStructure.CalculatedAt.Kind != DateTimeKind.Utc
                || scan.PriceStructure.CalculatedAt > asOfUtc.UtcDateTime)
                issues.Add("StructureTimestampUnverified");
            if (!string.Equals(scan.Symbol, row.Symbol, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(scan.AnalysisTicker)
                || string.IsNullOrWhiteSpace(scan.AnalysisCurrency)
                || string.IsNullOrWhiteSpace(scan.AnalysisMarket))
                issues.Add("ScanIdentityMismatch");
        }
        if (string.IsNullOrWhiteSpace(row.ScanSource) || row.EvidenceAvailableAtUtc == default
            || row.EvidenceAvailableAtUtc > asOfUtc
            || scan is not null && scan.ScannedAt.Kind == DateTimeKind.Utc
                && row.EvidenceAvailableAtUtc.UtcDateTime < scan.ScannedAt)
            issues.Add("ScanProvenanceUnverified");
        if (string.IsNullOrWhiteSpace(row.ListingExchange) || row.ListingCurrency is not ("CAD" or "USD"))
            issues.Add("ListingIdentityUnverified");
        if (string.IsNullOrWhiteSpace(row.ListingIdentitySource) || row.IdentityAvailableAtUtc == default
            || row.IdentityAvailableAtUtc > asOfUtc)
            issues.Add("ListingProvenanceUnverified");

        SectorRelativeResult? sector = null;
        var historyVerified = row.BenchmarkVerified && row.Benchmark is not null
            && !string.IsNullOrWhiteSpace(row.MappingSource) && row.MappingAvailableAtUtc <= asOfUtc
            && row.MappingAvailableAtUtc.HasValue && row.MappingAvailableAtUtc != default(DateTimeOffset)
            && !string.IsNullOrWhiteSpace(row.HistorySource) && row.HistoryAvailableAtUtc <= asOfUtc
            && row.HistoryAvailableAtUtc.HasValue && row.HistoryAvailableAtUtc != default(DateTimeOffset)
            && !row.AnalysisHistory.IsDefaultOrEmpty && !row.BenchmarkHistory.IsDefaultOrEmpty;
        if (historyVerified && scan is not null)
        {
            var bars = row.AnalysisHistory.Concat(row.BenchmarkHistory).ToArray();
            if (bars.Any(bar => bar.Date > row.ExpectedSession)
                || row.AnalysisHistory.GroupBy(bar => bar.Date).Any(group => group.Count() > 1)
                || row.BenchmarkHistory.GroupBy(bar => bar.Date).Any(group => group.Count() > 1))
                issues.Add("HistorySessionConflict");
            else
                sector = SectorRelativeCalculator.Compute(row.AnalysisHistory, row.BenchmarkHistory,
                    row.ExpectedSession, TradingCalendar.ExchangeFor(scan.AnalysisTicker, scan.AnalysisMarket),
                    scan.AnalysisCurrency, row.Benchmark);
        }
        else
            issues.Add("BenchmarkEvidenceUnavailable");

        var fatalIssues = issues.Any(issue => issue != "BenchmarkEvidenceUnavailable");
        var setup = TradeSetupResolver.Resolve(scan, row.ExpectedSession);
        var score = OpportunityScoringEngine.ScoreSwing(new(scan, row.ExpectedSession, sector, setup,
            null, asOfUtc.UtcDateTime, EvaluationFailed: fatalIssues, InstrumentType: row.InstrumentType));
        var fundamentals = row.Fundamentals;
        if (fundamentals is not null && (!string.Equals(fundamentals.Symbol, row.Symbol, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(row.FundamentalsSource)
            || row.FundamentalsAvailableAtUtc is null || row.FundamentalsAvailableAtUtc > asOfUtc
            || fundamentals.AnalyzedAt.Kind != DateTimeKind.Utc
            || row.FundamentalsAvailableAtUtc.Value.UtcDateTime < fundamentals.AnalyzedAt))
        {
            issues.Add("FundamentalsProvenanceUnverified");
            fundamentals = null;
        }
        var strategic = OpportunityScoringEngine.ScoreStrategic(new(scan, row.ExpectedSession, sector, setup,
            fundamentals, asOfUtc.UtcDateTime, EvaluationFailed: fatalIssues, InstrumentType: row.InstrumentType));
        var structuralHistoryVerified = !row.AnalysisHistory.IsDefaultOrEmpty && !string.IsNullOrWhiteSpace(row.HistorySource)
            && row.HistoryAvailableAtUtc is { } available && available != default && available <= asOfUtc;
        var quality = SetupQualityCalculator.Evaluate(scan, structuralHistoryVerified ? row.AnalysisHistory : null,
            row.ExpectedSession, asOfUtc.UtcDateTime);
        return new(row.ListingId, row.Symbol, score, sector, setup, issues.ToImmutable(), strategic, quality);
    }
}
