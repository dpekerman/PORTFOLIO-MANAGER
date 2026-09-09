namespace PortfolioManager.Api.Models;

/// <summary>Durable idempotency record for an Automation summary email.</summary>
public sealed class AutomationNotificationRecord
{
    public int Id { get; set; }
    public string OperationKey { get; set; } = "";
    public string Action { get; set; } = "";
    public string Status { get; set; } = "Sending";
    public int AttemptCount { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}