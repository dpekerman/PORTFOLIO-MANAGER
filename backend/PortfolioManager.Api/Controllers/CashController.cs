using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CashController(ICashService cashService, ICashLedgerQueryService cashLedger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CashItemDto>>> GetAll(CancellationToken ct)
    {
        var items = await cashService.GetAllAsync(ct);
        return Ok(items);
    }

    /// <summary>The accounting boundary: dates before this use frozen legacy history; dates on/after it
    /// are reconstructed authoritatively from the cash ledger.</summary>
    [HttpGet("ledger-start-date")]
    public async Task<ActionResult> GetLedgerStartDate(CancellationToken ct)
    {
        var date = await cashLedger.GetLedgerStartDateAsync(ct);
        return Ok(new { ledgerStartDate = date.ToString("yyyy-MM-dd") });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CashItemDto>> GetById(int id, CancellationToken ct)
    {
        var item = await cashService.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [Authorize(Roles = "Admin,Trader")]
    [HttpPost]
    public async Task<ActionResult<CashItemDto>> Add([FromBody] AddCashItemRequest request, CancellationToken ct)
    {
        try
        {
            var item = await cashService.AddAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
        catch (DbUpdateException)
        {
            // Filtered unique index violation — duplicate OpeningBalance for this account+date.
            return BadRequest("A conflicting cash ledger entry already exists for this account and date.");
        }
    }

    /// <summary>"Adjust Balance": types the desired new total; backend computes the delta and inserts one
    /// new dated ledger row. Rejects a Type/delta direction mismatch or a zero-delta request (400).</summary>
    [Authorize(Roles = "Admin,Trader")]
    [HttpPost("adjust-balance")]
    public async Task<ActionResult<CashItemDto>> AdjustBalance([FromBody] AdjustCashBalanceRequest request, CancellationToken ct)
    {
        try
        {
            var item = await cashService.AdjustBalanceAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize(Roles = "Admin,Trader")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<CashItemDto>> Update(int id, [FromBody] UpdateCashItemRequest request, CancellationToken ct)
    {
        try
        {
            var item = await cashService.UpdateAsync(id, request, ct);
            return item is null ? NotFound() : Ok(item);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize(Roles = "Admin,Trader")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            var deleted = await cashService.DeleteAsync(id, ct);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Exports all cash items as a JSON backup payload.</summary>
    [HttpGet("backup")]
    public async Task<ActionResult<IReadOnlyList<CashBackupItem>>> Backup(CancellationToken ct)
    {
        var items = await cashService.BackupAsync(ct);
        return Ok(items);
    }

    /// <summary>Clears all cash items and restores from the provided backup payload.</summary>
    [Authorize(Roles = "Admin,Trader")]
    [HttpPost("restore")]
    public async Task<IActionResult> Restore([FromBody] RestoreCashRequest request, CancellationToken ct)
    {
        var count = await cashService.RestoreAsync(request.Items, ct);
        return Ok(new { restored = count });
    }
}