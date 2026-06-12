namespace PortfolioManager.Api.Models;

public enum ScanType { Oversold, Overbought }
public enum SignalStatus { Confirmed, EarlyWarning }

public class RsiScanResult
{
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal Rsi { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public ScanType ScanType { get; set; }
    public SignalStatus Status { get; set; }
    public string TriggerDetails { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public decimal Volume { get; set; }
    public decimal VolumeRatio { get; set; }  // vs 20-day avg
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    public bool IsDemo { get; set; }

    // ── 5 Technical Indicators ──────────────────────────────────────────────
    /// <summary>Stochastic Fast %K (0-100). Confirms extreme reading when
    /// below 20 (oversold) or above 80 (overbought).</summary>
    public decimal StochasticK { get; set; }
    public bool StochasticsConfirm { get; set; }

    /// <summary>MACD line and signal line values.</summary>
    public decimal MacdValue { get; set; }
    public decimal MacdSignalLine { get; set; }
    /// <summary>"Bullish" | "Bearish" | "Neutral"</summary>
    public string MacdCrossover { get; set; } = "Neutral";

    /// <summary>True when price is outside the Bollinger Band for the scan direction.</summary>
    public bool BollingerBreakout { get; set; }
    /// <summary>"Below Lower" | "Above Upper" | "Inside"</summary>
    public string BollingerPosition { get; set; } = "Inside";

    /// <summary>"Validated" (high-vol confirms move) | "Low-Volume Trap" | "Neutral"</summary>
    public string VolumeSignal { get; set; } = "Neutral";

    /// <summary>% deviation of current price from 50-day simple moving average.</summary>
    public decimal Dma50Deviation { get; set; }
    /// <summary>% deviation of current price from 200-day simple moving average.
    /// Only valid when Has200Dma is true.</summary>
    public decimal Dma200Deviation { get; set; }
    public bool Has200Dma { get; set; }

    /// <summary>Aggregate reversal probability: "Low" | "Medium" | "High"</summary>
    public string ReversalProbability { get; set; } = "Low";
}

public class ScannerResponse
{
    public IReadOnlyList<RsiScanResult> OversoldChain { get; set; } = [];
    public IReadOnlyList<RsiScanResult> OverboughtChain { get; set; } = [];
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    public bool IsDemo { get; set; }
    public string Market { get; set; } = string.Empty;
}
