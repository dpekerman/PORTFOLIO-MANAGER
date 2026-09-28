namespace PortfolioManager.Api.Services;

/// <summary>Single source of truth for CashFlowType sign direction and external-flow classification.
/// Used by CashService.AddAsync (derive sign) and AdjustBalanceAsync (validate direction).</summary>
public static class CashFlowTypeRules
{
    public const string OpeningBalance = "OpeningBalance";
    public const string Deposit = "Deposit";
    public const string Withdrawal = "Withdrawal";
    public const string TradeProceeds = "TradeProceeds";
    public const string TradePurchase = "TradePurchase";
    public const string Dividend = "Dividend";
    public const string Interest = "Interest";
    public const string Fee = "Fee";
    public const string Tax = "Tax";
    public const string AdjustmentIncrease = "AdjustmentIncrease";
    public const string AdjustmentDecrease = "AdjustmentDecrease";

    /// <summary>Types a user may pick when adding/adjusting cash. OpeningBalance is migration/admin-only.</summary>
    public static readonly IReadOnlyList<string> SelectableTypes =
    [
        Deposit, Withdrawal, TradeProceeds, TradePurchase, Dividend, Interest, Fee, Tax,
        AdjustmentIncrease, AdjustmentDecrease
    ];

    private static readonly HashSet<string> PositiveOnly = new(StringComparer.OrdinalIgnoreCase)
    {
        OpeningBalance, Deposit, TradeProceeds, Dividend, Interest, AdjustmentIncrease
    };

    private static readonly HashSet<string> NegativeOnly = new(StringComparer.OrdinalIgnoreCase)
    {
        Withdrawal, TradePurchase, Fee, Tax, AdjustmentDecrease
    };

    private static readonly HashSet<string> ExternalFlows = new(StringComparer.OrdinalIgnoreCase)
    {
        Deposit, Withdrawal
    };

    public static bool IsKnownType(string? type) =>
        !string.IsNullOrWhiteSpace(type) && (PositiveOnly.Contains(type) || NegativeOnly.Contains(type));

    /// <summary>+1 for positive-only types, -1 for negative-only types, null if the type is unknown.</summary>
    public static int? AllowedSign(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return null;
        if (PositiveOnly.Contains(type)) return 1;
        if (NegativeOnly.Contains(type)) return -1;
        return null;
    }

    public static bool IsExternalFlow(string? type) =>
        !string.IsNullOrWhiteSpace(type) && ExternalFlows.Contains(type);

    /// <summary>Derives the signed amount from a user-entered positive magnitude and a Type.
    /// Throws if the type is unknown or OpeningBalance (migration-only, never user-derivable).</summary>
    public static decimal DeriveSignedAmount(string type, decimal positiveMagnitude)
    {
        if (string.Equals(type, OpeningBalance, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("OpeningBalance is migration/admin-only and cannot be entered directly.", nameof(type));
        var sign = AllowedSign(type) ?? throw new ArgumentException($"Unknown CashFlowType '{type}'.", nameof(type));
        return sign * Math.Abs(positiveMagnitude);
    }
}
