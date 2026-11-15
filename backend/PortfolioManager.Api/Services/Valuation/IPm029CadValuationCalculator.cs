using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.Valuation;

public interface IPm029CadValuationCalculator
{
    Pm029CadValuationResult Evaluate(Pm029ValuationRequest request);
}
