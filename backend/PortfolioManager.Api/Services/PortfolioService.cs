using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services.Risk;
using System.Security.Claims;

namespace PortfolioManager.Api.Services;

public interface IPortfolioService
{
    Task<IReadOnlyList<PortfolioItemDto>> GetAllAsync(CancellationToken ct = default);
    /// <summary>Items visible to <paramref name="userId"/> (own + shared) for background jobs that have no HTTP user.</summary>
    Task<IReadOnlyList<PortfolioItemDto>> GetAllForUserAsync(string userId, CancellationToken ct = default);
    Task<PortfolioItemDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<PortfolioItemDto> AddAsync(AddPortfolioItemRequest request, CancellationToken ct = default);
    Task<PortfolioItemDto> AddManualAsync(AddManualPositionRequest request, CancellationToken ct = default);
    Task<UpdatePortfolioItemResponse?> UpdateAsync(int id, UpdatePortfolioItemRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task<int> RefreshSectorsAsync(CancellationToken ct = default);
    Task<bool> UpdateHoldingRoleAsync(int id, string holdingRole, CancellationToken ct = default);
    Task<bool> UpdateNotesAsync(int id, string? notes, CancellationToken ct = default);
    Task<IReadOnlyList<PortfolioBackupItem>> BackupAsync(CancellationToken ct = default);
    Task<int> RestoreAsync(IReadOnlyList<PortfolioBackupItem> items, CancellationToken ct = default);
    Task RefreshSectorForItemAsync(int id, string symbol, CancellationToken ct = default);
}

public sealed class PortfolioService(
    AppDbContext db,
    IMarketDataProvider marketData,
    IMutationClock mutationClock,
    IHttpContextAccessor httpCtx) : IPortfolioService
{
    private string? CurrentUserId() => httpCtx.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
    private bool IsAdmin() => httpCtx.HttpContext?.User.IsInRole("Admin") ?? false;

    private IQueryable<PortfolioItem> OwnedItems()
    {
        var q = db.PortfolioItems.AsQueryable();
        if (IsAdmin()) return q; // Admins see everything
        var uid = CurrentUserId();
        return q.Where(x => x.UserId == uid || x.UserId == null);
    }

    public async Task<IReadOnlyList<PortfolioItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await OwnedItems()
            .AsNoTracking()
            .OrderBy(x => x.Symbol)
            .ToListAsync(ct);

        return items.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<PortfolioItemDto>> GetAllForUserAsync(string userId, CancellationToken ct = default)
    {
        var items = await db.PortfolioItems
            .AsNoTracking()
            .Where(x => x.UserId == userId || x.UserId == null)
            .OrderBy(x => x.Symbol)
            .ToListAsync(ct);

        return items.Select(ToDto).ToList();
    }

    public async Task<PortfolioItemDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var item = await OwnedItems().FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : ToDto(item);
    }

    public async Task<PortfolioItemDto> AddAsync(AddPortfolioItemRequest request, CancellationToken ct = default)
    {
        await using var tx = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
        var item = new PortfolioItem
        {
            UserId           = CurrentUserId(),
            Symbol           = request.Symbol.ToUpperInvariant(),
            CompanyName      = request.CompanyName,
            Shares           = request.Shares,
            AverageCostBasis = request.AverageCostBasis,
            Sector           = string.Empty, // populated in background after response
            Industry         = string.Empty,
            AddedAt          = DateTime.UtcNow,
            TransactionType  = request.TransactionType,
            AccountType      = request.AccountType,
            OpenDate         = request.OpenDate,
            CloseDate        = request.CloseDate,
            ClosingPrice     = request.ClosingPrice,
            DecisionSource   = request.DecisionSource,
            HoldingRole      = request.HoldingRole
        };

        await TradeEpisodeAssigner.AssignOnOpenAsync(db, item, ct);
        db.PortfolioItems.Add(item);
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        mutationClock.Touch();
        mutationClock.MarkTodayDirty();
        return ToDto(item);
    }

    public async Task<PortfolioItemDto> AddManualAsync(AddManualPositionRequest request, CancellationToken ct = default)
    {
        // Generate a unique short symbol so the unique-index constraint is satisfied.
        // Manual positions are never looked up by symbol on Yahoo Finance.
        var sym = "M_" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        var item = new PortfolioItem
        {
            UserId           = CurrentUserId(),
            Symbol           = sym,
            CompanyName      = request.Name,
            Shares           = 1,
            AverageCostBasis = request.AverageCost,
            Sector           = request.Name,
            Industry         = request.Description,
            IsManual         = true,
            ManualMarketValue = request.MarketValue,
            AddedAt          = DateTime.UtcNow
        };

        db.PortfolioItems.Add(item);
        await db.SaveChangesAsync(ct);
        mutationClock.Touch();
        mutationClock.MarkTodayDirty();
        return ToDto(item);
    }

