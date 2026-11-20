using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services.Opportunities;

namespace PortfolioManager.Api.Services.Valuation;

public interface IValuationConsumerAdapter
{
    ValuationConsumerEvidence Project(SharedValuationSnapshot snapshot, string consumer);
    (FitResult Fit, bool IsAuthoritative) ProjectShadowFit(SharedValuationSnapshot snapshot, string symbol);
    IReadOnlyList<Pm029SecurityValuationInput> AdaptPortfolio(IReadOnlyList<PortfolioLotEvidence> lots);
}
