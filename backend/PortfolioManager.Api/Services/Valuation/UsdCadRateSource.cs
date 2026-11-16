using Microsoft.Extensions.Caching.Memory;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.Valuation;

/// <summary>CAD per 1 USD for one valuation, or the exact reason it is unavailable. Never defaults to 1.</summary>
public sealed record UsdCadRate(decimal? Rate, DateOnly? ObservationDate, string? IssueCode, string Detail)
{
    public bool IsAvailable => Rate is > 0m && IssueCode is null;
}

public interface IUsdCadRateSource
{
    Task<UsdCadRate> GetAsync(DateTimeOffset asOfUtc, CancellationToken ct = default);
}

/// <summary>
/// The one USD/CAD rate used by every consumer of a valuation: the latest Bank of Canada FXUSDCAD observation on or before the
/// valuation session that the publication calendar says was already published at <c>asOfUtc</c> (no look-ahead). It may lag the
/// latest expected publication by at most one session; anything older is stale.
/// </summary>
public sealed class UsdCadRateSource(IBankOfCanadaFxProvider provider, IFxPublicationCalendar calendar, IMemoryCache cache) : IUsdCadRateSource
{
    private const int LookbackDays = 14;
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(30);

    public Task<UsdCadRate> GetAsync(DateTimeOffset asOfUtc, CancellationToken ct = default)
    {
        var session = TradingCalendar.TorontoDate(asOfUtc.UtcDateTime);
        var key = $"pm029:usdcad:{session:yyyyMMdd}:{asOfUtc.UtcDateTime:yyyyMMddHH}";
        return cache.GetOrCreateAsync(key, async entry =>
        {
            var result = await ResolveAsync(session, asOfUtc, ct);
            entry.AbsoluteExpirationRelativeToNow = result.IsAvailable ? CacheFor : TimeSpan.FromMinutes(2);
            return result;
        })!;
    }

    private async Task<UsdCadRate> ResolveAsync(DateOnly session, DateTimeOffset asOfUtc, CancellationToken ct)
    {
        IReadOnlyList<Pm029FxObservation> observations;
        try { observations = await provider.GetUsdCadObservationsAsync(session.AddDays(-LookbackDays), session, ct); }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return Unavailable(Pm029ValuationIssueCodes.FxUnavailable, "the Bank of Canada USD/CAD rate could not be retrieved");
        }

        var start = session.AddDays(-LookbackDays);
        if (!calendar.Covers(start, session))
            return Unavailable(Pm029ValuationIssueCodes.FxCalendarCoverageMissing, "the FX publication calendar does not cover the valuation date");
        var published = calendar.GetDays(start, session)
            .Where(d => d.IsPublicationSession && d.ExpectedPublicationAtUtc!.Value.ToUniversalTime() <= asOfUtc)
            .OrderByDescending(d => d.Date).ToList();
        var latest = published.Count == 0 ? null : observations
            .Where(o => o.ObservationDate <= published[0].Date && o.Rate > 0m
                && string.Equals(o.Series, "FXUSDCAD", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(o => o.ObservationDate).FirstOrDefault();
        if (latest is null)
            return Unavailable(Pm029ValuationIssueCodes.FxUnavailable, "no published Bank of Canada USD/CAD observation exists for the valuation date");
        var oldestAcceptable = published.Count > 1 ? published[1].Date : published[0].Date;
        if (latest.ObservationDate < oldestAcceptable)
            return Unavailable(Pm029ValuationIssueCodes.FxStale,
                $"the latest Bank of Canada USD/CAD observation ({latest.ObservationDate:yyyy-MM-dd}) is older than the latest publication ({published[0].Date:yyyy-MM-dd})");
        return new(latest.Rate, latest.ObservationDate, null,
            $"Bank of Canada FXUSDCAD {latest.ObservationDate:yyyy-MM-dd} = {latest.Rate} CAD per USD");
    }

    private static UsdCadRate Unavailable(string code, string detail) => new(null, null, code, detail);
}
