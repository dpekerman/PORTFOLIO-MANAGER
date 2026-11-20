using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.Opportunities;

namespace PortfolioManager.Tests;

public sealed class SwingReplayRunnerTests
{
    private static readonly DateOnly Session = new(2026, 11, 13);
    private static readonly DateTimeOffset Close = new(2026, 11, 13, 21, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset AsOf = new(2026, 11, 15, 16, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static SwingReplayPackage Package()
    {
        var scan = new RsiScanResult
        {
            Symbol = "SYNTHETIC", AnalysisTicker = "SYNTHETIC", AnalysisMarket = "US", AnalysisCurrency = "USD",
            TradingDate = Session, ScannedAt = Close.AddMinutes(5).UtcDateTime,
            CurrentPrice = 100m, Rsi = 55m, RsiSignal = 50m, RsiSignalAvailable = true,
            MacdHistogram = 1m, MacdHistDelta = 0.1m,
            Ema20Price = 99m, Sma50Price = 90m, Sma200 = 80m, Has200Dma = true,
            Volume = 10000m, VolumeRatio = 1.5m, VolumeSignal = "Validated", ChangePercent = 1m,
            DailyAtr = 2m, FibZone = "Normal Pullback", DynamicStopLoss = 0m,
            PriceStructure = PriceStructureResult.None with { CalculatedAt = Close.UtcDateTime }
        };
        var dates = new List<DateOnly> { Session };
        for (var i = 0; i < 60; i++)
            dates.Add(TradingCalendar.PreviousTradingDay(dates[^1], MarketExchange.Nyse));
        dates.Reverse();
        return new("Synthetic", "test-scorer-commit", AsOf, 1,
        [
            new("listing:synthetic", "SYNTHETIC", "NYSE", "USD", Session,
                JsonSerializer.Serialize(scan, Json), "Synthetic scanner", Close.AddMinutes(5),
                new("SYNTHETIC-BENCH", "Synthetic benchmark", "USD"), true, "Synthetic mapping", Close,
                "Synthetic adjusted closes", Close.AddMinutes(5),
                dates.Select((date, i) => new MarketDailyClose(date, 100m + i * 0.1m)).ToImmutableArray(),
                dates.Select(date => new MarketDailyClose(date, 100m)).ToImmutableArray(),
                "Synthetic verified listing", Close)
        ]);
    }

    [Fact]
    public void ActualSwingEngineRunsDeterministicallyWithoutStopMarketROwnershipOrManualR()
    {
        var package = Package();
        var original = JsonSerializer.Serialize(package, Json);
        var runner = new SwingReplayRunner();
        var result = runner.Evaluate(package);
        var row = Assert.Single(result.Rows);
        Assert.Equal("pm029-v2", row.Score.ScoringVersion);
        Assert.Equal(ScoreStatus.Verified, row.Score.Status);
        Assert.NotNull(row.Score.Score);
        Assert.Null(row.Setup.RewardRisk);
        Assert.DoesNotContain(row.Score.Components, component => component.Code == "S6");
        Assert.Equal(original, JsonSerializer.Serialize(package, Json));
        Assert.Equal(JsonSerializer.Serialize(result, Json), JsonSerializer.Serialize(runner.Evaluate(package), Json));
        var scan = JsonNode.Parse(package.Listings[0].ScanJson)!;
        scan["dynamicStopLoss"] = 96m;
        scan["week52High"] = 120m;
        var changed = runner.Evaluate(package with { Listings =
            [package.Listings[0] with { ScanJson = scan.ToJsonString() }] });
        Assert.Equal(row.Score.Score, changed.Rows[0].Score.Score);
        Assert.Equal(row.Score.AwardedTotal, changed.Rows[0].Score.AwardedTotal);
    }

    [Fact]
    public void MissingRawMacdDeltaCannotSilentlyDeserializeAsZero()
    {
        var package = Package();
        var scan = JsonNode.Parse(package.Listings[0].ScanJson)!.AsObject();
        scan.Remove("macdHistDelta");
        var row = Assert.Single(new SwingReplayRunner().Evaluate(package with
            { Listings = [package.Listings[0] with { ScanJson = scan.ToJsonString() }] }).Rows);
        Assert.Contains("RawEvidenceMissing:macdHistDelta", row.PackageIssueCodes);
        Assert.Equal(ScoreStatus.Unavailable, row.Score.Status);
        Assert.Null(row.Score.Score);
    }

    [Fact]
    public void FundDeferralAndFutureFundamentalsDoNotContaminateTechnicalScoring()
    {
        var package = Package();
        var baseline = new SwingReplayRunner().Evaluate(package).Rows[0];
        var future = package.Listings[0] with
        {
            Fundamentals = new ValueScreenerResult { Symbol = "SYNTHETIC", AnalyzedAt = AsOf.AddMinutes(1).UtcDateTime },
            FundamentalsSource = "Synthetic source", FundamentalsAvailableAtUtc = AsOf.AddMinutes(1)
        };
        var invalid = new SwingReplayRunner().Evaluate(package with { Listings = [future] }).Rows[0];
        Assert.Contains("FundamentalsProvenanceUnverified", invalid.PackageIssueCodes);
        Assert.Equal(JsonSerializer.Serialize(baseline.Score, Json), JsonSerializer.Serialize(invalid.Score, Json));
        Assert.Null(invalid.Strategic!.Score);
        var fund = new SwingReplayRunner().Evaluate(package with
            { Listings = [package.Listings[0] with { InstrumentType = "ETF" }] }).Rows[0];
        Assert.Equal(ScoreStatus.NotEvaluated, fund.Score.Status);
        Assert.Equal(ScoreStatus.NotEvaluated, fund.Strategic!.Status);
    }

    [Theory]
    [InlineData("benchmark", "BenchmarkEvidenceUnavailable")]
    [InlineData("future-source", "ScanProvenanceUnverified")]
    [InlineData("unknown-source-time", "ScanProvenanceUnverified")]
    [InlineData("future-history", "HistorySessionConflict")]
    [InlineData("identity", "ScanIdentityMismatch")]
    [InlineData("timestamp", "ScanTimestampNotUtc")]
    public void UnverifiedEvidenceIsNotPresentedAsVerified(string scenario, string issue)
    {
        var package = Package();
        var listing = package.Listings[0];
        var scan = JsonNode.Parse(listing.ScanJson)!;
        if (scenario == "identity") scan["symbol"] = "OTHER";
        if (scenario == "timestamp") scan["scannedAt"] = "2026-11-13T21:05:00";
        listing = scenario switch
        {
            "benchmark" => listing with { BenchmarkVerified = false },
            "future-source" => listing with { EvidenceAvailableAtUtc = AsOf.AddHours(1) },
            "unknown-source-time" => listing with { EvidenceAvailableAtUtc = default },
            "future-history" => listing with
            { AnalysisHistory = listing.AnalysisHistory.Add(new(Session.AddDays(1), 101m)) },
            _ => listing with { ScanJson = scan.ToJsonString() }
        };
        var row = Assert.Single(new SwingReplayRunner().Evaluate(package with { Listings = [listing] }).Rows);
        Assert.Contains(issue, row.PackageIssueCodes);
        Assert.NotEqual(ScoreStatus.Verified, row.Score.Status);
        if (scenario != "benchmark") Assert.Null(row.Score.Score);
    }

    [Fact]
    public void ActualScorerRetainsInsufficientEssentialEvidenceAndStaleScanReasons()
    {
        var package = Package();
        var scan = JsonNode.Parse(package.Listings[0].ScanJson)!;
        scan["ema20Price"] = 0m;
        var insufficient = new SwingReplayRunner().Evaluate(package with { Listings =
            [package.Listings[0] with { ScanJson = scan.ToJsonString() }] }).Rows[0];
        Assert.Equal(ScoreStatus.Insufficient, insufficient.Score.Status);
        Assert.Contains("MissingEssential:S1", insufficient.Score.ReasonCodes);
        scan["tradingDate"] = "2026-11-12";
        var stale = new SwingReplayRunner().Evaluate(package with { Listings =
            [package.Listings[0] with { ScanJson = scan.ToJsonString() }] }).Rows[0];
        Assert.Contains(OpportunityReasons.StaleScan, stale.Score.ReasonCodes);
    }

    [Fact]
    public void DuplicateOrIncompleteMembershipCannotBeClaimedAsFullUniverseCoverage()
    {
        var package = Package();
        var runner = new SwingReplayRunner();
        Assert.Throws<ArgumentException>(() => runner.Evaluate(package with { ExpectedUniqueListings = 224 }));
        Assert.Throws<ArgumentException>(() => runner.Evaluate(package with
            { ExpectedUniqueListings = 2, Listings = [package.Listings[0], package.Listings[0]] }));
    }
}
