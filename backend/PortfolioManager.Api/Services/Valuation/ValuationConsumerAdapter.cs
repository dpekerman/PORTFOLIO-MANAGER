using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services.Opportunities;

namespace PortfolioManager.Api.Services.Valuation;

public sealed class ValuationConsumerAdapter : IValuationConsumerAdapter
{
    public ValuationConsumerEvidence Project(SharedValuationSnapshot snapshot, string consumer)
    {
        if (consumer is not ("Portfolio" or "Dashboard" or "Allocation" or "FinalAction" or "Pm029PortfolioFit"))
            throw new ArgumentException("Unknown valuation consumer.", nameof(consumer));
        return new(consumer, snapshot.Valuation.AsOfUtc, snapshot.Valuation.Complete,
            snapshot.Valuation.DenominatorCad, snapshot.Valuation.StockOnlyCad,
            snapshot.Exposures, snapshot.Valuation.IssueCodes, false);
    }

    public IReadOnlyList<Pm029SecurityValuationInput> AdaptPortfolio(IReadOnlyList<PortfolioLotEvidence> lots) =>
        lots.Where(lot => !string.Equals(lot.Summary.Item.TransactionType, "CLOSE", StringComparison.OrdinalIgnoreCase))
            .Select(lot =>
            {
                var item = lot.Summary.Item;
                var quote = lot.Summary.Quote;
                var quoteUtc = quote?.SourcePriceTimestamp is > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(quote.SourcePriceTimestamp.Value)
                    : (DateTimeOffset?)null;
                return new Pm029SecurityValuationInput(
                    $"stock:{item.Id}", lot.OwnerId, item.AccountType, item.Symbol, item.Sector, item.HoldingRole,
                    item.Shares, lot.ListingCurrency, quote?.Currency,
                    item.IsManual ? null : quote?.CurrentPrice, quoteUtc.HasValue ? "Yahoo Finance" : null,
                    quoteUtc, quoteUtc.HasValue ? TradingCalendar.TorontoDate(quoteUtc.Value.UtcDateTime) : null,
                    lot.ExpectedSession, lot.ExpectedSessionCloseUtc);
            }).ToArray();
}
