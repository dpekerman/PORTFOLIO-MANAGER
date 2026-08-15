using FluentAssertions;

namespace PortfolioManager.Tests;

/// <summary>
/// Tests for email Turn Strength label construction.
/// The email combines TrendShift + TurnStrength with " — " separator.
/// Normal strength shows no suffix; Early/Strong/Explosive append the label.
/// </summary>
public class EmailLabelTests
{
    private static string TrendShiftDisplay(string shift, string strength)
    {
        if (string.IsNullOrEmpty(shift) || shift == "Waiting") return shift ?? "Waiting";
        // Strip emoji prefix for display
        var clean = shift
            .Replace("🟢 ", "")
            .Replace("🟡 ", "")
            .Replace("🔴 ", "");
        if (string.IsNullOrEmpty(strength) || strength == "Normal") return clean;
        return $"{clean} — {strength}";
    }

    [Fact]
    public void Normal_ShowsNoSuffix() =>
        TrendShiftDisplay("🟢 Bull Turn", "Normal").Should().Be("Bull Turn");

    [Fact]
    public void Early_AppendsSuffix() =>
        TrendShiftDisplay("🟢 Bull Turn", "Early").Should().Be("Bull Turn — Early");

    [Fact]
    public void Strong_AppendsSuffix() =>
        TrendShiftDisplay("🟢 Bull Turn", "Strong").Should().Be("Bull Turn — Strong");

    [Fact]
    public void Explosive_AppendsSuffix() =>
        TrendShiftDisplay("🟢 Bull Turn", "Explosive").Should().Be("Bull Turn — Explosive");

    [Fact]
    public void EmptyStrength_ShowsNoSuffix() =>
        TrendShiftDisplay("🟡 Stabilizing", "").Should().Be("Stabilizing");

    [Fact]
    public void Waiting_ReturnsWaiting() =>
        TrendShiftDisplay("Waiting", "").Should().Be("Waiting");

    // ── Confirmed vs Awaiting classification ──────────────────────────────────

    [Theory]
    [InlineData("🟢 Bull Turn",   true)]
    [InlineData("🟢 Bear Turn",   true)]
    [InlineData("🟡 Stabilizing", false)]
    [InlineData("🔴 Still Falling", false)]
    [InlineData("🔴 Still Rising",  false)]
    [InlineData("Waiting",          false)]
    [InlineData("",                 false)]
    public void IsTurn_CorrectlyIdentifiesReversal(string shift, bool expected)
    {
        bool isTurn = shift.Contains("Bull Turn") || shift.Contains("Bear Turn");
        isTurn.Should().Be(expected);
    }
}
