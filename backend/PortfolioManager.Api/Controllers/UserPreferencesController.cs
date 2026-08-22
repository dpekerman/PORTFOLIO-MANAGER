using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Services;
using System.Security.Claims;

namespace PortfolioManager.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/users/preferences")]
public class UserPreferencesController(IUserPreferenceService service) : ControllerBase
{
    private string CurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet]
    public async Task<ActionResult<Dictionary<string, string>>> GetAll(CancellationToken ct)
    {
        var uid = CurrentUserId();
        if (string.IsNullOrEmpty(uid)) return Unauthorized();
        return Ok(await service.GetAllAsync(uid, ct));
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> Upsert(string key, [FromBody] UpsertPreferenceRequest body, CancellationToken ct)
    {
        var uid = CurrentUserId();
        if (string.IsNullOrEmpty(uid)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(key)) return BadRequest("Key is required.");
        await service.UpsertAsync(uid, key, body.Value, ct);
        return NoContent();
    }

    [HttpDelete("{key}")]
    public async Task<IActionResult> Delete(string key, CancellationToken ct)
    {
        var uid = CurrentUserId();
        if (string.IsNullOrEmpty(uid)) return Unauthorized();
        await service.DeleteAsync(uid, key, ct);
        return NoContent();
    }
}

public record UpsertPreferenceRequest(string Value);
