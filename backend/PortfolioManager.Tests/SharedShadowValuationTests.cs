using System.Text.Json;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.Valuation;
using PortfolioManager.Api.Services.Opportunities;

namespace PortfolioManager.Tests;

public sealed class SharedShadowValuationTests
{
    private static readonly DateOnly Session = new(2025, 11, 26);
    private static readonly DateTimeOffset Close = new(2025, 11, 26, 21, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Cutoff = Close.AddHours(1);
    private static readonly CurrencyEvidence Cad = new("CAD", "Synthetic broker", true, Close);
    private static readonly CurrencyEvidence Usd = Cad with { CurrencyCode = "USD" };

    private static SharedShadowValuationService Service() => new(new VerifiedFinancialEvidenceAdapter(),
        new Pm029CadValuationCalculator(new ExplicitFxPublicationCalendar(Session, Session,
            [new(Session, true, Close.AddMinutes(30))])));

    private static Pm029SecurityValuationInput Stock(string id, string symbol, CurrencyEvidence currency) =>
        new(id, "test-user", "Account", symbol, "Technology", "Swing", 10m, currency,
            currency.CurrencyCode, 100m, "Synthetic quote", Close, Session, Session, Close);

    private static CashLedgerEvidence Cash(string id, decimal amount, string type, CurrencyEvidence? currency = null) =>
        new(id, "test-user", "account-1", "Account", amount, Session, Close, type, currency ?? Cad);

    private static OptionPositionEvidence Option(string id, int contracts, decimal multiplier) =>
        new(id, "test-user", "account-1", "Account", id, "OPTION-UNDERLYING", "Options",
            "CALL", contracts > 0 ? "LONG" : "SHORT", "Open", contracts, Close,
            "Synthetic broker position", Cad, multiplier, "Synthetic contract terms", Close,
            "CAD", 1m, "Synthetic quote", Close, Session, Session, Close);

    private static ShadowValuationRequest Request() => new(
        "Synthetic", Cutoff, Session, "test-user", Pm029ValuationScopes.Consolidated, 3,
        [Stock("stock:cad", "CAD-LISTING", Cad), Stock("stock:usd", "USD-LISTING", Usd)],
        new(Close, Session, "Synthetic complete ledger",
        [
            Cash("opening", 500m, CashFlowTypeRules.OpeningBalance) with { OwnerId = null, AccountId = "shared" },
            Cash("deposit", 200m, CashFlowTypeRules.Deposit) with { OwnerId = null, AccountId = "shared" },
            Cash("purchase", -200m, CashFlowTypeRules.TradePurchase) with
            { OwnerId = null, AccountId = "shared", SourceType = "PortfolioOpen", SourceItemId = 1 },
            Cash("usd-opening", 200m, CashFlowTypeRules.OpeningBalance, Usd)
        ]),
        [Option("option:long", 2, 10m), Option("option:short", -1, 100m)],
        [new("Bank of Canada", "FXUSDCAD", "USD", "CAD", Session, 1.4m, Close.AddMinutes(40))],
        [new("CAD-LISTING", "Technology", 40m, 60m, 2m)],
        [
            new("stock:cad", "Security", 1000m, "CAD"), new("stock:usd", "Security", 1000m, "USD"),
            new("cash:shared:shared:CAD", "Cash", 500m, "CAD"), new("cash:test-user:account-1:USD", "Cash", 200m, "USD"),
            new("option:long", "Option", 200m, "CAD"), new("option:short", "Option", 100m, "CAD")
        ]);

    [Fact]
    public void MixedCurrencyShadowUsesOneDenominatorAndPreservesExternalFlowClassification()
    {
        var input = Request();
        var before = JsonSerializer.Serialize(input);
        var result = Service().Evaluate(input);
        Assert.True(result.Valuation.Complete);
        Assert.Equal(3100m, result.Valuation.DenominatorCad);
        Assert.Equal(2400m, result.Valuation.StockOnlyCad);
        Assert.Equal(3000m, result.LegacyRawDenominator);
        Assert.Equal(200m, result.ExternalFlowsByCurrency["CAD"]);
        var exposure = Assert.Single(result.Exposures);
        Assert.Equal(1000m / 3100m * 100m, exposure.Evidence.PositionPct);
        Assert.Equal(100m, exposure.Evidence.SectorPct);
        Assert.Equal(40m - 1000m / 3100m * 100m, exposure.PositionHeadroomPp);
        Assert.Equal(40m, exposure.SectorDeviationPp);
        Assert.Contains(result.Differences, row => row.ComponentId == "stock:usd" && row.Classification == "ExpectedCurrencyCorrection");
        Assert.Contains(result.Differences, row => row.ComponentId == "option:short" && row.Classification == "PolicyDecisionRequired");
        Assert.Equal(before, JsonSerializer.Serialize(input));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Service().Evaluate(input)));
        using var serialized = JsonDocument.Parse(JsonSerializer.Serialize(result.Valuation,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var componentInput = serialized.RootElement.GetProperty("components")[0].GetProperty("input");
        Assert.Equal("CAD", componentInput.GetProperty("listingCurrency").GetProperty("currencyCode").GetString());
        Assert.Equal(100m, componentInput.GetProperty("quotePrice").GetDecimal());
        foreach (var consumer in new[] { "Portfolio", "Dashboard", "Allocation", "FinalAction", "Pm029PortfolioFit" })
        {
            var projection = new ValuationConsumerAdapter().Project(result, consumer);
            Assert.Same(result.Exposures, projection.Exposures);
            Assert.Equal(result.Valuation.DenominatorCad, projection.DenominatorCad);
            Assert.False(projection.IsAuthoritative);
        }
    }

    [Theory]
    [InlineData("CAD", 1000)]
    [InlineData("USD", 1400)]
    public void SingleCurrencyShadowHasExpectedParity(string currency, int expected)
    {
        var input = Request() with
        {
            Securities = [Stock("single", "LISTING", currency == "CAD" ? Cad : Usd)],
            Cash = new(Close, Session, "Synthetic empty ledger", []), Options = [],
            Legacy = [new("single", "Security", 1000m, currency)]
        };
        var result = Service().Evaluate(input);
        Assert.Equal(expected, result.Valuation.DenominatorCad);
        Assert.Equal(currency == "CAD" ? "Parity" : "ExpectedCurrencyCorrection",
            Assert.Single(result.Differences).Classification);
    }

    [Fact]
    public void NvdaEtfCashAndNetOptionsProjectFitFromTheSameSnapshotWithoutDoubleCounting()
    {
        var request = Request() with
        {
            Securities = [Stock("nvda", "NVDA", Usd), Stock("etf", "SMH", Usd) with { Sector = "ETFs & Funds" }],
            Policies = [new("NVDA", "Technology", 50m, 60m, 2m)]
        };
        var snapshot = Service().Evaluate(request);
        Assert.Equal(3500m, snapshot.Valuation.DenominatorCad);
        Assert.Equal(2800m, snapshot.Valuation.StockOnlyCad);
        var adapter = new ValuationConsumerAdapter();
        var (fit, authoritative) = adapter.ProjectShadowFit(snapshot, "NVDA");
        Assert.Equal(FitStatus.Eligible, fit.Status);
        Assert.Equal(40d, fit.WeightPct);
        Assert.Equal(50d, fit.SectorWeightPct);
        Assert.False(authoritative);
        Assert.Equal(6, snapshot.Valuation.Components.Count);
        Assert.DoesNotContain(snapshot.Valuation.Components, component => component.Symbol == "OPTION-UNDERLYING" && component.Kind == "Security");
        foreach (var consumer in new[] { "Portfolio", "Dashboard", "Allocation", "FinalAction", "Pm029PortfolioFit" })
            Assert.Equal(3500m, adapter.Project(snapshot, consumer).DenominatorCad);
        var invalid = request with { Options = [Option("unknown", 1, 100m) with { Currency = null }] };
        Assert.Equal(FitStatus.Unavailable, adapter.ProjectShadowFit(Service().Evaluate(invalid), "NVDA").Fit.Status);
        Assert.Equal(FitStatus.Unavailable, adapter.ProjectShadowFit(snapshot, "UNMAPPED").Fit.Status);
    }

    [Fact]
    public void UnknownCashCurrencyDoesNotBecomeCadOrZero()
    {
        var input = Request() with { Cash = new(Close, Session, "Synthetic ledger",
            [Cash("unknown", 100m, CashFlowTypeRules.Deposit) with { Currency = null }]) };
        var result = Service().Evaluate(input);
        Assert.False(result.Valuation.Complete);
        Assert.Null(result.Valuation.DenominatorCad);
        Assert.Null(result.Exposures[0].Evidence.PositionPct);
        Assert.Contains(Pm029ValuationIssueCodes.CurrencyEvidenceMissing, result.Valuation.IssueCodes);
    }

    [Theory]
    [InlineData("duplicate-row", "DuplicateLedgerRow")]
    [InlineData("duplicate-link", "CashTradeLinkDuplicated")]
    [InlineData("duplicate-opening", "MultipleOpeningBalances")]
    [InlineData("bad-sign", "CashFlowSignConflict")]
    [InlineData("future-row", "LedgerRowNotAvailableAsOf")]
    [InlineData("missing-account", "CashAccountIdentityMissing")]
    [InlineData("currency-conflict", "ConflictingCurrencyEvidence")]
    public void InvalidLedgerEvidenceSuppressesAllAllocation(string scenario, string issue)
    {
        var first = Cash("one", 100m, CashFlowTypeRules.OpeningBalance);
        var rows = scenario switch
        {
            "duplicate-row" => new[] { first, first },
            "duplicate-link" => new[]
            {
                first with { SourceType = "PortfolioOpen", SourceItemId = 1 },
                Cash("two", 10m, CashFlowTypeRules.TradeProceeds) with { SourceType = "PortfolioOpen", SourceItemId = 1 }
            },
            "duplicate-opening" => [first, first with { Id = "two" }],
            "bad-sign" => [first with { SignedAmount = -1m }],
            "future-row" => [first with { RecordedAtUtc = Cutoff.AddMinutes(1) }],
            "missing-account" => [first with { AccountId = null }],
            _ => [first with { HistoricalCurrencyEvidence = [Usd] }]
        };
        var result = Service().Evaluate(Request() with { Cash = new(Close, Session, "Synthetic ledger", rows) });
        Assert.Contains(issue, result.Valuation.IssueCodes);
        Assert.False(result.Valuation.Complete);
        Assert.Null(result.Exposures[0].PositionHeadroomPp);
    }

    [Theory]
    [InlineData("sign")]
    [InlineData("identity")]
    [InlineData("closed-nonzero")]
    [InlineData("future-position")]
    [InlineData("stale-position")]
    public void InvalidOptionPositionNeverEnablesPortfolioFit(string scenario)
    {
        var option = Option("option:bad", 1, 10m);
        option = scenario switch
        {
            "sign" => option with { PositionSide = "SHORT" },
            "identity" => option with { ContractId = null },
            "closed-nonzero" => option with { State = "Closed" },
            "future-position" => option with { PositionAsOfUtc = Cutoff.AddHours(1) },
            _ => option with { PositionAsOfUtc = Close.AddDays(-1) }
        };
        var result = Service().Evaluate(Request() with { Options = [option] });
        Assert.Contains("OptionPositionEvidenceUnverified", result.Valuation.IssueCodes);
        Assert.False(result.Valuation.Complete);
    }

    [Fact]
    public void ClosedOptionsAndOtherUsersAreExcludedButSharedCashIsIncluded()
    {
        var input = Request();
        var result = Service().Evaluate(input with
        {
            Options = [Option("closed", 1, 10m) with { State = "Closed", SignedOpenContracts = 0 }],
            Cash = input.Cash with { Entries = input.Cash.Entries.Append(
                Cash("other-user", 10000m, CashFlowTypeRules.Deposit) with { OwnerId = "another-user" }).ToArray() }
        });
        Assert.True(result.Valuation.Complete);
        Assert.Equal(3180m, result.Valuation.DenominatorCad);
        Assert.DoesNotContain(result.Valuation.Components, row => row.Id == "closed");
    }

    [Fact]
    public void MultipleRolesKeepWholeSymbolExposureAndSuppressAnAmbiguousLimit()
    {
        var input = Request();
        var result = Service().Evaluate(input with
        {
            Securities = input.Securities.Append(Stock("second-role", "CAD-LISTING", Cad) with
                { InvestmentRole = "Strategic" }).ToArray()
        });
        Assert.True(result.Exposures[0].Evidence.RolePolicyAmbiguous);
        Assert.Null(result.Exposures[0].PositionHeadroomPp);
        Assert.Null(result.Exposures[0].Evidence.PositionLimitExceeded);
    }

    [Fact]
    public void UnknownSectorDoesNotBecomeZeroExposureInConsumerEvidence()
    {
        var input = Request();
        var result = Service().Evaluate(input with { Securities =
            [input.Securities[0], input.Securities[1] with { Sector = null }] });
        Assert.True(result.Valuation.Complete);
        Assert.Null(result.Exposures[0].Evidence.SectorPct);
        Assert.Null(result.Exposures[0].Evidence.SectorOverTarget);
        Assert.Null(result.Exposures[0].SectorDeviationPp);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void NonpositiveConsolidatedDenominatorRemainsUnavailable(int amount)
    {
        var input = Request() with
        {
            Securities = [], Options = [],
            Cash = new(Close, Session, "Synthetic ledger",
                [Cash("balance", amount, amount < 0 ? CashFlowTypeRules.Withdrawal : CashFlowTypeRules.Deposit)]),
            Legacy = []
        };
        var result = Service().Evaluate(input);
        Assert.False(result.Valuation.Complete);
        Assert.Null(result.Valuation.DenominatorCad);
        Assert.Contains(Pm029ValuationIssueCodes.NonPositiveDenominator, result.Valuation.IssueCodes);
    }

    [Fact]
    public void LegacyClassifierDoesNotUseDescriptionsOrTickerSuffixesAsEvidence()
    {
        var classifier = new LegacyProvenanceClassifier();
        var cash = new CashItem { Id = 1, Description = "USD CASH", AccountType = "CAD account" };
        Assert.Equal("UnknownCurrency", classifier.ClassifyCash(cash, [], Cutoff).Classification);
        Assert.Equal("RequiresBrokerOrOwnerConfirmation", classifier.ClassifyCash(cash,
            [Cad with { IsVerified = false }], Cutoff).Classification);
        Assert.Equal("ConflictingEvidence", classifier.ClassifyCash(cash, [Cad, Usd], Cutoff).Classification);
        Assert.Equal("VerifiedFromAuthoritativeEvidence", classifier.ClassifyCash(cash, [Usd], Cutoff).Classification);
        var option = classifier.ClassifyOption(new OptionItem { Id = 1, UnderlyingTicker = "CDR.TO", PositionType = "CALL" }, null, Cutoff);
        Assert.Contains("VerifiedCurrency", option.MissingEvidence);
        Assert.Contains("PositionSide", option.MissingEvidence);
        Assert.Contains("VerifiedMultiplier", option.MissingEvidence);
    }

    [Fact]
    public void PortfolioAdapterUsesProviderTimeNotFetchTimeAndNeverCostFallback()
    {
        var item = new PortfolioItemDto(1, "CDR.TO", "Synthetic CDR", 10m, 99m, "Technology", "",
            false, false, null, Close.UtcDateTime);
        var quote = new StockQuote { Currency = "CAD", CurrentPrice = 100m,
            Timestamp = Cutoff.ToUnixTimeSeconds(), SourcePriceTimestamp = Close.ToUnixTimeSeconds() };
        var adapter = new ValuationConsumerAdapter();
        var security = Assert.Single(adapter.AdaptPortfolio([new(new(item, quote), "test-user", Cad, Session, Close)]));
        Assert.Equal(Close, security.QuoteAsOfUtc);
        Assert.Equal("CAD", security.ListingCurrency!.CurrencyCode);
        var missing = Assert.Single(adapter.AdaptPortfolio([new(new(item, null), "test-user", Cad, Session, Close)]));
        Assert.Null(missing.QuotePrice);
        Assert.Null(missing.QuoteAsOfUtc);
    }
}
