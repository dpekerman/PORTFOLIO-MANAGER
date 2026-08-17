using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    AppDbContext db,
    IConfiguration configuration) : ControllerBase
{
    private static readonly string[] ValidRoles = ["Admin", "Trader", "Viewer"];

    [HttpGet("setup-required")]
    [AllowAnonymous]
    public async Task<ActionResult<SetupRequiredResponse>> SetupRequired(CancellationToken ct)
    {
        var hasUsers = await userManager.Users.AnyAsync(ct);
        return Ok(new SetupRequiredResponse(!hasUsers));
    }

    [HttpPost("setup")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Setup([FromBody] SetupRequest request, CancellationToken ct)
    {
        if (await userManager.Users.AnyAsync(ct))
            return Conflict(new { message = "System already configured. Contact an administrator." });

        if (request.Password != request.ConfirmPassword)
            return BadRequest(new { message = "Passwords do not match." });

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

        await userManager.AddToRoleAsync(user, "Admin");
        return await IssueTokensAsync(user, ct);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized(new { message = "Invalid email or password." });

        return await IssueTokensAsync(user, ct);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
    {
        var rawToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrWhiteSpace(rawToken))
            return Unauthorized();

        var hashed = tokenService.HashToken(rawToken);
        var stored = await db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == hashed && !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow, ct);

        if (stored?.User is null)
            return Unauthorized();

        // Rotate: revoke used token, issue a new pair
        stored.IsRevoked = true;
        await db.SaveChangesAsync(ct);

        return await IssueTokensAsync(stored.User, ct);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var rawToken = Request.Cookies["refreshToken"];
        if (!string.IsNullOrWhiteSpace(rawToken))
        {
            var hashed = tokenService.HashToken(rawToken);
            var stored = await db.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == hashed, ct);
            if (stored is not null)
            {
                stored.IsRevoked = true;
                await db.SaveChangesAsync(ct);
            }
        }

        Response.Cookies.Delete("refreshToken");
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserInfoDto>> Me()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var roles = await userManager.GetRolesAsync(user);
        return Ok(new UserInfoDto(user.Id, user.DisplayName, user.Email!, roles));
    }

    private async Task<ActionResult<AuthResponse>> IssueTokensAsync(ApplicationUser user, CancellationToken ct = default)
    {
        var roles = await userManager.GetRolesAsync(user);
        var accessToken = tokenService.GenerateAccessToken(user, roles);
        var (rawToken, hashedToken) = tokenService.GenerateRefreshToken();

        var expiryDays = configuration.GetValue<int>("Jwt:RefreshTokenExpiryDays", 7);

        db.RefreshTokens.Add(new RefreshToken
        {
            Token = hashedToken,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);

        Response.Cookies.Append("refreshToken", rawToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(expiryDays),
            Path = "/api/auth"     // limit cookie scope to auth endpoints only
        });

        return Ok(new AuthResponse(accessToken, new UserInfoDto(user.Id, user.DisplayName, user.Email!, roles)));
    }
}
