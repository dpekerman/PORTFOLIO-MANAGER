using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public static class DividendStatuses
{
    public const string Positive = "Positive";
    /// <summary>The provider explicitly reported a forward dividend rate of 0.</summary>
    public const string Zero = "Zero";
    /// <summary>The provider supplied no forward rate. Unknown is never shown as zero.</summary>
    public const string Missing = "Missing";
    /// <summary>The provider's dividend payment date is too old for the rate to be trusted as forward-looking.</summary>
    public const string Stale = "Stale";
    /// <summary>The rate and the provider yield, or the currencies, cannot be reconciled.</summary>
    public const string Inconsistent = "Inconsistent";
}

/// <summary>Forward annual dividend per share and the yield derived from it; both null when the evidence is not reliable.</summary>
public sealed record ForwardDividend(
    decimal? PerShare,
    decimal? YieldPct,
    string Status,
    DateTime? DividendDateUtc,
    string Source);

/// <summary>
/// Validated on live /v7/finance/quote responses for US and Canadian equities, an ETF and a REIT:
/// <c>dividendRate</c> is the annualised forward dividend per share in the trading currency, and <c>dividendYield</c> on that
/// endpoint is a PERCENTAGE (the /v10 summaryDetail module reports a fraction). ETFs and funds return no <c>dividendRate</c>, so
/// they stay Missing rather than being filled from trailing distributions. Yahoo supplies no observation timestamp for either field.
/// </summary>
public static class ForwardDividendCalculator
{
    public const string Source = "Yahoo Finance v7 quote: dividendRate";

    /// <summary>Allowed gap between the provider yield and rate ÷ price: the larger of 0.15 percentage points or 5% of the yield.</summary>
    public const decimal YieldToleranceAbsolutePct = 0.15m;
    public const decimal YieldToleranceRelative = 0.05m;

    /// <summary>A last dividend date older than this means the rate is no longer a credible forward estimate.</summary>
    public static readonly TimeSpan MaxDividendAge = TimeSpan.FromDays(400);

    public static ForwardDividend Evaluate(
        decimal? dividendRate,
        decimal? providerYieldPct,
        decimal price,
        long? dividendDateUnix,
        DateTime utcNow,
        string? quoteCurrency = null,
        string? dividendCurrency = null)
    {
        var dividendDate = ToUtc(dividendDateUnix);
        ForwardDividend Result(decimal? perShare, decimal? yield, string status) => new(perShare, yield, status, dividendDate, Source);

        if (dividendRate is null) return Result(null, null, DividendStatuses.Missing);
        if (dividendRate < 0m) return Result(null, null, DividendStatuses.Missing);
        if (dividendRate == 0m) return Result(0m, 0m, DividendStatuses.Zero);

        // No FX is ever invented: a dividend declared in another currency is unavailable, not converted.
        if (!string.IsNullOrWhiteSpace(dividendCurrency) && !string.IsNullOrWhiteSpace(quoteCurrency)
            && !string.Equals(dividendCurrency, quoteCurrency, StringComparison.OrdinalIgnoreCase))
            return Result(null, null, DividendStatuses.Inconsistent);

        if (dividendDate is not null && utcNow - dividendDate.Value > MaxDividendAge)
            return Result(null, null, DividendStatuses.Stale);

        if (price <= 0m) return Result(dividendRate, null, DividendStatuses.Positive);

        var local = Math.Round(dividendRate.Value / price * 100m, 4);
        if (providerYieldPct is not null)
        {
            var tolerance = Math.Max(YieldToleranceAbsolutePct, local * YieldToleranceRelative);
            if (Math.Abs(providerYieldPct.Value - local) > tolerance)
                return Result(null, null, DividendStatuses.Inconsistent);
        }
        return Result(dividendRate, local, DividendStatuses.Positive);
    }

    private static DateTime? ToUtc(long? unix) =>
        unix is > 0 && unix <= DateTimeOffset.MaxValue.ToUnixTimeSeconds() ? DateTimeOffset.FromUnixTimeSeconds(unix.Value).UtcDateTime : null;
}
