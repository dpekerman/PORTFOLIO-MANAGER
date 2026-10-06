namespace PortfolioManager.Api.Models;

/// <summary>A cash/options allocation backup to restore in one transaction.</summary>
public sealed record AllocationRestoreRequest(List<CashBackupItem> Cash, List<OptionBackupItem> Options);

/// <summary>Counts of committed restored records.</summary>
public sealed record AllocationRestoreResponse(int CashCount, int OptionCount);
