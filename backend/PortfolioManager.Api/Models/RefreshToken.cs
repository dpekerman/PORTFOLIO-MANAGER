namespace PortfolioManager.Api.Models;

public class RefreshToken
{
    public int Id { get; set; }
    public required string Token { get; set; }   // SHA-256 hash of the raw cookie value
    public required string UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser? User { get; set; }
}
