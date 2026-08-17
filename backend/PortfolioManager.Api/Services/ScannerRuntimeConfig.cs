using System.Text.Json;
using System.Text.Json.Serialization;

namespace PortfolioManager.Api.Services;

/// <summary>
/// Singleton that holds runtime-overridable scanner settings (e.g. EOD confirmation window).
/// Configured initially from appsettings via constructor injection; overridden at runtime via
/// the PUT /api/scanner/eod-settings endpoint.
/// Settings are also persisted to scanner-eod-config.json in AppContext.BaseDirectory so they
/// survive server restarts.
/// </summary>
public sealed class ScannerRuntimeConfig
{
    private string _eodWindowStart   = "15:30";
    private string _eodWindowEnd     = "16:30";
    private bool   _eodWindowEnabled = true;
    private decimal _eodOversoldRsiThreshold  = 25m;
    private decimal _eodOverboughtRsiThreshold = 75m;

    // ── Turn Strength Thresholds ──────────────────────────────────────────────
    // RSI Δ1D >= EarlyMin and < NormalMin → Early; >= NormalMin and < StrongMin → Normal;
    // >= StrongMin and < ExplosiveMin → Strong; >= ExplosiveMin → Explosive.
    private decimal _turnStrengthEarlyMin    = 0.25m;
    private decimal _turnStrengthNormalMin   = 1.0m;
    private decimal _turnStrengthStrongMin   = 5.0m;
    private decimal _turnStrengthExplosiveMin = 10.0m;

    // Max trading sessions a staged setup may remain active without confirmed promotion.
    private int _maxActiveTradingDays = 7;

    // Windows timezone id — cross-platform fallback to IANA "America/New_York"
    private static readonly string[] EasternTzIds =
        ["Eastern Standard Time", "America/New_York"];

    private static readonly string ConfigFilePath =
        Path.Combine(AppContext.BaseDirectory, "scanner-eod-config.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string  EodWindowStart           { get => _eodWindowStart;           set => _eodWindowStart           = value; }
    public string  EodWindowEnd             { get => _eodWindowEnd;             set => _eodWindowEnd             = value; }
    public bool    EodWindowEnabled         { get => _eodWindowEnabled;         set => _eodWindowEnabled         = value; }
    public decimal EodOversoldRsiThreshold  { get => _eodOversoldRsiThreshold;  set => _eodOversoldRsiThreshold  = value; }
    public decimal EodOverboughtRsiThreshold{ get => _eodOverboughtRsiThreshold; set => _eodOverboughtRsiThreshold = value; }
    public decimal TurnStrengthEarlyMin    { get => _turnStrengthEarlyMin;    set => _turnStrengthEarlyMin    = value; }
    public decimal TurnStrengthNormalMin   { get => _turnStrengthNormalMin;   set => _turnStrengthNormalMin   = value; }
    public decimal TurnStrengthStrongMin   { get => _turnStrengthStrongMin;   set => _turnStrengthStrongMin   = value; }
    public decimal TurnStrengthExplosiveMin{ get => _turnStrengthExplosiveMin; set => _turnStrengthExplosiveMin = value; }
    public int     MaxActiveTradingDays    { get => _maxActiveTradingDays;    set => _maxActiveTradingDays    = value; }

    // ── Persistence ───────────────────────────────────────────────────────────

    /// <summary>Loads settings from scanner-eod-config.json if it exists (overrides appsettings).</summary>
    public void LoadFromFile()
    {
        if (!File.Exists(ConfigFilePath)) return;
        try
        {
            var json = File.ReadAllText(ConfigFilePath);
            var dto  = JsonSerializer.Deserialize<EodConfigFileDto>(json, JsonOpts);
            if (dto is null) return;
            if (!string.IsNullOrWhiteSpace(dto.EodWindowStart))    _eodWindowStart           = dto.EodWindowStart;
            if (!string.IsNullOrWhiteSpace(dto.EodWindowEnd))      _eodWindowEnd             = dto.EodWindowEnd;
            _eodWindowEnabled          = dto.EodWindowEnabled;
            if (dto.EodOversoldRsiThreshold  > 0) _eodOversoldRsiThreshold  = dto.EodOversoldRsiThreshold;
            if (dto.EodOverboughtRsiThreshold > 0) _eodOverboughtRsiThreshold = dto.EodOverboughtRsiThreshold;
            if (dto.TurnStrengthEarlyMin    > 0) _turnStrengthEarlyMin    = dto.TurnStrengthEarlyMin;
            if (dto.TurnStrengthNormalMin   > 0) _turnStrengthNormalMin   = dto.TurnStrengthNormalMin;
            if (dto.TurnStrengthStrongMin   > 0) _turnStrengthStrongMin   = dto.TurnStrengthStrongMin;
            if (dto.TurnStrengthExplosiveMin > 0) _turnStrengthExplosiveMin = dto.TurnStrengthExplosiveMin;
            if (dto.MaxActiveTradingDays    > 0) _maxActiveTradingDays    = dto.MaxActiveTradingDays;
        }
        catch { /* ignore corrupt file */ }
    }

    /// <summary>Saves current settings to scanner-eod-config.json for restart-persistence.</summary>
    public void SaveToFile()
    {
        try
        {
            var dto = new EodConfigFileDto
            {
                EodWindowStart           = _eodWindowStart,
                EodWindowEnd             = _eodWindowEnd,
                EodWindowEnabled         = _eodWindowEnabled,
                EodOversoldRsiThreshold  = _eodOversoldRsiThreshold,
                EodOverboughtRsiThreshold = _eodOverboughtRsiThreshold,
                TurnStrengthEarlyMin     = _turnStrengthEarlyMin,
                TurnStrengthNormalMin    = _turnStrengthNormalMin,
                TurnStrengthStrongMin    = _turnStrengthStrongMin,
                TurnStrengthExplosiveMin = _turnStrengthExplosiveMin,
                MaxActiveTradingDays     = _maxActiveTradingDays,
            };
            File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(dto, JsonOpts));
        }
        catch { /* non-critical */ }
    }

    /// <summary>
    /// Returns true when the current Eastern Time falls within the configured EOD window.
    /// </summary>
    public bool IsEodWindowActive()
    {
        if (!_eodWindowEnabled) return false;

        TimeZoneInfo? tz = null;
        foreach (var id in EasternTzIds)
        {
            try { tz = TimeZoneInfo.FindSystemTimeZoneById(id); break; }
            catch { /* try next */ }
        }
        if (tz is null) return false;

        var easternNow  = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var currentTime = easternNow.TimeOfDay;

        return TimeSpan.TryParse(_eodWindowStart, out var start)
            && TimeSpan.TryParse(_eodWindowEnd,   out var end)
            && currentTime >= start
            && currentTime <= end;
    }

    private sealed class EodConfigFileDto
    {
        public string  EodWindowStart           { get; set; } = "15:30";
        public string  EodWindowEnd             { get; set; } = "16:30";
        public bool    EodWindowEnabled         { get; set; } = true;
        public decimal EodOversoldRsiThreshold  { get; set; } = 25m;
        public decimal EodOverboughtRsiThreshold{ get; set; } = 75m;
        public decimal TurnStrengthEarlyMin    { get; set; } = 0.25m;
        public decimal TurnStrengthNormalMin   { get; set; } = 1.0m;
        public decimal TurnStrengthStrongMin   { get; set; } = 5.0m;
        public decimal TurnStrengthExplosiveMin{ get; set; } = 10.0m;
        public int     MaxActiveTradingDays    { get; set; } = 7;
    }
}
