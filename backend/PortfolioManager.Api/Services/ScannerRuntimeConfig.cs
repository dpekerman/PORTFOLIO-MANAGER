namespace PortfolioManager.Api.Services;

/// <summary>
/// Singleton that holds runtime-overridable scanner settings (e.g. EOD confirmation window).
/// Configured initially from appsettings via constructor injection; overridden at runtime via
/// the PUT /api/scanner/eod-settings endpoint.
/// </summary>
public sealed class ScannerRuntimeConfig
{
    private string _eodWindowStart = "15:30";
    private string _eodWindowEnd   = "16:00";
    private bool   _eodWindowEnabled = true;

    // Windows timezone id — cross-platform fallback to IANA "America/New_York"
    private static readonly string[] EasternTzIds =
        ["Eastern Standard Time", "America/New_York"];

    public string EodWindowStart    { get => _eodWindowStart;    set => _eodWindowStart    = value; }
    public string EodWindowEnd      { get => _eodWindowEnd;      set => _eodWindowEnd      = value; }
    public bool   EodWindowEnabled  { get => _eodWindowEnabled;  set => _eodWindowEnabled  = value; }

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
            && TimeSpan.TryParse(_eodWindowEnd, out var end)
            && currentTime >= start
            && currentTime <= end;
    }
}
