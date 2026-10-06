using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/account-types")]
public sealed class AccountTypesController(IAccountTypeService service) : ControllerBase
{
    /// <summary>Lists shared account types and their global usage.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccountTypeResponse>>> GetAll(CancellationToken ct)
    {
        await service.InitializeAsync(ct);
        return Ok(await service.GetAllAsync(ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AccountTypeResponse>> GetById(int id, CancellationToken ct)
    {
        var item = (await service.GetAllAsync(ct)).FirstOrDefault(x => x.Id == id);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>Adds an account type shared by all users.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<AccountTypeResponse>> Add(CreateAccountTypeRequest request, CancellationToken ct)
    {
        var item = await service.AddAsync(request.Name, ct);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    /// <summary>Atomically renames an account across all users and transactions.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<AccountTypeRenameResponse>> Rename(int id, UpdateAccountTypeRequest request, CancellationToken ct)
        => Ok(await service.RenameAsync(id, request, ct));

    /// <summary>Deletes a type only when no stock, option, or cash record uses it.</summary>
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromQuery] Guid version, CancellationToken ct)
    {
        await service.DeleteAsync(id, version, ct);
        return NoContent();
    }
}