    public async Task<UpdatePortfolioItemResponse?> UpdateAsync(int id, UpdatePortfolioItemRequest request, CancellationToken ct = default)
    {
        await using var tx = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
        var item = await OwnedItems().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return null;
        if (string.Equals(request.TransactionType, "CLOSE", StringComparison.OrdinalIgnoreCase)
            && request.OpenDate.HasValue && request.CloseDate.HasValue && request.CloseDate.Value.Date < request.OpenDate.Value.Date)
            throw new RiskValidationException("A trade cannot close before its opening date.");
        if (await HasRiskHistoryAsync(item, ct)
            && (!string.Equals(item.AccountType, request.AccountType, StringComparison.Ordinal)
                || item.OpenDate?.Date != request.OpenDate?.Date
                || (string.Equals(item.TransactionType, "CLOSE", StringComparison.OrdinalIgnoreCase)
                    && (item.TransactionType != request.TransactionType || item.Shares != request.Shares
                        || item.CloseDate?.Date != request.CloseDate?.Date))))
            throw new RiskValidationException("This change would make existing trade-episode/risk history ambiguous. Keep its account, role and dates; a new trade must be recorded separately.");
        if (request.HoldingRole is not null) await ApplyRoleCorrectionAsync(item, request.HoldingRole, ct);

        // Partial close: user is closing fewer shares than the position currently holds. Rather than
        // silently losing track of the remainder (previously required a manual second "Add Stock"),
        // auto-split it into a new OPEN row carrying over this position's own fields — mirrors exactly
        // what a manual split looked like, minus the extra manual step.
        var isPartialClose = string.Equals(request.TransactionType, "CLOSE", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(item.TransactionType, "CLOSE", StringComparison.OrdinalIgnoreCase)
            && request.Shares < item.Shares;
        var wasClose = string.Equals(item.TransactionType, "CLOSE", StringComparison.OrdinalIgnoreCase);
        var remainingShares = isPartialClose ? item.Shares - request.Shares : 0m;

        item.CompanyName      = request.CompanyName;
        item.Shares           = request.Shares;
        item.AverageCostBasis = request.AverageCostBasis;

        // When OverrideSector is true, always save the supplied values (even empty to clear).
        // When false, only update if non-empty (legacy behaviour).
        if (request.OverrideSector)
        {
            item.Sector            = request.Sector ?? string.Empty;
            item.Industry          = request.Industry ?? string.Empty;
            item.SectorIsOverridden = true;
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.Sector))   item.Sector   = request.Sector;
            if (!string.IsNullOrWhiteSpace(request.Industry)) item.Industry = request.Industry;
        }

        item.TransactionType      = request.TransactionType;
        item.AccountType           = request.AccountType;
        item.OpenDate              = request.OpenDate;
        item.CloseDate             = request.CloseDate;
        item.ClosingPrice          = request.ClosingPrice;
        item.DecisionSource        = request.DecisionSource;
        item.DecisionSourceClosed  = request.DecisionSourceClosed;

        PortfolioItem? remainderItem = null;
        if (isPartialClose)
        {
            remainderItem = new PortfolioItem
            {
                UserId             = item.UserId,
                Symbol             = item.Symbol,
                CompanyName        = item.CompanyName,
                Shares             = remainingShares,
                AverageCostBasis   = item.AverageCostBasis,
                Sector             = item.Sector,
                Industry           = item.Industry,
                SectorIsOverridden = item.SectorIsOverridden,
                AddedAt            = DateTime.UtcNow,
                TransactionType    = "OPEN",
                AccountType        = item.AccountType,
                OpenDate           = item.OpenDate,
                HoldingRole        = item.HoldingRole,
                DecisionSource     = item.DecisionSource,
            };
            db.PortfolioItems.Add(remainderItem);
        }

