namespace PortfolioManager.Api.Models;

/// <summary>Email SMTP configuration — read from appsettings.json.</summary>
public class EmailSettings
{
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Portfolio Manager Alerts";
    public bool Enabled { get; set; } = false;

    /// <summary>How often (seconds) the background service re-scans for confirmed signals. Default: 300 (5 min).</summary>
    public int ScanIntervalSeconds { get; set; } = 300;

    /// <summary>RSI threshold below which a stock is considered Oversold (used by background scan).</summary>
    public decimal OversoldThreshold { get; set; } = 30m;

    /// <summary>RSI threshold above which a stock is considered Overbought (used by background scan).</summary>
    public decimal OverboughtThreshold { get; set; } = 75m;
}

/// <summary>Notification recipients — managed via API, persisted to a JSON file.</summary>
public class NotificationRecipientsDto
{
    public List<string> Emails { get; set; } = [];
}

/// <summary>Result of a send-notification operation.</summary>
public record NotificationResult(bool Success, string Message, int RecipientsCount, int SignalsCount);
