using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PortfolioBetaController(IPortfolioBetaService betaService) : ControllerBase
{
    /// <summary>Calculates portfolio beta without any overrides.</summary>
    [HttpGet]
    public async Task<ActionResult<PortfolioBetaResult>> Get(CancellationToken ct)
    {
        var result = await betaService.CalculateAsync(ct);
        return Ok(result);
    }

    /// <summary>Calculates portfolio beta using user-supplied beta overrides (keyed by symbol).</summary>
    [HttpPost("calculate")]
    public async Task<ActionResult<PortfolioBetaResult>> Calculate(
        [FromBody] PortfolioBetaRequest request, CancellationToken ct)
    {
        var overrides = request.BetaOverrides?.Count > 0 ? request.BetaOverrides : null;
        var result = await betaService.CalculateAsync(ct, overrides);
        return Ok(result);
    }
}
