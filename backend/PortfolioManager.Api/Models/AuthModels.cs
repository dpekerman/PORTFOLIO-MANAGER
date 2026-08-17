namespace PortfolioManager.Api.Models;

public record LoginRequest(string Email, string Password);

public record SetupRequest(string DisplayName, string Email, string Password, string ConfirmPassword);

public record CreateUserRequest(string DisplayName, string Email, string Password, string Role);

public record AssignRoleRequest(string Role);

public record AuthResponse(string AccessToken, UserInfoDto User);

public record UserInfoDto(string Id, string DisplayName, string Email, IList<string> Roles);

public record SetupRequiredResponse(bool Required);
