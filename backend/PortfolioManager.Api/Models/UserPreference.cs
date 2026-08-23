namespace PortfolioManager.Api.Models;

public class UserPreference
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string PreferenceKey { get; set; } = string.Empty;
    public string PreferenceValue { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
