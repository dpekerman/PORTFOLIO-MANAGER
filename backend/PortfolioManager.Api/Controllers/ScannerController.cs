using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScannerController(IRsiScannerService scanner) : ControllerBase
{
    [HttpGet("rsi")]
    public async Task<ActionResult<ScannerResponse>> GetRsiScan(CancellationToken ct)
    {
        var result = await scanner.ScanAsync(ct);
        return Ok(result);
    }
}
