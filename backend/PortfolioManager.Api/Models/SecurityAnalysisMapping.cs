namespace PortfolioManager.Api.Models;

public enum SecurityAnalysisMappingSource
{
    AUTO,
    USER,
}

public enum UnderlyingResolutionStatus
{
    NotApplicable,
    Resolved,
    NeedsUserInput,
}

/// <summary>
/// Central mapping between an owned/traded symbol and the symbol used for technical analysis.
/// A null UserId row is shared reference data; a user row is a manual override.
/// </summary>
public class SecurityAnalysisMapping
{
    public int Id { get; set; }
    public string TradingTicker { get; set; } = string.Empty;
    public string? UnderlyingTicker { get; set; }
    public string? UnderlyingMarket { get; set; }
    public bool UseUnderlyingForAnalysis { get; set; }
    public UnderlyingResolutionStatus ResolutionStatus { get; set; }
    public SecurityAnalysisMappingSource MappingSource { get; set; }
    public string? UserId { get; set; }
    public string? DetectionDetail { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed record ResolvedSecurityAnalysis(
    string TradingTicker,
    string AnalysisTicker,
    string AnalysisMarket,
    string AnalysisCurrency,
    bool UsesUnderlyingSecurity,
    UnderlyingResolutionStatus ResolutionStatus,
    SecurityAnalysisMappingSource? MappingSource,
    string? DataError = null);