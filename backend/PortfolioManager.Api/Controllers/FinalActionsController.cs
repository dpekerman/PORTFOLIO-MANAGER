using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Services.FinalAction;
using System.Security.Claims;

namespace PortfolioManager.Api.Controllers;

/// <summary>The one canonical Final Action engine, exposed to the UI, Action Center and automation.</summary>
[Authorize(Roles = "Admin,Trader")]
[ApiController]
[Route("api/final-actions")]
public sealed class FinalActionsController(IFinalActionService service, ILogger<FinalActionsController> logger) : ControllerBase
{
    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    /// <summary>Canonical Final Action for every watchlist symbol and open holding (cached ~10 min; ?fresh=true rescans).
    /// Each evaluation also refreshes the portfolio actions the Dashboard Action Center reads.</summary>
    [HttpGet]
    public async Task<ActionResult<FinalActionSnapshotDto>> Get([FromQuery] bool fresh, CancellationToken ct)
    {
        var userId = CurrentUserId();
        var snapshot = await service.ComputeAsync(userId, fresh, ct);
        try
        {
            await service.PublishPortfolioActionsAsync(userId, snapshot, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "[FinalActions] Publishing portfolio actions to the snapshot failed.");
        }
        return Ok(snapshot);
    }

    [HttpGet("catalog")]
    public ActionResult<IReadOnlyList<FinalActionDefinition>> Catalog() => Ok(FinalActionCatalog.All);
}