        if (remainderItem is not null) TradeEpisodeAssigner.PropagateToRemainder(item, remainderItem);
        if (!string.Equals(item.TransactionType, "CLOSE", StringComparison.OrdinalIgnoreCase) && item.TradeEpisodeId is null)
            await TradeEpisodeAssigner.AssignOnOpenAsync(db, item, ct);
        if (!isPartialClose && !wasClose)
            await TradeEpisodeAssigner.RetireOnFullExitAsync(db, item, new RiskUserContext(httpCtx), ct);

        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        mutationClock.Touch();
        mutationClock.MarkTodayDirty();
        return new UpdatePortfolioItemResponse(ToDto(item), remainderItem is null ? null : ToDto(remainderItem));
    }

    public async Task<bool> UpdateHoldingRoleAsync(int id, string holdingRole, CancellationToken ct = default)
    {
        await using var tx = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
        var item = await OwnedItems().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return false;
        var changed = await ApplyRoleCorrectionAsync(item, holdingRole, ct);
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        if (changed) mutationClock.Touch();
        return true;
    }

    /// <summary>
    /// Role is a classification of one lot, not a trade. Only HoldingRole changes: episode id, quantities, prices, dates,
    /// broker stops and prior audit rows are untouched. Swing membership is derived from lot roles, so nothing is moved.
    /// Stages one audit event in the caller's unit of work. Returns false for a no-op.
    /// </summary>
    private async Task<bool> ApplyRoleCorrectionAsync(PortfolioItem item, string newRole, CancellationToken ct)
    {
        if (string.Equals(item.HoldingRole, newRole, StringComparison.Ordinal)) return false;
        var isClose = string.Equals(item.TransactionType, "CLOSE", StringComparison.OrdinalIgnoreCase);
        var toSwing = string.Equals(newRole, "Swing", StringComparison.OrdinalIgnoreCase);
        var symbol = TradeEpisodeResolver.NormSymbol(item.Symbol);
        var account = item.AccountType ?? string.Empty;

        if (item.TradeEpisodeId is { } episodeId)
        {
            var episode = await db.TradeEpisodes.AsNoTracking().FirstOrDefaultAsync(e => e.Id == episodeId, ct);
            if (episode is null || episode.UserId != item.UserId || episode.AccountType != account || episode.Symbol != symbol)
                throw new RiskConflictException("This lot's trade episode is missing or belongs to a different owner, account or symbol, so its role cannot be corrected safely. Resolve the episode history first.");

            if (!isClose)
            {
                var mixes = await db.PortfolioItems.AsNoTracking().AnyAsync(x => x.Id != item.Id && x.TradeEpisodeId == episodeId
                    && x.TransactionType == "OPEN" && x.Shares > 0 && x.HoldingRole != newRole, ct);
                if (mixes)
                    throw new RiskConflictException($"Other open lots of {item.Symbol} in this trade episode have different roles. Changing only this lot would leave one episode with different roles and could close its history or broker-stop link incorrectly. Reclassify the whole episode (all its open lots) or record a separate trade.");
            }
            if (toSwing && !isClose)
            {
                if (episode.Status != EpisodeStatuses.Open)
                    throw new RiskConflictException("This lot's trade episode is closed, so it cannot become a Swing position. Record a new trade instead.");
                if (item.OpenDate is null || item.OpenDate.Value.Date < episode.OpenDate.Date)
                    throw new RiskConflictException("This lot's open date does not fit its trade episode, so a consistent Swing position cannot be established. Correct the history first.");
            }
        }
        else if (toSwing)
        {
            throw new RiskConflictException("This lot has no trade episode, so it cannot safely become a Swing position (no episode or broker stop will be created or assigned automatically). Record the Swing trade separately.");
        }

        var changes = new List<AuditChangeDto>();
        RiskSupport.Diff(changes, "HoldingRole", item.HoldingRole, newRole);
        RiskSupport.StageAudit(db, new RiskUserContext(httpCtx), RiskAuditSources.User, "HoldingRoleCorrected", account, symbol, changes,
            $"Lot #{item.Id} role corrected. Quantity, cost, dates, transactions and broker orders are unchanged.", null,
            episodeId: item.TradeEpisodeId);
        item.HoldingRole = newRole;
        return true;
    }

    public async Task<bool> UpdateNotesAsync(int id, string? notes, CancellationToken ct = default)
    {
        var item = await OwnedItems().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return false;
        item.Notes = notes;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var tx = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
        var item = await OwnedItems().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return false;

        if (await HasRiskHistoryAsync(item, ct))
            throw new RiskValidationException("This transaction has trade-episode/risk history and cannot be deleted. Its history and broker records must be preserved.");
        db.PortfolioItems.Remove(item);
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        mutationClock.Touch();
        mutationClock.MarkTodayDirty();
        return true;
    }

    public async Task RefreshSectorForItemAsync(int id, string symbol, CancellationToken ct = default)
    {
        var (sector, industry) = await marketData.GetSectorAsync(symbol, ct);
        var item = await db.PortfolioItems.FindAsync([id], ct);
        if (item is null || (!string.IsNullOrWhiteSpace(item.Sector) && item.SectorIsOverridden)) return;
        item.Sector   = sector;
        item.Industry = industry;
        await db.SaveChangesAsync(ct);
    }

    private static PortfolioItemDto ToDto(PortfolioItem item) =>
        new(item.Id, item.Symbol, item.CompanyName, item.Shares, item.AverageCostBasis,
            item.Sector, item.Industry, item.SectorIsOverridden, item.IsManual, item.ManualMarketValue, item.AddedAt,
            item.TransactionType, item.AccountType, item.OpenDate, item.CloseDate, item.ClosingPrice, item.HoldingRole,
            item.Notes, item.DecisionSource, item.DecisionSourceClosed);

    public async Task<IReadOnlyList<PortfolioBackupItem>> BackupAsync(CancellationToken ct = default)
    {
        var items = await db.PortfolioItems.AsNoTracking().OrderBy(x => x.Symbol).ToListAsync(ct);
        return items.Select(x => new PortfolioBackupItem(
            x.Symbol, x.CompanyName, x.Shares, x.AverageCostBasis,
            x.Sector, x.Industry, x.SectorIsOverridden, x.IsManual, x.ManualMarketValue,
            x.TransactionType, x.AccountType, x.OpenDate, x.CloseDate, x.ClosingPrice,
            x.HoldingRole, x.Notes, x.AddedAt)).ToList();
    }

    public async Task<int> RestoreAsync(IReadOnlyList<PortfolioBackupItem> items, CancellationToken ct = default)
    {
        await using var tx = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
        if (await db.TradeEpisodes.AnyAsync(ct) || await db.RiskPlans.AnyAsync(ct)
            || await db.BrokerStopRecords.AnyAsync(ct) || await db.RiskAuditEvents.AnyAsync(ct))
            throw new RiskValidationException("Portfolio JSON replacement cannot preserve existing trade episodes, broker orders and risk audit history. Use the approved full SQL database recovery procedure instead. No data was replaced.");
        if (!IsAdmin() && await db.PortfolioItems.AnyAsync(x => x.UserId != CurrentUserId(), ct))
            throw new RiskValidationException("Replacement restore cannot replace other ownership partitions.");
        var existing = await db.PortfolioItems.ToListAsync(ct);
        db.PortfolioItems.RemoveRange(existing);

        var newItems = items.Select(i => new PortfolioItem
        {
            UserId            = CurrentUserId(),
            Symbol            = i.Symbol,
            CompanyName       = i.CompanyName,
            Shares            = i.Shares,
            AverageCostBasis  = i.AverageCostBasis,
            Sector            = i.Sector,
            Industry          = i.Industry,
            SectorIsOverridden = i.SectorIsOverridden,
            IsManual          = i.IsManual,
            ManualMarketValue = i.ManualMarketValue,
            TransactionType   = i.TransactionType,
            AccountType       = i.AccountType,
            OpenDate          = i.OpenDate,
            CloseDate         = i.CloseDate,
            ClosingPrice      = i.ClosingPrice,
            HoldingRole       = i.HoldingRole,
            Notes             = i.Notes,
            AddedAt           = i.AddedAt
        }).ToList();

        db.PortfolioItems.AddRange(newItems);
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return newItems.Count;
    }

    private async Task<bool> HasRiskHistoryAsync(PortfolioItem item, CancellationToken ct) =>
        item.TradeEpisodeId is not null
        || await db.RiskPlans.AnyAsync(x => x.AccountType == item.AccountType && x.Symbol == item.Symbol, ct)
        || await db.BrokerStopRecords.AnyAsync(x => x.AccountType == item.AccountType && x.Symbol == item.Symbol, ct)
        || await db.RiskAuditEvents.AnyAsync(x => x.AccountType == item.AccountType && x.Symbol == item.Symbol, ct);

    public async Task<int> RefreshSectorsAsync(CancellationToken ct = default)
    {
        var items = await db.PortfolioItems.ToListAsync(ct);
        // Process all items; throttle to 3 concurrent requests to avoid Yahoo rate limiting
        var semaphore = new SemaphoreSlim(3, 3);
        int updated  = 0;

        var tasks = items
            .Where(item => !item.IsManual && !item.SectorIsOverridden)  // skip manual positions and user overrides
            .Select(async item =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var (sector, industry) = await marketData.GetSectorAsync(item.Symbol, ct);
                if (!string.IsNullOrWhiteSpace(sector))
                {
                    item.Sector   = sector;
                    item.Industry = industry;
                    Interlocked.Increment(ref updated);
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        if (updated > 0)
            await db.SaveChangesAsync(ct);

        return updated;
    }
}
