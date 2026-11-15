namespace PortfolioManager.Api.Services.Valuation;

public interface IFxPublicationCalendar
{
    bool Covers(DateOnly fromInclusive, DateOnly toInclusive);
    IReadOnlyList<Pm029FxPublicationDay> GetDays(DateOnly fromInclusive, DateOnly toInclusive);
}

public sealed record Pm029FxPublicationDay(
    DateOnly Date,
    bool IsPublicationSession,
    DateTimeOffset? ExpectedPublicationAtUtc);
