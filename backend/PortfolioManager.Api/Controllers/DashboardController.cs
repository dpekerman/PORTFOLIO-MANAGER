using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(IDashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get(CancellationToken ct)
    {
        var snapshot = await dashboard.GetLatestAsync(CurrentUserId(), ct);
        return snapshot is null ? NoContent() : Ok(snapshot);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<DashboardResponse>> Refresh(CancellationToken ct)
        => Ok(await dashboard.RebuildAsync(CurrentUserId(), ct));

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
}
