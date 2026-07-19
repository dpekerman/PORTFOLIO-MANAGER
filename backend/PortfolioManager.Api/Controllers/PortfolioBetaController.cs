using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PortfolioBetaController(IPortfolioBetaService betaService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PortfolioBetaResult>> Get(CancellationToken ct)
    {
        var result = await betaService.CalculateAsync(ct);
        return Ok(result);
    }
}
