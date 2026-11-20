using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface ISecurityAnalysisResolver
{
    Task<ResolvedSecurityAnalysis> ResolveAsync(string tradingTicker, string? userId, CancellationToken ct = default);
    /// <summary>PM-029: resolution without persisting an AUTO "needs user input" mapping. Test doubles that never persist may rely on the default.</summary>
    Task<ResolvedSecurityAnalysis> ResolveReadOnlyAsync(string tradingTicker, string? userId, CancellationToken ct = default)
        => ResolveAsync(tradingTicker, userId, ct);
    Task<bool> ValidateUnderlyingTickerAsync(string underlyingTicker, CancellationToken ct = default);
    Task<ResolvedSecurityAnalysis> SaveUserMappingAsync(string tradingTicker, string underlyingTicker, string userId, bool useUnderlyingForAnalysis, CancellationToken ct = default);
    Task<bool> RemoveUserMappingAsync(string tradingTicker, string userId, CancellationToken ct = default);
}

/// <summary>
/// Owns the distinction between an instrument's trading symbol and its technical-analysis symbol.
/// Underlyings are only accepted from persisted reference data or a validated user override.
/// </summary>
public sealed class SecurityAnalysisResolver(AppDbContext db, IMarketDataProvider marketData) : ISecurityAnalysisResolver
{
    private static readonly string[] WrapperIndicators =
    [
        "CAD HEDGED",
        "CANADIAN DEPOSITARY RECEIPT",
        "CDR",
    ];

    public Task<ResolvedSecurityAnalysis> ResolveAsync(string tradingTicker, string? userId, CancellationToken ct = default)
        => ResolveCoreAsync(tradingTicker, userId, persist: true, ct);

    public Task<ResolvedSecurityAnalysis> ResolveReadOnlyAsync(string tradingTicker, string? userId, CancellationToken ct = default)
        => ResolveCoreAsync(tradingTicker, userId, persist: false, ct);

