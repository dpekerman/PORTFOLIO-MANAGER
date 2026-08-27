using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/data")]
public sealed class DataRefreshController(IDataRefreshService dataRefresh) : ControllerBase
{
    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpPost("refresh")]
    public async Task<ActionResult<DataRefreshResultDto>> Refresh(CancellationToken ct)
    {
        var uid = CurrentUserId();
        if (string.IsNullOrEmpty(uid)) return Unauthorized();
        var result = await dataRefresh.RefreshAllAsync(uid, ct);
        return Ok(result);
    }
}
