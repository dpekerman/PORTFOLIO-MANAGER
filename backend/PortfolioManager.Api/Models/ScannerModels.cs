namespace PortfolioManager.Api.Models;

public enum ScanType { Oversold, Overbought, Neutral }

public enum ChannelDirection { NONE, RISING }
public enum ChannelState
{
    NONE,
    CHANNEL_ACTIVE,
    THIRD_TOUCH_APPROACHING,
    THIRD_TOUCH_TEST,
    REVERSAL_DEVELOPING,
    BOUNCE_CONFIRMED,
    CHANNEL_BROKEN,
}

/// <summary>
/// Signal classification levels (in descending priority):
/// Confirmed    — price-action trigger met on candle close.
/// EodConfirm   — end-of-day confirmation: all 4 EOD rules met near market close.
/// EarlyWarning — RSI threshold crossed but no confirmation yet.
/// Neutral      — no directional signal.
/// </summary>
public enum SignalStatus { Confirmed, EodConfirm, EarlyWarning, Neutral }

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
    public decimal StochasticD { get; set; }
    public bool StochasticsConfirm { get; set; }
    public string RsiDivergence { get; set; } = "None";

    /// <summary>MACD line and signal line values.</summary>
    public decimal MacdValue { get; set; }
    public decimal MacdSignalLine { get; set; }
    /// <summary>"Bullish" | "Bearish" | "Neutral"</summary>
    public string MacdCrossover { get; set; } = "Neutral";

    /// <summary>True when price is outside the Bollinger Band for the scan direction.</summary>
    public bool BollingerBreakout { get; set; }
    /// <summary>"Below Lower" | "Above Upper" | "Inside"</summary>
    public string BollingerPosition { get; set; } = "Inside";
    public decimal BollingerPctB { get; set; }
    public decimal BollingerBandwidth { get; set; }

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

    // ── EOD Confirm Data ─────────────────────────────────────────────────────
    /// <summary>14-day Average True Range (Wilder's smoothing). 0 when insufficient data.</summary>
    public decimal DailyAtr { get; set; }
    /// <summary>9-period EMA of the closing price series.</summary>
    public decimal Ema9Price { get; set; }
    /// <summary>20-period Simple Moving Average of the closing price series.
    /// Used by the Momentum Shift engine (Consolidation rule: price near SMA-20).</summary>
    public decimal Sma20Price { get; set; }
    /// <summary>50-period Simple Moving Average. Used by Trend Setup engine.</summary>
    public decimal Sma50Price { get; set; }
    /// <summary>10-period Exponential Moving Average. Used by Trend Setup engine.</summary>
    public decimal Ema10Price { get; set; }
    /// <summary>20-period Exponential Moving Average. Used by Trend Setup engine.</summary>
    public decimal Ema20Price { get; set; }

    // ── Analyst & Market Data ────────────────────────────────────────────────
    /// <summary>Analyst consensus 1-year target price. 0 when not available.</summary>
    public decimal AnalystTargetPrice { get; set; }
    /// <summary>(TargetPrice − CurrentPrice) / CurrentPrice × 100. 0 when target not available.</summary>
    public decimal AnalystTargetUpside { get; set; }

    // ── Gap Data ─────────────────────────────────────────────────────────────
    /// <summary>Today's opening price. Used for gap detection.</summary>
    public decimal OpenPrice { get; set; }
    /// <summary>Yesterday's closing price. Used for gap detection: GapPct = (Open - PrevClose) / PrevClose × 100.</summary>
    public decimal PreviousClose { get; set; }
    /// <summary>52-week high price.</summary>
    public decimal Week52High { get; set; }
    /// <summary>52-week low price.</summary>
    public decimal Week52Low { get; set; }

    /// <summary>Today's intraday high price (from last completed candle).</summary>
    public decimal DayHigh { get; set; }
    /// <summary>Today's intraday low price (from last completed candle).</summary>
    public decimal DayLow { get; set; }

    // ── Day-over-Day Momentum Tracking (StagedSignals) ───────────────────────
    /// <summary>RSI change from previous trading session (CurrentRsi - PreviousRsi). Null on Day 1.</summary>
    public decimal? RsiDelta1D { get; set; }
    /// <summary>Trend shift state derived from RsiDelta1D. "Waiting" | "🟢 Bull Turn" | "🟡 Stabilizing" | "🔴 Still Falling" | "🟢 Bear Turn" | "🔴 Still Rising"</summary>
    public string TrendShift { get; set; } = "Waiting";
    /// <summary>200-day Simple Moving Average of closing price. 0 when fewer than 200 trading days available.</summary>
    public decimal Sma200 { get; set; }
    /// <summary>Price position relative to SMA200: "Trend-Aligned" | "Counter-Trend" | "" when no SMA200.</summary>
    public string TrendSetup200 { get; set; } = string.Empty;
    /// <summary>Dynamic stop-loss: ExtremeLow - 1.5×ATR (oversold) or ExtremeHigh + 1.5×ATR (overbought). 0 when not yet computed.</summary>
    public decimal DynamicStopLoss { get; set; }
    /// <summary>Whether this result comes from an active staged signal (RSI may have recovered from extreme).</summary>
    public bool IsTracked { get; set; }
    public string ChannelDirection { get; set; } = "NONE";
    public decimal ChannelSlope { get; set; }
    public decimal LowerRailToday { get; set; }
    public decimal UpperRailToday { get; set; }
    public int ChannelQuality { get; set; }
    public int PriorConfirmedLowerTouches { get; set; }
    public DateTime? LastLowerTouchDate { get; set; }
    public decimal DistanceToLowerRailPercent { get; set; }
    public decimal DistanceToLowerRailATR { get; set; }
    public string ChannelState { get; set; } = "NONE";
    public decimal? NearestOpenGapAbove { get; set; }
    public decimal? NearestOpenGapBelow { get; set; }
    public decimal? DistanceToGapAbovePercent { get; set; }
    public decimal? DistanceToGapBelowPercent { get; set; }
    public decimal VolumeProjection { get; set; }
    public decimal PositionSizingShares { get; set; }
    public decimal PositionSizingRiskAmount { get; set; }
    public decimal PositionSizingPositionValue { get; set; }
    public string PositionSizingLimitingReason { get; set; } = string.Empty;

    // ── 2-Stage Engine — Status &amp; Velocity ──────────────────────────────────────
    /// <summary>
    /// Stage workflow status for this setup.
    /// "STAGED"     — Day 1; no prior RSI to calculate delta.
    /// "TRACKING"   — Delta exists but momentum has not meaningfully reversed yet.
    /// "CONFIRMING" — TrendShift is Bull Turn or Bear Turn; engine evaluating price + volume.
    /// Empty string when the result is not from an active staged signal.
    /// </summary>
    public string StageStatus { get; set; } = string.Empty;

    /// <summary>
    /// Velocity of the RSI reversal, derived from |RsiDelta1D|.
    /// "Early" | "Normal" | "Strong" | "Explosive" — empty when not applicable.
    /// Normal is the baseline (no suffix shown in display).
    /// </summary>
    public string TurnStrength { get; set; } = string.Empty;

    /// <summary>
    /// "Elevated" when TurnStrength is Explosive, flagging that a large portion of the
    /// rebound may have already occurred before confirmation.  Empty otherwise.
    /// </summary>
    public string ChaseRisk { get; set; } = string.Empty;

    // ── Fibonacci Retracement V1 ─────────────────────────────────────────────
    /// <summary>Swing low price used for Fibonacci calculation (60-day lookback).</summary>
    public decimal FibSwingLow { get; set; }
    /// <summary>Swing high price used for Fibonacci calculation (must be after swing low).</summary>
    public decimal FibSwingHigh { get; set; }
    /// <summary>Fibonacci 38.2% retracement level: SwingHigh − (range × 0.382). 0 when not calculable.</summary>
    public decimal Fib38_2 { get; set; }
    /// <summary>Fibonacci 50% retracement level: SwingHigh − (range × 0.50).</summary>
    public decimal Fib50 { get; set; }
    /// <summary>Fibonacci 61.8% retracement level (Golden Ratio): SwingHigh − (range × 0.618).</summary>
    public decimal Fib61_8 { get; set; }
    /// <summary>Fibonacci 78.6% retracement level: SwingHigh − (range × 0.786).</summary>
    public decimal Fib78_6 { get; set; }
    /// <summary>Price zone relative to Fibonacci levels: "Shallow Pullback" | "Normal Pullback" | "Value Zone" | "Key Fib Support" | "Deep Pullback" | "Trend Damage". Empty when Fib not calculable.</summary>
    public string FibZone { get; set; } = string.Empty;
    /// <summary>Fibonacci status at current price: "Above 61.8" | "Testing 61.8" | "Reclaimed 61.8" | "Below 61.8" | "Below 78.6". Empty when Fib not calculable.</summary>
    public string FibStatus { get; set; } = string.Empty;
    /// <summary>((CurrentPrice − Fib61.8) / Fib61.8) × 100. Positive = above the level. 0 when Fib not calculable.</summary>
    public decimal DistanceToFib61_8Pct { get; set; }
}

