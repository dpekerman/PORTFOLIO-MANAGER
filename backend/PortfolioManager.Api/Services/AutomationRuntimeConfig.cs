using System.Text.Json;
using System.Text.Json.Serialization;

namespace PortfolioManager.Api.Services;

/// <summary>
/// Runtime-overridable Automation settings (machine wake/keep-awake availability window) — kept
/// completely separate from ScannerRuntimeConfig's EOD Window and ValueScreenerScheduleConfig's
/// schedule, which represent business-time rules, not machine availability. Persisted to
/// automation-config.json in AppContext.BaseDirectory, same pattern as ScannerRuntimeConfig.
/// </summary>
public sealed class AutomationRuntimeConfig
{
    private bool _enabled = false;
    private string _wakeTimeEt = "15:15";
    private string? _keepAwakeUntilEtOverride;
    private int _completionGraceMinutes = 15;
    private int _maxPollMinutes = 90;

    public bool Enabled { get => _enabled; set => _enabled = value; }

    /// <summary>HH:mm ET — when the Scheduled Task wakes/starts the backend and fires the trigger.</summary>
    public string WakeTimeEt { get => _wakeTimeEt; set => _wakeTimeEt = value; }

    /// <summary>HH:mm ET, or null to use the computed default (see <see cref="ComputeKeepAwakeUntilEt"/>).</summary>
    public string? KeepAwakeUntilEtOverride { get => _keepAwakeUntilEtOverride; set => _keepAwakeUntilEtOverride = value; }

    /// <summary>Minutes appended after max(EOD Window End, Value Screener time) so the PC doesn't sleep
    /// exactly when Value Screener's own schedule fires. Default 15.</summary>
    public int CompletionGraceMinutes { get => _completionGraceMinutes; set => _completionGraceMinutes = value; }

    /// <summary>Upper bound on how long a single run's verification poll loop will wait, regardless of
    /// KeepAwakeUntil — prevents a manual "Run Automation Now" fired hours before the business windows
    /// from blocking for the rest of the trading day; it simply reports NotEligible and finalizes.</summary>
    public int MaxPollMinutes { get => _maxPollMinutes; set => _maxPollMinutes = value; }

    private static readonly string ConfigFilePath =
        Path.Combine(AppContext.BaseDirectory, "automation-config.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public void LoadFromFile()
    {
        if (!File.Exists(ConfigFilePath)) return;
        try
        {
            var json = File.ReadAllText(ConfigFilePath);
            var dto = JsonSerializer.Deserialize<AutomationConfigFileDto>(json, JsonOpts);
            if (dto is null) return;
            _enabled = dto.Enabled;
            if (!string.IsNullOrWhiteSpace(dto.WakeTimeEt)) _wakeTimeEt = dto.WakeTimeEt;
            _keepAwakeUntilEtOverride = string.IsNullOrWhiteSpace(dto.KeepAwakeUntilEtOverride) ? null : dto.KeepAwakeUntilEtOverride;
            if (dto.CompletionGraceMinutes > 0) _completionGraceMinutes = dto.CompletionGraceMinutes;
            if (dto.MaxPollMinutes > 0) _maxPollMinutes = dto.MaxPollMinutes;
        }
        catch { /* ignore corrupt file */ }
    }

    public void SaveToFile()
    {
        try
        {
            var dto = new AutomationConfigFileDto
            {
                Enabled = _enabled,
                WakeTimeEt = _wakeTimeEt,
                KeepAwakeUntilEtOverride = _keepAwakeUntilEtOverride,
                CompletionGraceMinutes = _completionGraceMinutes,
                MaxPollMinutes = _maxPollMinutes,
            };
            File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(dto, JsonOpts));
        }
        catch { /* non-critical */ }
    }

    /// <summary>Never mutates or duplicates EOD Window/Value Screener storage — reads their current
    /// values live, each call, from the caller.</summary>
    public string ComputeKeepAwakeUntilEt(string eodWindowEndEt, string valueScreenerTimeEt)
    {
        if (!string.IsNullOrWhiteSpace(_keepAwakeUntilEtOverride)) return _keepAwakeUntilEtOverride;

        var eodEnd = TimeSpan.TryParse(eodWindowEndEt, out var e) ? e : new TimeSpan(16, 30, 0);
        var vs = TimeSpan.TryParse(valueScreenerTimeEt, out var v) ? v : new TimeSpan(17, 0, 0);
        var max = eodEnd > vs ? eodEnd : vs;
        var withGrace = max.Add(TimeSpan.FromMinutes(_completionGraceMinutes));
        return withGrace.ToString(@"hh\:mm");
    }

    private sealed class AutomationConfigFileDto
    {
        public bool Enabled { get; set; }
        public string WakeTimeEt { get; set; } = "15:15";
        public string? KeepAwakeUntilEtOverride { get; set; }
        public int CompletionGraceMinutes { get; set; } = 15;
        public int MaxPollMinutes { get; set; } = 90;
    }
}
