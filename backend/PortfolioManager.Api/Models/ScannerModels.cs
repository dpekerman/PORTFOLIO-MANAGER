namespace PortfolioManager.Api.Models;

public enum ScanType { Oversold, Overbought, Neutral }
public enum SignalStatus { Confirmed, EarlyWarning, Neutral }

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

    /// <summary>9-period EMA of the RSI(14) series — the "RSI Signal line".
    /// Null when there is insufficient data to compute (requires at least 23 candles:
    /// 14 for first RSI + 9 for EMA seed).</summary>
    public decimal? RsiSignal { get; set; }
    /// <summary>True when RsiSignal was successfully calculated; false means the column
    /// should display an "unable to calculate" indicator.</summary>
    public bool RsiSignalAvailable { get; set; }

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

    // ── Enhanced Mode (MACD Histogram Momentum + State Machine) ─────────────
    /// <summary>MACD histogram value (macdLine − signalLine) at latest bar.</summary>
    public decimal MacdHistogram { get; set; }
    /// <summary>Change in histogram from previous bar (Δhist = hist[t] − hist[t−1]).
    /// Negative bars that are shrinking toward zero → slope is positive → momentum shift.</summary>
    public decimal MacdHistDelta { get; set; }
    /// <summary>"Rising" | "Falling" | "Neutral" — internal momentum shift direction
    /// detected before the MACD lines actually cross.</summary>
    public string MacdHistSlope { get; set; } = "Neutral";
    /// <summary>"Legacy" (original logic) or "Enhanced" (histogram momentum + strict state machine).</summary>
    public string LogicMode { get; set; } = "Legacy";

    // ── Analyst & Market Data ────────────────────────────────────────────────
    /// <summary>Analyst consensus 1-year target price. 0 when not available.</summary>
    public decimal AnalystTargetPrice { get; set; }
    /// <summary>(TargetPrice − CurrentPrice) / CurrentPrice × 100. 0 when target not available.</summary>
    public decimal AnalystTargetUpside { get; set; }
    /// <summary>52-week high price.</summary>
    public decimal Week52High { get; set; }
    /// <summary>52-week low price.</summary>
    public decimal Week52Low { get; set; }
}

public class ScannerResponse
{
    public IReadOnlyList<RsiScanResult> OversoldChain { get; set; } = [];
    public IReadOnlyList<RsiScanResult> OverboughtChain { get; set; } = [];
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    public bool IsDemo { get; set; }
    public string Market { get; set; } = string.Empty;
}
