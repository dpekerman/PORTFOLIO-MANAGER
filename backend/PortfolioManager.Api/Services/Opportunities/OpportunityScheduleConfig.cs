using System.Text.Json;
using System.Text.Json.Serialization;

namespace PortfolioManager.Api.Services.Opportunities;

/// <summary>
/// Runtime schedule for the automatic Trading Opportunities refresh (day/time picker on the Configuration page).
/// Persisted to opportunities-schedule.json in AppContext.BaseDirectory (same pattern as AutomationRuntimeConfig); no DB change.
/// </summary>
public sealed class OpportunityScheduleConfig
{
    private static readonly string ConfigFilePath = Path.Combine(AppContext.BaseDirectory, "opportunities-schedule.json");
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
    private readonly object _lock = new();

    public bool Enabled { get; private set; } = true;

    /// <summary>HH:mm Eastern. Default is just after the 6:00 PM ET point at which the session counts as completed.</summary>
    public string TimeEt { get; private set; } = "18:05";

    /// <summary>Weekdays the refresh may run (market holidays are always skipped).</summary>
    public IReadOnlyList<DayOfWeek> Days { get; private set; } =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    /// <summary>ET calendar date (yyyy-MM-dd) of the last scheduled run attempt, so a day never runs twice.</summary>
    public string? LastRunDateEt { get; private set; }
    public DateTime? LastRunAtUtc { get; private set; }
    public string? LastRunStatus { get; private set; }
    public string? LastRunMessage { get; private set; }

    public static bool IsValidTime(string? value) =>
        value is { Length: 5 } && TimeOnly.TryParseExact(value, "HH:mm", out _);

    public void Update(bool enabled, string timeEt, IEnumerable<DayOfWeek> days)
    {
        lock (_lock)
        {
            Enabled = enabled;
            TimeEt = timeEt;
            Days = days.Distinct().OrderBy(d => ((int)d + 6) % 7).ToList();
            Save();
        }
    }

    public void RecordRun(string dateEt, string status, string? message)
    {
        lock (_lock)
        {
            LastRunDateEt = dateEt;
            LastRunAtUtc = DateTime.UtcNow;
            LastRunStatus = status;
            LastRunMessage = message is { Length: > 500 } ? message[..500] : message;
            Save();
        }
    }

    public void LoadFromFile()
    {
        if (!File.Exists(ConfigFilePath)) return;
        try
        {
            var dto = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(ConfigFilePath), JsonOpts);
            if (dto is null) return;
            Enabled = dto.Enabled;
            if (IsValidTime(dto.TimeEt)) TimeEt = dto.TimeEt!;
            if (dto.Days is { Count: > 0 }) Days = dto.Days;
            LastRunDateEt = dto.LastRunDateEt;
            LastRunAtUtc = dto.LastRunAtUtc;
            LastRunStatus = dto.LastRunStatus;
            LastRunMessage = dto.LastRunMessage;
        }
        catch { /* ignore a corrupt file; defaults apply */ }
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(new FileDto
            {
                Enabled = Enabled, TimeEt = TimeEt, Days = Days.ToList(), LastRunDateEt = LastRunDateEt,
                LastRunAtUtc = LastRunAtUtc, LastRunStatus = LastRunStatus, LastRunMessage = LastRunMessage,
            }, JsonOpts));
        }
        catch { /* non-critical */ }
    }

    private sealed class FileDto
    {
        public bool Enabled { get; set; }
        public string? TimeEt { get; set; }
        public List<DayOfWeek>? Days { get; set; }
        public string? LastRunDateEt { get; set; }
        public DateTime? LastRunAtUtc { get; set; }
        public string? LastRunStatus { get; set; }
        public string? LastRunMessage { get; set; }
    }
}
