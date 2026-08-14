using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController(UserManager<ApplicationUser> userManager) : ControllerBase
{
    private static readonly string[] ValidRoles = ["Admin", "Trader", "Viewer"];

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserInfoDto>>> GetAll(CancellationToken ct)
    {
        var users = await userManager.Users.OrderBy(u => u.DisplayName).ToListAsync(ct);
        var result = new List<UserInfoDto>();
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            result.Add(new UserInfoDto(user.Id, user.DisplayName, user.Email!, roles));
        }
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<UserInfoDto>> Create([FromBody] CreateUserRequest request)
    {
        if (!ValidRoles.Contains(request.Role))
            return BadRequest(new { message = "Role must be Admin, Trader, or Viewer." });

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        await userManager.AddToRoleAsync(user, request.Role);
        var roles = await userManager.GetRolesAsync(user);
        return Ok(new UserInfoDto(user.Id, user.DisplayName, user.Email!, roles));
    }

    [HttpPut("{id}/role")]
    public async Task<IActionResult> AssignRole(string id, [FromBody] AssignRoleRequest request)
    {
        if (!ValidRoles.Contains(request.Role))
            return BadRequest(new { message = "Role must be Admin, Trader, or Viewer." });

        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        // Prevent removing the last Admin
        if (request.Role != "Admin" && await userManager.IsInRoleAsync(user, "Admin"))
        {
            var adminCount = (await userManager.GetUsersInRoleAsync("Admin")).Count;
            if (adminCount <= 1)
                return BadRequest(new { message = "Cannot change role of the last administrator." });
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        await userManager.RemoveFromRolesAsync(user, currentRoles);
        await userManager.AddToRoleAsync(user, request.Role);

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var currentUserId = userManager.GetUserId(User);
        if (id == currentUserId)
            return BadRequest(new { message = "Cannot delete your own account." });

        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        if (await userManager.IsInRoleAsync(user, "Admin"))
        {
            var adminCount = (await userManager.GetUsersInRoleAsync("Admin")).Count;
            if (adminCount <= 1)
                return BadRequest(new { message = "Cannot delete the last administrator." });
        }

        await userManager.DeleteAsync(user);
        return NoContent();
    }
}
