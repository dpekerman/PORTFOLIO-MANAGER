using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.Valuation;

public interface IBankOfCanadaFxProvider
{
    Task<IReadOnlyList<Pm029FxObservation>> GetUsdCadObservationsAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default);
}
