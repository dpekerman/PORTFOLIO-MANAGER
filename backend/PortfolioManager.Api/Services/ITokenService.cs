using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface ITokenService
{
    string GenerateAccessToken(ApplicationUser user, IList<string> roles);
    (string Raw, string Hashed) GenerateRefreshToken();
    string HashToken(string rawToken);
}
