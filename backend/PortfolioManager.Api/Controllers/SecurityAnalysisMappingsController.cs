using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using System.Security.Claims;

namespace PortfolioManager.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/security-analysis-mappings")]
public sealed class SecurityAnalysisMappingsController(ISecurityAnalysisResolver resolver) : ControllerBase
{
    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet("{tradingTicker}")]
    public async Task<ActionResult<SecurityAnalysisMappingDto>> Get(string tradingTicker, CancellationToken ct)
    {
        var mapping = await resolver.ResolveAsync(tradingTicker, CurrentUserId(), ct);
        return Ok(ToDto(mapping));
    }

    [Authorize(Roles = "Admin,Trader")]
    [HttpPost("{tradingTicker}/validate")]
    public async Task<IActionResult> Validate(string tradingTicker, [FromBody] SaveSecurityAnalysisMappingRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tradingTicker) || string.IsNullOrWhiteSpace(request.UnderlyingTicker))
            return BadRequest("Both trading and underlying tickers are required.");

        return await resolver.ValidateUnderlyingTickerAsync(request.UnderlyingTicker, ct)
            ? NoContent()
            : BadRequest("The underlying ticker is unavailable or lacks sufficient market history.");
    }

    [Authorize(Roles = "Admin,Trader")]
    [HttpPut("{tradingTicker}")]
    public async Task<ActionResult<SecurityAnalysisMappingDto>> Save(
        string tradingTicker,
        [FromBody] SaveSecurityAnalysisMappingRequest request,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var mapping = await resolver.SaveUserMappingAsync(
            tradingTicker, request.UnderlyingTicker, userId, request.UseUnderlyingForAnalysis, ct);
        return Ok(ToDto(mapping));
    }

    [Authorize(Roles = "Admin,Trader")]
    [HttpDelete("{tradingTicker}")]
    public async Task<IActionResult> Delete(string tradingTicker, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        return await resolver.RemoveUserMappingAsync(tradingTicker, userId, ct) ? NoContent() : NotFound();
    }

    private static SecurityAnalysisMappingDto ToDto(ResolvedSecurityAnalysis mapping) =>
        new(
            mapping.TradingTicker,
            mapping.AnalysisTicker,
            mapping.AnalysisMarket,
            mapping.AnalysisCurrency,
            mapping.UsesUnderlyingSecurity,
            mapping.ResolutionStatus,
            mapping.MappingSource,
            mapping.DataError);
}