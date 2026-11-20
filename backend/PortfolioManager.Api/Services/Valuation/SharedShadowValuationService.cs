using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.Valuation;

public sealed class SharedShadowValuationService(
    IVerifiedFinancialEvidenceAdapter adapter,
    IPm029CadValuationCalculator calculator) : ISharedShadowValuationService
{
    public SharedValuationSnapshot Evaluate(ShadowValuationRequest request)
    {
        if (request.DatasetKind is not ("Synthetic" or "VerifiedReal"))
            throw new ArgumentException("Shadow valuation requires an explicit dataset kind.", nameof(request));
        var adapted = adapter.Adapt(request.Cash, request.Options, request.AsOfUtc,
            request.Session, request.RequestingUserId, request.AccountScope);
        var components = request.Securities.Cast<Pm029ValuationComponentInput>().Concat(adapted.Components).ToArray();
        var valuation = calculator.Evaluate(new(request.AsOfUtc, request.Session, request.RequestingUserId,
            request.AccountScope, request.MaximumFxPublicationSessions, components, request.Fx));
        if (adapted.Issues.Count > 0)
            valuation = valuation with
            {
                Complete = false, DenominatorCad = null, StockOnlyCad = null,
                IssueCodes = valuation.IssueCodes.Concat(adapted.Issues.Select(issue => issue.Code))
                    .Distinct().Order(StringComparer.Ordinal).ToArray()
            };
        var exposures = request.Policies.Select(policy =>
        {
            var evidence = Pm029PortfolioFitEvidenceCalculator.Evaluate(valuation, policy.Symbol,
                policy.PositionLimitPct, policy.Sector, policy.SectorTargetPct, policy.SectorTolerancePp);
            return new ValuationExposureProjection(policy.Symbol, evidence,
                evidence.RolePolicyAmbiguous ? null : policy.PositionLimitPct - evidence.PositionPct,
                evidence.SectorPct - policy.SectorTargetPct);
        }).ToArray();
        if (request.Legacy.GroupBy(row => row.Id).Any(group => group.Count() > 1))
            throw new ArgumentException("Legacy comparison component identifiers must be unique.", nameof(request));
        var legacy = request.Legacy.ToDictionary(row => row.Id, StringComparer.Ordinal);
        var differences = valuation.Components.Select(component =>
        {
            legacy.TryGetValue(component.Id, out var old);
            var classification = component.IssueCode is not null ? "MissingSourceEvidence"
                : old is null ? "LegacyCalculationLimitation"
                : old.UsedCostFallback ? "LegacyCalculationLimitation"
                : component.Kind == "Option" && component.NativeValue != old.Value ? "PolicyDecisionRequired"
                : old.Value != component.NativeValue ? "PossibleAccountingDefect"
                : component.Currency == "USD" ? "ExpectedCurrencyCorrection"
                : component.ValueCad == old.Value ? "Parity" : "PossibleAccountingDefect";
            return new ShadowValuationDifference(component.Id, old?.Value, component.ValueCad, classification);
        }).Concat(request.Legacy.Where(row => valuation.Components.All(component => component.Id != row.Id))
            .Select(row => new ShadowValuationDifference(row.Id, row.Value, null, "PossibleAccountingDefect"))).ToArray();
        return new(request, valuation, exposures, adapted.Issues, adapted.ExternalFlowsByCurrency,
            differences, request.Legacy.Sum(row => row.Value));
    }
}
