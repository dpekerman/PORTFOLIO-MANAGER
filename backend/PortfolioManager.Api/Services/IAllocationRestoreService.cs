using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IAllocationRestoreService
{
    Task<AllocationRestoreResponse> RestoreAsync(AllocationRestoreRequest request, CancellationToken ct);
}
