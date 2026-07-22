using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IPortfolioValueHistoryService
{
    Task<IReadOnlyList<PortfolioValueHistoryDto>> GetLatestAsync(int count, CancellationToken ct);
    Task SaveAsync(decimal totalValue, decimal stocksValue, decimal cashValue, decimal optionsValue, string recordedDate, CancellationToken ct);
    Task<bool> ExistsForDateAsync(string recordedDate, CancellationToken ct);
    /// <summary>Calculates and persists the current portfolio value. If a record for today already exists it is overwritten.</summary>
    Task<PortfolioValueHistoryDto> RecordCurrentValueAsync(CancellationToken ct);
}

public sealed class PortfolioValueHistoryService(AppDbContext db, IMarketDataProvider marketData) : IPortfolioValueHistoryService
{
    public async Task<IReadOnlyList<PortfolioValueHistoryDto>> GetLatestAsync(int count, CancellationToken ct)
    {
        return await db.PortfolioValueHistories
            .OrderByDescending(h => h.RecordedAt)
            .Take(count)
            .Select(h => new PortfolioValueHistoryDto(h.Id, h.RecordedAt, h.RecordedDate, h.TotalValue, h.StocksValue, h.CashValue, h.OptionsValue))
            .ToListAsync(ct);
    }

    public async Task SaveAsync(decimal totalValue, decimal stocksValue, decimal cashValue, decimal optionsValue, string recordedDate, CancellationToken ct)
    {
        db.PortfolioValueHistories.Add(new PortfolioValueHistory
        {
            RecordedAt = DateTime.UtcNow,
            RecordedDate = recordedDate,
            TotalValue = totalValue,
            StocksValue = stocksValue,
            CashValue = cashValue,
            OptionsValue = optionsValue
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistsForDateAsync(string recordedDate, CancellationToken ct)
        => await db.PortfolioValueHistories.AnyAsync(h => h.RecordedDate == recordedDate, ct);

    public async Task<PortfolioValueHistoryDto> RecordCurrentValueAsync(CancellationToken ct)
    {
        var recordedDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

        // ── Stocks market value ─────────────────────────────────────────────
        var portfolioItems = await db.PortfolioItems
            .Where(p => p.TransactionType != "CLOSE")
            .ToListAsync(ct);

        var nonManualSymbols = portfolioItems
            .Where(p => !p.IsManual)
            .Select(p => p.Symbol)
            .Distinct()
            .ToList();

        decimal stocksValue = 0m;
        if (nonManualSymbols.Count > 0)
        {
            var quotes = await marketData.GetBatchQuotesAsync(nonManualSymbols, ct);
            foreach (var item in portfolioItems.Where(p => !p.IsManual))
            {
                var price = quotes.TryGetValue(item.Symbol, out var q) ? q.CurrentPrice : item.AverageCostBasis;
                stocksValue += price * item.Shares;
            }
        }
        foreach (var item in portfolioItems.Where(p => p.IsManual))
            stocksValue += item.ManualMarketValue ?? item.AverageCostBasis;

        // ── Cash ────────────────────────────────────────────────────────────
        var cashValue = await db.CashItems.SumAsync(c => c.Amount, ct);

        // ── Options ─────────────────────────────────────────────────────────
        var optionsValue = await db.OptionItems
            .Where(o => o.TransactionType != "CLOSE")
            .SumAsync(o => o.MarketPrice * o.NumberOfContracts * 100, ct);

        var total = stocksValue + cashValue + optionsValue;

        // Upsert: remove existing record for today if present, then insert fresh
        var existing = await db.PortfolioValueHistories
            .Where(h => h.RecordedDate == recordedDate)
            .ToListAsync(ct);
        if (existing.Count > 0)
            db.PortfolioValueHistories.RemoveRange(existing);

        var entity = new PortfolioValueHistory
        {
            RecordedAt = DateTime.UtcNow,
            RecordedDate = recordedDate,
            TotalValue = total,
            StocksValue = stocksValue,
            CashValue = cashValue,
            OptionsValue = optionsValue
        };
        db.PortfolioValueHistories.Add(entity);
        await db.SaveChangesAsync(ct);

        return new PortfolioValueHistoryDto(entity.Id, entity.RecordedAt, entity.RecordedDate,
            entity.TotalValue, entity.StocksValue, entity.CashValue, entity.OptionsValue);
    }
}
