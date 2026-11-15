using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.Valuation;

public interface ISharedShadowValuationService
{
    SharedValuationSnapshot Evaluate(ShadowValuationRequest request);
}
