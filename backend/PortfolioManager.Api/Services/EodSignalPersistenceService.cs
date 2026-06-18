using System.Text.Json;
using System.Text.Json.Serialization;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

/// <summary>
/// Singleton service that persists EOD CONFIRM signals to a JSON file on disk
/// (eod-signal-history.json, located next to the API binary).
///
/// Purpose — "Gap 3 / Overnight Persistence":
///   When the EOD window closes, confirmed signals are saved so that the next morning
///   traders can see what was flagged and decide whether the setup is still valid.
///
/// The file holds ONE day's worth of signals (the most recent EOD window).
/// On the next save (next trading day) the file is overwritten with fresh data.
/// A read operation returns the stored history plus metadata (date, morning window flag).
/// </summary>
public class EodSignalPersistenceService
{
    private readonly string _filePath;
    private readonly ILogger<EodSignalPersistenceService> _logger;

    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly string[] EasternTzIds = ["Eastern Standard Time", "America/New_York"];

    public EodSignalPersistenceService(ILogger<EodSignalPersistenceService> logger)
    {
        _logger = logger;
        // Resolve path relative to the application's base directory
        var baseDir = AppContext.BaseDirectory;
        _filePath = Path.Combine(baseDir, "eod-signal-history.json");
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Persists the supplied EOD CONFIRM signals to disk, tagged with today's ET date.
    /// Overwrites any previously saved signals (only the latest EOD window is kept).
    /// </summary>
    public async Task SaveAsync(IEnumerable<RsiScanResult> eodResults, CancellationToken ct = default)
    {
        var tz = GetEasternTz();
        var etToday = tz is not null
            ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).ToString("yyyy-MM-dd")
            : DateTime.UtcNow.ToString("yyyy-MM-dd");

        var history = new EodSignalHistory
        {
            Date = etToday,
            Signals = eodResults.Select(r => new EodSignalRecord
            {
                Symbol        = r.Symbol,
                CompanyName   = r.CompanyName ?? string.Empty,
                ScanType      = r.ScanType.ToString(),
                Rsi           = Math.Round(r.Rsi, 2),
                Price         = r.CurrentPrice,
                TriggerDetails = r.TriggerDetails ?? string.Empty,
                ScannedAt     = r.ScannedAt,
            }).ToList()
        };

        try
        {
            var json = JsonSerializer.Serialize(history, _json);
            await File.WriteAllTextAsync(_filePath, json, ct);
            _logger.LogInformation("Persisted {Count} EOD CONFIRM signal(s) for {Date} to {Path}",
                history.Signals.Count, etToday, _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist EOD signals to {Path}", _filePath);
        }
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the persisted EOD signal history plus contextual metadata.
    /// If the file does not exist or is unreadable, returns an empty response with HasData = false.
    /// </summary>
    public async Task<YesterdayEodResponse> GetYesterdayEodAsync(CancellationToken ct = default)
    {
        var tz = GetEasternTz();

        bool isMorning = false;
        if (tz is not null)
        {
            var etNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            isMorning = etNow.Hour < 12;  // Before noon ET = morning window
        }

        if (!File.Exists(_filePath))
            return new YesterdayEodResponse { HasData = false, IsMorningWindow = isMorning };

        try
        {
            var json = await File.ReadAllTextAsync(_filePath, ct);
            var history = JsonSerializer.Deserialize<EodSignalHistory>(json, _json);
            if (history is null || history.Signals.Count == 0)
                return new YesterdayEodResponse { HasData = false, IsMorningWindow = isMorning };

            return new YesterdayEodResponse
            {
                HasData        = true,
                SignalDate     = history.Date,
                IsMorningWindow = isMorning,
                Signals        = history.Signals
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read EOD signal history from {Path}", _filePath);
            return new YesterdayEodResponse { HasData = false, IsMorningWindow = isMorning };
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TimeZoneInfo? GetEasternTz()
    {
        foreach (var id in EasternTzIds)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch { /* try next */ }
        }
        return null;
    }
}
