using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IPortfolioValueHistoryService
{
    Task<IReadOnlyList<PortfolioValueHistoryDto>> GetLatestAsync(int count, CancellationToken ct);
    Task SaveAsync(decimal totalValue, decimal stocksValue, decimal cashValue, decimal optionsValue, string recordedDate, CancellationToken ct);
    Task<bool> ExistsForDateAsync(string recordedDate, CancellationToken ct);
}

public sealed class PortfolioValueHistoryService(AppDbContext db) : IPortfolioValueHistoryService
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
}
