using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.Valuation;

public interface IValuationConsumerAdapter
{
    ValuationConsumerEvidence Project(SharedValuationSnapshot snapshot, string consumer);
    IReadOnlyList<Pm029SecurityValuationInput> AdaptPortfolio(IReadOnlyList<PortfolioLotEvidence> lots);
}
