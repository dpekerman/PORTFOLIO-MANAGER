namespace PortfolioManager.Api.Services.Opportunities;

/// <summary>
/// Pure Portfolio Fit (docs/pm-029-trading-opportunities.md §6). Inputs are the authoritative FinalActionService portfolio evidence
/// (whole-symbol exposure vs GrandTotal, role limit, sector weight vs AllocationSectorTargets); the same comparisons the
/// PortfolioEvidenceGate uses to block an ADD (size ≥ limit; sector &gt; target + 2 pp). Fit never feeds a score or FINAL ACTION.
/// External deposits/withdrawals change these inputs (weights, headroom) and therefore Fit — by design (PM-035, C1).
/// </summary>
public static class PortfolioFitCalculator
{
    public static FitResult Evaluate(FitInput input)
    {
        if (!input.AllocationReliable)
            return new FitResult(FitStatus.Unavailable, [FitReasons.AllocationUnverifiable], null, input.PositionLimitPct, null, null, input.SectorTargetPct, null);

        double? weight = input.Owned ? input.PositionSizePct : 0d;
        if (weight is null)
            return new FitResult(FitStatus.Unavailable, [FitReasons.ExposureUnavailable], null, input.PositionLimitPct, null,
                input.SectorPct, input.SectorTargetPct, null);

        var reasons = new List<string>();
        var status = FitStatus.Eligible;
        void Raise(FitStatus s, string code)
        {
            reasons.Add(code);
            if (s > status) status = s;
        }

        double? positionHeadroom = null;
        if (input.RolePolicyAmbiguous) Raise(FitStatus.Review, FitReasons.RolePolicyAmbiguous);
        if (input.PositionLimitPct is { } limit)
        {
            positionHeadroom = limit - weight.Value;
            if (weight.Value >= limit) Raise(FitStatus.NoAdd, FitReasons.PositionAtLimit);
            else if (positionHeadroom < OpportunityScoringConfig.PositionNearLimitPp) Raise(FitStatus.Review, FitReasons.PositionNearLimit);
        }
        else if (!input.RolePolicyAmbiguous) Raise(FitStatus.Review, FitReasons.NoPositionLimit);

        double? sectorHeadroom = null;
        if (!input.SectorKnown) Raise(FitStatus.Review, FitReasons.SectorUnknown);
        else if (input.SectorTargetPct is not { } target) reasons.Add(FitReasons.SectorNoTarget);
        else if (input.SectorPct is { } sectorPct)
        {
            sectorHeadroom = target + OpportunityScoringConfig.SectorTolerancePp - sectorPct;
            if (sectorPct - target > OpportunityScoringConfig.SectorTolerancePp) Raise(FitStatus.NoAdd, FitReasons.SectorOverTarget);
            else if (sectorPct > target) Raise(FitStatus.Review, FitReasons.SectorAboveTarget);
        }

        return new FitResult(status, reasons, weight, input.PositionLimitPct, positionHeadroom, input.SectorPct, input.SectorTargetPct, sectorHeadroom);
    }
}
