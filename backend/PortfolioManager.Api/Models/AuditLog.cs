namespace PortfolioManager.Api.Models;

/// <summary>Immutable audit record of one create/update/delete (or explicit system event).</summary>
public class AuditLog
{
    public long Id { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    /// <summary>Null for System-sourced entries.</summary>
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    /// <summary>User | System</summary>
    public string Source { get; set; } = AuditSources.User;
    public string Category { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Symbol { get; set; }
    public string? AccountType { get; set; }
    public string Summary { get; set; } = string.Empty;
    /// <summary>JSON array of { field, old, new }.</summary>
    public string? ChangesJson { get; set; }
    public string? CorrelationId { get; set; }
}

public static class AuditSources
{
    public const string User = "User";
    public const string System = "System";
}

public static class AuditCategories
{
    public const string Stock = "Stock";
    public const string Option = "Option";
    public const string Cash = "Cash";
    public const string Watchlist = "Watchlist";
    public const string Allocation = "Allocation";
    public const string Config = "Config";
    public const string User = "User";
    public const string Automation = "Automation";
    public const string Signals = "Signals";
}

public static class AuditActions
{
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Delete = "Delete";
    public const string Restore = "Restore";
    public const string Adjust = "Adjust";
    public const string Other = "Other";
}

public record AuditChangeDto(string Field, string? Old, string? New);

public record AuditLogDto(
    long Id,
    DateTime OccurredAtUtc,
    string? UserId,
    string? UserName,
    string Source,
    string Category,
    string Action,
    string EntityType,
    string? EntityId,
    string? Symbol,
    string? AccountType,
    string Summary,
    IReadOnlyList<AuditChangeDto> Changes,
    string? CorrelationId);

public record AuditQuery(
    int Page = 1,
    int PageSize = 25,
    string? SortBy = null,
    string? SortDir = null,
    DateTime? From = null,
    DateTime? To = null,
    string[]? Category = null,
    string[]? Action = null,
    string? Source = null,
    string? UserId = null,
    string? Q = null);

public record AuditPagedResponse(
    IReadOnlyList<AuditLogDto> Items,
    int Total,
    int Page,
    int PageSize);

public record AuditUserDto(string UserId, string UserName);

public record DeleteAuditBatchRequest(long[]? Ids);

public record AuditFiltersDto(
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Actions,
    IReadOnlyList<AuditUserDto> Users);