    private async Task<ResolvedSecurityAnalysis> ResolveCoreAsync(
        string tradingTicker,
        string? userId,
        bool persist,
        CancellationToken ct)
    {
        var normalizedTradingTicker = Normalize(tradingTicker);
        var mappings = await db.SecurityAnalysisMappings.AsNoTracking()
            .Where(item => item.TradingTicker == normalizedTradingTicker
                && (item.UserId == userId || item.UserId == null))
            .OrderByDescending(item => item.UserId == userId)
            .ThenByDescending(item => item.MappingSource == SecurityAnalysisMappingSource.USER)
            .ThenBy(item => item.Id)
            .ToListAsync(ct);

        if (mappings.Count > 0)
        {
            var selected = mappings[0];
            var selectedIsUserSpecific = selected.UserId == userId;
            var selectedIsUserMapping = selected.MappingSource == SecurityAnalysisMappingSource.USER;
            var highestPriority = mappings
                .TakeWhile(item => (item.UserId == userId) == selectedIsUserSpecific
                    && (item.MappingSource == SecurityAnalysisMappingSource.USER) == selectedIsUserMapping)
                .Select(item => ToResolved(item, normalizedTradingTicker))
                .ToList();
            var distinctIdentities = highestPriority
                .Select(item => (item.AnalysisTicker, item.AnalysisMarket, item.AnalysisCurrency, item.UsesUnderlyingSecurity, item.ResolutionStatus))
                .Distinct()
                .ToList();
            if (distinctIdentities.Count > 1)
            {
                var self = SelfResolved(normalizedTradingTicker);
                return self with
                {
                    ResolutionStatus = UnderlyingResolutionStatus.NeedsUserInput,
                    MappingSource = null,
                    DataError = "Conflicting security analysis mappings require resolution.",
                };
            }

            return highestPriority[0];
        }

        var quote = normalizedTradingTicker.EndsWith(".TO", StringComparison.OrdinalIgnoreCase)
            ? await marketData.GetQuoteAsync(normalizedTradingTicker, ct)
            : null;
        if (quote is not null && IsWrapperCandidate(quote.CompanyName))
        {
            if (persist)
            {
                db.SecurityAnalysisMappings.Add(new SecurityAnalysisMapping
                {
                    TradingTicker = normalizedTradingTicker,
                    UnderlyingMarket = "US",
                    UseUnderlyingForAnalysis = false,
                    ResolutionStatus = UnderlyingResolutionStatus.NeedsUserInput,
                    MappingSource = SecurityAnalysisMappingSource.AUTO,
                    DetectionDetail = quote.CompanyName,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
                await db.SaveChangesAsync(ct);
            }
            return new ResolvedSecurityAnalysis(
                normalizedTradingTicker,
                normalizedTradingTicker,
                "CA",
                "CAD",
                false,
                UnderlyingResolutionStatus.NeedsUserInput,
                null,
                "Underlying U.S. ticker required for technical analysis.");
        }

        return SelfResolved(normalizedTradingTicker);
    }

    public async Task<bool> ValidateUnderlyingTickerAsync(string underlyingTicker, CancellationToken ct = default)
    {
        var normalizedUnderlyingTicker = Normalize(underlyingTicker);
        if (string.IsNullOrEmpty(normalizedUnderlyingTicker)) return false;

        var quote = await marketData.GetQuoteAsync(normalizedUnderlyingTicker, ct);
        if (quote is null || quote.CurrentPrice <= 0m) return false;

        var history = await marketData.GetDailyClosesAsync(normalizedUnderlyingTicker, ct);
        return history is { Count: >= 200 };
    }

    public async Task<ResolvedSecurityAnalysis> SaveUserMappingAsync(
        string tradingTicker,
        string underlyingTicker,
        string userId,
        bool useUnderlyingForAnalysis,
        CancellationToken ct = default)
    {
        var normalizedTradingTicker = Normalize(tradingTicker);
        var normalizedUnderlyingTicker = Normalize(underlyingTicker);
        if (!await ValidateUnderlyingTickerAsync(normalizedUnderlyingTicker, ct))
            throw new ArgumentException("The underlying ticker is unavailable or lacks sufficient market history.", nameof(underlyingTicker));

        var mapping = await db.SecurityAnalysisMappings.SingleOrDefaultAsync(item =>
            item.TradingTicker == normalizedTradingTicker && item.UserId == userId, ct);
        if (mapping is null)
        {
            mapping = new SecurityAnalysisMapping
            {
                TradingTicker = normalizedTradingTicker,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
            };
            db.SecurityAnalysisMappings.Add(mapping);
        }

        mapping.UnderlyingTicker = normalizedUnderlyingTicker;
        mapping.UnderlyingMarket = "US";
        mapping.UseUnderlyingForAnalysis = useUnderlyingForAnalysis;
        mapping.ResolutionStatus = useUnderlyingForAnalysis
            ? UnderlyingResolutionStatus.Resolved
            : UnderlyingResolutionStatus.NotApplicable;
        mapping.MappingSource = SecurityAnalysisMappingSource.USER;
        mapping.DetectionDetail = "User-confirmed mapping";
        mapping.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToResolved(mapping, normalizedTradingTicker);
    }

    public async Task<bool> RemoveUserMappingAsync(string tradingTicker, string userId, CancellationToken ct = default)
    {
        var normalizedTradingTicker = Normalize(tradingTicker);
        var mapping = await db.SecurityAnalysisMappings.SingleOrDefaultAsync(item =>
            item.TradingTicker == normalizedTradingTicker && item.UserId == userId, ct);
        if (mapping is null) return false;

        db.SecurityAnalysisMappings.Remove(mapping);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static ResolvedSecurityAnalysis ToResolved(SecurityAnalysisMapping mapping, string tradingTicker)
    {
        if (mapping.ResolutionStatus != UnderlyingResolutionStatus.Resolved
            || !mapping.UseUnderlyingForAnalysis
            || string.IsNullOrWhiteSpace(mapping.UnderlyingTicker))
        {
            return new ResolvedSecurityAnalysis(
                tradingTicker,
                tradingTicker,
                "CA",
                "CAD",
                false,
                mapping.ResolutionStatus,
                mapping.MappingSource,
                mapping.ResolutionStatus == UnderlyingResolutionStatus.NeedsUserInput
                    ? "Underlying U.S. ticker required for technical analysis."
                    : null);
        }

        return new ResolvedSecurityAnalysis(
            tradingTicker,
            mapping.UnderlyingTicker,
            mapping.UnderlyingMarket ?? "US",
            "USD",
            true,
            UnderlyingResolutionStatus.Resolved,
            mapping.MappingSource);
    }

    private static ResolvedSecurityAnalysis SelfResolved(string ticker) =>
        new(ticker, ticker, ticker.EndsWith(".TO", StringComparison.OrdinalIgnoreCase) ? "CA" : "US",
            ticker.EndsWith(".TO", StringComparison.OrdinalIgnoreCase) ? "CAD" : "USD",
            false, UnderlyingResolutionStatus.NotApplicable, null);

    private static bool IsWrapperCandidate(string name) =>
        WrapperIndicators.Any(indicator => name.Contains(indicator, StringComparison.OrdinalIgnoreCase));

    private static string Normalize(string ticker) => ticker.Trim().ToUpperInvariant();
}