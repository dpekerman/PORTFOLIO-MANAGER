using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public sealed class AllocationRestoreService(AppDbContext db, IOptionService options, ICashService cash) : IAllocationRestoreService
{
    public async Task<AllocationRestoreResponse> RestoreAsync(AllocationRestoreRequest request, CancellationToken ct)
    {
        await using var tx = await AccountTypeWriteGuard.BeginAsync(db, ct);
        var optionCount = await options.RestoreAsync(request.Options, ct);
        var cashCount = await cash.RestoreAsync(request.Cash, ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return new(cashCount, optionCount);
    }
}