public class TechnicalChannel
{
    public int Id { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string Timeframe { get; set; } = "1D";
    public string Direction { get; set; } = "NONE";
    public decimal Slope { get; set; }
    public decimal LowerRailCurrent { get; set; }
    public decimal UpperRailCurrent { get; set; }
    public int ChannelQuality { get; set; }
    public int LowerTouchCount { get; set; }
    public DateTime? LastLowerTouchDate { get; set; }
    public decimal DistanceToLowerRailPercent { get; set; }
    public decimal DistanceToLowerRailATR { get; set; }
    public string ChannelState { get; set; } = "NONE";
    public decimal? NearestOpenGapAbove { get; set; }
    public decimal? NearestOpenGapBelow { get; set; }
    public decimal? DistanceToGapAbovePercent { get; set; }
    public decimal? DistanceToGapBelowPercent { get; set; }
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

public class ScannerResponse
{
    public IReadOnlyList<RsiScanResult> OversoldChain { get; set; } = [];
    public IReadOnlyList<RsiScanResult> OverboughtChain { get; set; } = [];
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    public bool IsDemo { get; set; }
    public string Market { get; set; } = string.Empty;
}

// ── Ad-Hoc Session Persistence ───────────────────────────────────────────────

/// <summary>Single-row upsert table (Id always 1). Persists the latest RSI scan JSON
/// so the scanner page loads instantly without hitting Yahoo Finance.</summary>
public class RsiScanSnapshot
{
    public int Id { get; set; } = 1;
    public string SnapshotJson { get; set; } = "{}";
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    public int SymbolCount { get; set; }
    public int OversoldCount { get; set; }
    public int OverboughtCount { get; set; }
}

public class AdhocAnalysisSession
{
    public int Id { get; set; }
    public string SessionKey { get; set; } = "default";
    /// <summary>JSON-serialised string[] of ticker symbols.</summary>
    public string Symbols { get; set; } = "[]";
    /// <summary>JSON-serialised RsiScanResult[] — null when the user entered
    /// symbols but has not yet run an analysis.</summary>
    public string? ResultsJson { get; set; }
    public decimal OversoldThreshold { get; set; } = 30m;
    public decimal OverboughtThreshold { get; set; } = 75m;
    public string LogicMode { get; set; } = "Legacy";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>DTO for reading / updating the EOD confirmation window settings at runtime.</summary>
public class EodWindowSettingsDto
{
    /// <summary>Start time in "HH:mm" format (Eastern Time). Default: "15:30"</summary>
    public string EodWindowStart { get; set; } = "15:30";
    /// <summary>End time in "HH:mm" format (Eastern Time). Default: "16:30"</summary>
    public string EodWindowEnd { get; set; } = "16:30";
    /// <summary>Whether the EOD window is enabled.</summary>
    public bool EodWindowEnabled { get; set; } = true;
    /// <summary>RSI threshold below which a stock qualifies for EOD CONFIRM (oversold). Default: 25.</summary>
    public decimal EodOversoldRsiThreshold { get; set; } = 25m;
    /// <summary>RSI threshold above which a stock qualifies for EOD CONFIRM (overbought). Default: 75.</summary>
    public decimal EodOverboughtRsiThreshold { get; set; } = 75m;
}

/// <summary>A single persisted EOD CONFIRM signal, written to disk at end of day.</summary>
public class EodSignalRecord
{
    /// <summary>The ticker symbol (e.g. "TD.TO").</summary>
    public string Symbol { get; set; } = string.Empty;
    /// <summary>Company name if available.</summary>
    public string CompanyName { get; set; } = string.Empty;
    /// <summary>Oversold or Overbought.</summary>
    public string ScanType { get; set; } = string.Empty;
    /// <summary>RSI value at time of signal.</summary>
    public decimal Rsi { get; set; }
    /// <summary>Closing price at time of signal.</summary>
    public decimal Price { get; set; }
    /// <summary>Human-readable trigger explanation generated by the scanner.</summary>
    public string TriggerDetails { get; set; } = string.Empty;
    /// <summary>UTC timestamp when the signal was recorded.</summary>
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// The JSON file structure for persisted EOD CONFIRM signals.
/// Stored in eod-signal-history.json alongside the API binary.
/// </summary>
public class EodSignalHistory
{
    /// <summary>Date string "yyyy-MM-dd" (ET) for which these signals were recorded.</summary>
    public string Date { get; set; } = string.Empty;
    /// <summary>All EOD CONFIRM signals captured during that day's EOD window.</summary>
    public List<EodSignalRecord> Signals { get; set; } = [];
}

/// <summary>
/// Response DTO returned by GET /api/scanner/yesterday-eod.
/// Clients use this to show a "Morning Check" panel.
/// </summary>
public class YesterdayEodResponse
{
    /// <summary>Whether there is any persisted history to show.</summary>
    public bool HasData { get; set; }
    /// <summary>The date the signals were originally recorded ("yyyy-MM-dd").</summary>
    public string SignalDate { get; set; } = string.Empty;
    /// <summary>True when the server's current ET time is before 12:00 PM (morning window).</summary>
    public bool IsMorningWindow { get; set; }
    /// <summary>The persisted EOD CONFIRM signals.</summary>
    public List<EodSignalRecord> Signals { get; set; } = [];
}

/// <summary>
/// Active tracking record for a symbol that has entered an RSI extreme condition.
/// One record per Symbol + ScanType — deactivated when signal is confirmed (IsActiveWatch = 0).
/// </summary>
public class StagedSignal
{
    public int StagedId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    /// <summary>Oversold | Overbought</summary>
    public string ScanType { get; set; } = string.Empty;

    public decimal BasePrice { get; set; }
    public decimal BaseRsi { get; set; }
    public decimal BaseHigh { get; set; }
    public decimal BaseLow { get; set; }

    public decimal? PreviousPrice { get; set; }
    public decimal? PreviousRsi { get; set; }

    public decimal? CurrentPrice { get; set; }
    public decimal? CurrentRsi { get; set; }

    public decimal? RsiDelta1D { get; set; }

    public decimal? ExtremeLow { get; set; }
    public decimal? ExtremeHigh { get; set; }

    public DateOnly StagedDate { get; set; }
    public DateOnly? LastEvaluatedDate { get; set; }

    public bool IsActiveWatch { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A persisted daily EOD signal record stored in the database.
/// Enables querying full signal history across multiple days with lifecycle tracking.
/// </summary>
public class DailySignal
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    /// <summary>Oversold | Overbought</summary>
    public string ScanType { get; set; } = string.Empty;
    /// <summary>EodConfirm | Confirmed | EarlyWarning</summary>
    public string SignalType { get; set; } = string.Empty;
    public decimal Rsi { get; set; }
    public decimal Price { get; set; }
    public string TriggerDetails { get; set; } = string.Empty;
    /// <summary>Date string "yyyy-MM-dd" (ET) when this signal was recorded.</summary>
    public string SignalDate { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Logic mode: Legacy | Enhanced</summary>
    public string RuleVersion { get; set; } = string.Empty;
    /// <summary>Signal lifecycle state: Active | FollowThrough | Invalidated | Expired | Reversed</summary>
    public string SignalState { get; set; } = "Active";
    /// <summary>State before the last transition — used to surface "Changed Today" on the dashboard.</summary>
    public string? PreviousSignalState { get; set; }
    public string Sector { get; set; } = string.Empty;
    public string ReversalProbability { get; set; } = string.Empty;
    public string VolumeSignal { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // ── Confirmation snapshot (populated when signal is confirmed) ────────────
    /// <summary>Trend shift at confirmation: "Bull Turn" | "Bear Turn"</summary>
    public string? TrendShift { get; set; }
    /// <summary>RSI day-over-day delta at confirmation.</summary>
    public decimal? RsiDelta1D { get; set; }
    /// <summary>Market price at exact moment of confirmation.</summary>
    public decimal? EntryPrice { get; set; }
    /// <summary>Final stop-loss: ExtremeLow - 1.5×ATR or ExtremeHigh + 1.5×ATR.</summary>
    public decimal? StopLossPrice { get; set; }
    /// <summary>ABS(EntryPrice - StopLossPrice).</summary>
    public decimal? RiskPerShare { get; set; }
    public decimal? PositionSizingShares { get; set; }
    public decimal? PositionSizingRiskAmount { get; set; }
    public decimal? PositionSizingPositionValue { get; set; }
    public string? PositionSizingLimitingReason { get; set; }
    /// <summary>200-day SMA at confirmation.</summary>
    public decimal? Sma200 { get; set; }
    /// <summary>EMA9 price at the moment of promotion.</summary>
    public decimal? Ema9AtEntry { get; set; }
    /// <summary>Whether price had crossed EMA9 in the reversal direction at promotion time.</summary>
    public bool? Ema9ConfirmedAtEntry { get; set; }

    // ── Fibonacci snapshot (informational, not a promotion gate) ─────────────
    /// <summary>Fib 61.8% level at the moment the signal was generated. Null when not calculable.</summary>
    public decimal? Fib61_8AtSignal { get; set; }
    /// <summary>Fibonacci zone at the moment the signal was generated. Null when not calculable.</summary>
    public string? FibZoneAtSignal { get; set; }
    /// <summary>Fibonacci status at the moment the signal was generated. Null when not calculable.</summary>
    public string? FibStatusAtSignal { get; set; }
}

/// <summary>Request DTO for updating a DailySignal's lifecycle state.</summary>
public class UpdateSignalStateRequest
{
    public string SignalState { get; set; } = string.Empty;
}

/// <summary>Request DTO for updating a DailySignal's notes.</summary>
public class UpdateSignalNotesRequest
{
    public string? Notes { get; set; }
}

/// <summary>Query parameters for the EOD Signals Dashboard endpoint.</summary>
public class EodSignalQueryParams
{
    public string? Ticker { get; set; }
    public string? ScanType { get; set; }
    public string? SignalType { get; set; }
    public string? SignalState { get; set; }
    public string? RuleVersion { get; set; }
    public string? DateFrom { get; set; }
    public string? DateTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

/// <summary>Paginated response wrapper for DailySignal queries.</summary>
public class DailySignalPagedResponse
{
    public List<DailySignal> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class SaveAdhocSessionRequest
{
    public List<string> Symbols { get; set; } = [];
    public List<RsiScanResult>? Results { get; set; }
    public decimal OversoldThreshold { get; set; } = 30m;
    public decimal OverboughtThreshold { get; set; } = 75m;
    public string LogicMode { get; set; } = "Legacy";
}

public class LoadAdhocSessionResponse
{
    public List<string> Symbols { get; set; } = [];
    public List<RsiScanResult>? Results { get; set; }
    public decimal OversoldThreshold { get; set; } = 30m;
    public decimal OverboughtThreshold { get; set; } = 75m;
    public string LogicMode { get; set; } = "Legacy";
    public DateTime? UpdatedAt { get; set; }
}

public record MarketIndexDto(
    string Symbol,
    string Name,
    decimal Price,
    decimal Change,
    decimal ChangePercent);

public record MarketIndicesResponse(
    List<MarketIndexDto> Indices,
    DateTime FetchedAt);
