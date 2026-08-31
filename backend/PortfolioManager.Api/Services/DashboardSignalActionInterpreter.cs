using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public static class DashboardSignalActionInterpreter
{
    public static string Resolve(RsiScanResult signal, bool isInPortfolio, bool isInWatchlist)
    {
        if (isInPortfolio)
            return ResolvePortfolioAction(signal);

        if (isInWatchlist)
            return ResolveWatchlistAction(signal);

        return ResolveScannerAction(signal);
    }

    private static string ResolvePortfolioAction(RsiScanResult signal)
    {
        if (signal.ScanType == ScanType.Oversold)
            return signal.Status == SignalStatus.Confirmed || signal.Status == SignalStatus.EodConfirm ? "ADD CANDIDATE"
                 : signal.TrendShift.Contains("Bull Turn", StringComparison.OrdinalIgnoreCase) ? "ADD WATCH"
                 : signal.TrendShift.Contains("Stabilizing", StringComparison.OrdinalIgnoreCase) ? "HOLD"
                 : "REVIEW";

        return signal.Status == SignalStatus.Confirmed || signal.Status == SignalStatus.EodConfirm ? "TRIM WATCH"
             : signal.TrendShift.Contains("Bear Turn", StringComparison.OrdinalIgnoreCase) ? "EXIT REVIEW"
             : "HOLD/EXTENDED";
    }

    private static string ResolveWatchlistAction(RsiScanResult signal)
    {
        if (signal.ScanType == ScanType.Oversold)
            return signal.Status == SignalStatus.Confirmed || signal.Status == SignalStatus.EodConfirm ? "ENTRY CANDIDATE"
                 : signal.TrendShift.Contains("Bull Turn", StringComparison.OrdinalIgnoreCase) ? "BUY WATCH"
                 : signal.TrendShift.Contains("Stabilizing", StringComparison.OrdinalIgnoreCase) ? "REVERSAL WATCH"
                 : "WAIT FOR REVERSAL";

        return signal.Status == SignalStatus.Confirmed || signal.Status == SignalStatus.EodConfirm ? "AVOID"
             : signal.TrendShift.Contains("Bear Turn", StringComparison.OrdinalIgnoreCase) ? "WAIT FOR PULLBACK"
             : "WAIT";
    }

    private static string ResolveScannerAction(RsiScanResult signal)
    {
        if (signal.ScanType == ScanType.Oversold)
            return signal.Status == SignalStatus.Confirmed || signal.Status == SignalStatus.EodConfirm ? "BUY WATCH"
                 : signal.TrendShift.Contains("Bull Turn", StringComparison.OrdinalIgnoreCase) ? "REVERSAL WATCH"
                 : "WAIT FOR REVERSAL";

        return signal.Status == SignalStatus.Confirmed || signal.Status == SignalStatus.EodConfirm ? "TECHNICAL CAUTION"
             : signal.TrendShift.Contains("Bear Turn", StringComparison.OrdinalIgnoreCase) ? "TECHNICAL CAUTION"
             : "AVOID";
    }
}