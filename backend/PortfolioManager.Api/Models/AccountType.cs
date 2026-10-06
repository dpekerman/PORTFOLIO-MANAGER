namespace PortfolioManager.Api.Models;

public sealed class AccountType
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public Guid Version { get; set; } = Guid.NewGuid();
}

/// <summary>A shared account choice and its global record usage.</summary>
public sealed record AccountTypeResponse(int Id, string Name, Guid Version, int StockCount, int OptionCount, int CashCount);

/// <summary>The name of a new shared account type.</summary>
public sealed record CreateAccountTypeRequest(string Name);

/// <summary>A version-checked account rename.</summary>
public sealed record UpdateAccountTypeRequest(string Name, Guid Version);

/// <summary>The committed rename and its affected record counts.</summary>
public sealed record AccountTypeRenameResponse(string OldName, AccountTypeResponse Item);
