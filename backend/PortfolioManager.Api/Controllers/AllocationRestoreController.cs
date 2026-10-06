using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Trader")]
[Route("api/allocation/restore")]
public sealed class AllocationRestoreController(IAllocationRestoreService service) : ControllerBase
{
    /// <summary>Restores cash and options atomically, rejecting missing account types before replacing data.</summary>
    [HttpPost]
    public async Task<ActionResult<AllocationRestoreResponse>> Restore(AllocationRestoreRequest request, CancellationToken ct)
        => Ok(await service.RestoreAsync(request, ct));
}
