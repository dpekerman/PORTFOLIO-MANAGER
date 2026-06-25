/**
 * Shared Decision Engine — v2
 * ───────────────────────────
 * Implements the full rule specification:
 *  - BottomHalfClose / TopHalfClose gating on Active Buy/Sell Trigger
 *  - Direction-aware volume (high vol alone cannot force buy or sell)
 *  - Waterfall safety: blocks ABT when RSI still below RSI9EMA and vol < 1.3x
 *  - RSI overbought (> 65) → "Warning — Overbought Run" unless bearish candle confirms
 *  - Role-specific final actions per Watchlist / Portfolio
 *  - Debug output per ticker via console.debug
 */

import { Injectable } from '@angular/core';
import { RsiScanResult } from '../models/portfolio.models';

export type TrendSetup =
  | 'Waterfall / Falling Knife'
  | 'Oversold Reversal Watch'
  | 'Constructive Extended'
  | 'Quality Trend Entry'
  | 'Confirmed Constructive'
  | 'Early Reversal'
  | 'Cooling'
  | 'Technical Caution'
  | 'Neutral / No Setup';

export type MomentumShift =
  | 'Active Buy Trigger'
  | 'Active Sell Trigger'
  | 'Warning'
  | 'Warning — Overbought Run'
  | 'Bullish Shift'
  | 'Bearish Shift'
  | 'Breakdown'
  | 'Consolidation / Dip-Buy'
  | 'Uptrend'
  | 'Neutral';

export type BaseAction =
  | 'Confirmed Buy Signal'
  | 'Confirmed Sell Signal'
  | 'Early Buy Watch'
  | 'Early Sell Watch'
  | 'Watch / Do Not Chase'
  | 'Avoid / Wait'
  | 'Reduce / Review'
  | 'Buy / Accumulate'
  | 'Hold Longs'
  | 'Stand By';

export interface DecisionDebug {
  page: string;
  role: string | null;
  rsi14: number;
  rsi9Ema: number | null;
  close: number;
  dayHigh: number;
  dayLow: number;
  normalizedClose: number;
  topHalfClose: boolean;
  bottomHalfClose: boolean;
  ema9: number;
  ema10: number;
  ema20: number;
  sma20: number;
  sma50: number;
  volume: number;
  highVolume: boolean;
  macdHistogram: number;
  prevMacdHistogram: number;
  macdImproving: boolean;
  macdWeakening: boolean;
  oversoldContext: boolean;
  overboughtContext: boolean;
  trendSetupMatchedRule: string;
  momentumShiftMatchedRule: string;
  baseActionMatchedRule: string;
  finalActionMatchedRule: string;
}

export interface DecisionResult {
  trendSetup: TrendSetup;
  trendSetupReason: string;
  momentumShift: MomentumShift;
  momentumShiftReason: string;
  baseAction: BaseAction;
  baseActionReason: string;
}

export interface PageDecision extends DecisionResult {
  finalAction: string;
  hoverDescription: string;
  finalActionClass: string;
  trendSetupClass: string;
  momentumShiftClass: string;
}

const ROLE_VALUES = ['Core', 'Strategic', 'Swing', 'Speculative', 'Options'] as const;
export type InvestmentRole = (typeof ROLE_VALUES)[number];

/**
 * Optional portfolio-item context for account-specific rule overrides.
 * Passed to `translateForPortfolio` when item metadata is available.
 */
export interface PortfolioItemContext {
  /** Full account label, e.g. "Corp_TD", "TFSA_RBC". Rule matches if contains "TFSA". */
  accountType?: string | null;
  /** Unrealized gain as a percentage, e.g. 22 = +22%. */
  unrealizedGainPct?: number | null;
  /** Calendar days since the position was opened. */
  holdingDays?: number | null;
}

@Injectable({ providedIn: 'root' })
export class DecisionEngineService {
  calculateDecision(r: RsiScanResult, role?: string | null, page = 'Unknown'): DecisionResult {
    const ctx = this.buildContext(r);
    const trendSetup = this.calcTrendSetup(r, role ?? null, ctx);
    const trendSetupReason = this.trendSetupReason(trendSetup, r);
    const momentumShift = this.calcMomentumShift(r, ctx, trendSetup);
    const momentumShiftReason = this.momentumShiftReason(momentumShift, r);
    const baseAction = this.calcBaseAction(momentumShift, trendSetup);
    const baseActionReason = baseAction;

    const debug: DecisionDebug = {
      page,
      role: role ?? null,
      rsi14: r.rsi,
      rsi9Ema: r.rsiSignalAvailable ? (r.rsiSignal ?? null) : null,
      close: r.currentPrice,
      dayHigh: ctx.dayHigh,
      dayLow: ctx.dayLow,
      normalizedClose: ctx.normalizedClose,
      topHalfClose: ctx.topHalfClose,
      bottomHalfClose: ctx.bottomHalfClose,
      ema9: r.ema9Price ?? 0,
      ema10: r.ema10Price ?? 0,
      ema20: r.ema20Price ?? 0,
      sma20: r.sma20Price ?? 0,
      sma50: r.sma50Price ?? 0,
      volume: r.volume ?? 0,
      highVolume: ctx.highVolume,
      macdHistogram: r.macdHistogram,
      prevMacdHistogram: r.macdHistogram - r.macdHistDelta,
      macdImproving: ctx.macdImproving,
      macdWeakening: ctx.macdWeakening,
      oversoldContext: ctx.oversoldContext,
      overboughtContext: ctx.overboughtContext,
      trendSetupMatchedRule: trendSetup,
      momentumShiftMatchedRule: momentumShift,
      baseActionMatchedRule: baseAction,
      finalActionMatchedRule: '(computed per page)',
    };
    console.debug(`[DecisionEngine] ${r.symbol}`, debug);

    return {
      trendSetup,
      trendSetupReason,
      momentumShift,
      momentumShiftReason,
      baseAction,
      baseActionReason,
    };
  }

  translateForRsiScanner(r: RsiScanResult): PageDecision {
    const dec = this.calculateDecision(r, null, 'RSI Scanner');
    return {
      ...dec,
      finalAction: dec.baseAction,
      hoverDescription: dec.momentumShiftReason,
      finalActionClass: this.baseActionClass(dec.baseAction),
      trendSetupClass: this.trendSetupClass(dec.trendSetup),
      momentumShiftClass: this.momentumShiftClass(dec.momentumShift),
    };
  }

  translateForWatchlist(r: RsiScanResult, role: string | null): PageDecision {
    const dec = this.calculateDecision(r, role, 'Watchlist');
    const effectiveRole = (role ?? 'Strategic') as InvestmentRole;
    const rawAction = this.watchlistFinalAction(dec, effectiveRole);
    const finalAction = this.accumulateStarterGuard(
      rawAction,
      dec.trendSetup,
      dec.momentumShift,
      r.changePercent ?? 0,
    );
    return {
      ...dec,
      finalAction,
      hoverDescription: this.watchlistHover(dec, effectiveRole),
      finalActionClass: this.finalActionClass(finalAction),
      trendSetupClass: this.trendSetupClass(dec.trendSetup),
      momentumShiftClass: this.momentumShiftClass(dec.momentumShift),
    };
  }

  translateForPortfolio(
    r: RsiScanResult,
    role: string | null,
    isOwned: boolean,
    context?: PortfolioItemContext,
  ): PageDecision {
    const dec = this.calculateDecision(r, role, 'Portfolio');
    const effectiveRole = (role ?? 'Strategic') as InvestmentRole;
    let rawAction = this.portfolioFinalAction(dec, effectiveRole, isOwned);

    // ── TFSA Profit-Taking Override ──────────────────────────────────────────
    // Fires before role-based action when ALL conditions are met:
    //   account contains "TFSA" AND unrealized gain ≥ 20%
    //   AND holding period ≤ 6 months AND RSI14 ≥ 65
    //   AND current price ≥ analyst target
    if (this.tfsaProfitTakeTriggered(r, context)) {
      rawAction = 'Take Partial Profit / No Chase';
    }

    const finalAction = this.accumulateStarterGuard(
      rawAction,
      dec.trendSetup,
      dec.momentumShift,
      r.changePercent ?? 0,
    );
    return {
      ...dec,
      finalAction,
      hoverDescription: this.portfolioHover(dec, effectiveRole, isOwned, context),
      finalActionClass: this.finalActionClass(finalAction),
      trendSetupClass: this.trendSetupClass(dec.trendSetup),
      momentumShiftClass: this.momentumShiftClass(dec.momentumShift),
    };
  }

  /**
   * TFSA profit-taking rule.
   * Returns true when the position is in a TFSA account, has appreciated ≥20%,
   * was held ≤ 6 months, RSI14 ≥ 65, and price has reached/exceeded analyst target.
   */
  private tfsaProfitTakeTriggered(
    r: RsiScanResult,
    context: PortfolioItemContext | undefined,
  ): boolean {
    if (!context) return false;
    const account = (context.accountType ?? '').toUpperCase();
    if (!account.includes('TFSA')) return false;
    if ((context.unrealizedGainPct ?? 0) < 20) return false;
    if ((context.holdingDays ?? Infinity) > 183) return false; // 6 calendar months ≈ 183 days
    if (r.rsi < 65) return false;
    if (!r.analystTargetPrice || r.analystTargetPrice <= 0) return false;
    if (r.currentPrice < r.analystTargetPrice) return false;
    return true;
  }

  private buildContext(r: RsiScanResult) {
    const close = r.currentPrice;
    const rsi = r.rsi;
    const sig = r.rsiSignal ?? rsi;

    let dayHigh = r.dayHigh > 0 ? r.dayHigh : 0;
    let dayLow = r.dayLow > 0 ? r.dayLow : 0;

    if (dayHigh === 0 || dayLow === 0) {
      const chgPct = r.changePercent ?? 0;
      if (chgPct > 1.0) {
        dayHigh = close;
        dayLow = close * (1 - (Math.abs(chgPct) / 100) * 1.5);
      } else if (chgPct < -1.0) {
        dayLow = close;
        dayHigh = close * (1 + (Math.abs(chgPct) / 100) * 1.5);
      } else {
        dayHigh = close;
        dayLow = close;
      }
    }

    const range = dayHigh - dayLow;
    let normalizedClose: number, topHalfClose: boolean, bottomHalfClose: boolean;
    if (range > 0) {
      normalizedClose = (close - dayLow) / range;
      topHalfClose = normalizedClose > 0.5;
      bottomHalfClose = normalizedClose <= 0.5;
    } else {
      normalizedClose = 0.5;
      topHalfClose = false;
      bottomHalfClose = false;
    }

    const vol = r.volumeRatio ?? 1;
    const highVolume = vol >= 1.3;
    const macdImproving = r.macdHistDelta > 0;
    const macdWeakening = r.macdHistDelta < 0;
    const oversoldContext = rsi < 35 || (r.status === 'Confirmed' && r.scanType === 'Oversold');
    const overboughtContext = rsi > 65 || (r.status === 'Confirmed' && r.scanType === 'Overbought');

    return {
      dayHigh,
      dayLow,
      range,
      normalizedClose,
      topHalfClose,
      bottomHalfClose,
      vol,
      highVolume,
      macdImproving,
      macdWeakening,
      oversoldContext,
      overboughtContext,
      sig,
    };
  }

  private calcTrendSetup(
    r: RsiScanResult,
    role: string | null,
    ctx: ReturnType<DecisionEngineService['buildContext']>,
  ): TrendSetup {
    const rsi = r.rsi,
      sig = ctx.sig,
      close = r.currentPrice;
    const sma50 = r.sma50Price ?? 0,
      ema10 = r.ema10Price ?? 0,
      ema20 = r.ema20Price ?? 0;
    const rsiAvail = r.rsiSignalAvailable;

    if (rsi < 35 && rsiAvail && rsi < sig && sma50 > 0 && close < sma50)
      return 'Waterfall / Falling Knife';
    if (rsi < 35 && rsiAvail && rsi >= sig) return 'Oversold Reversal Watch';
    if (ema10 > 0 && ema20 > 0 && sma50 > 0 && ema10 > ema20 && close > sma50 && rsi > 70)
      return 'Constructive Extended';
    const isQualityRole = role === 'Core' || role === 'Strategic';
    if (
      isQualityRole &&
      ema10 > 0 &&
      ema20 > 0 &&
      sma50 > 0 &&
      ema10 > ema20 &&
      close > sma50 &&
      rsi >= 50 &&
      rsi <= 65
    )
      return 'Quality Trend Entry';
    if (ema10 > 0 && ema20 > 0 && sma50 > 0 && ema10 > ema20 && close > sma50 && rsi <= 70)
      return 'Confirmed Constructive';
    if (
      ema10 > 0 &&
      ema20 > 0 &&
      sma50 > 0 &&
      ema10 > ema20 &&
      close < sma50 &&
      rsi >= 30 &&
      rsi <= 55
    )
      return 'Early Reversal';
    if (ema10 > 0 && ema20 > 0 && sma50 > 0 && ema10 < ema20 && close > sma50) return 'Cooling';
    if (ema10 > 0 && ema20 > 0 && sma50 > 0 && ema10 < ema20 && close < sma50 && rsi >= 35)
      return 'Technical Caution';
    return 'Neutral / No Setup';
  }

  private trendSetupReason(ts: TrendSetup, r: RsiScanResult): string {
    switch (ts) {
      case 'Waterfall / Falling Knife':
        return `Deeply oversold (RSI ${r.rsi.toFixed(1)}) with RSI still below signal line and price below SMA50. Selling active — avoid.`;
      case 'Oversold Reversal Watch':
        return `RSI ${r.rsi.toFixed(1)} crossed above signal from oversold. Potential reversal — watch for candle confirmation.`;
      case 'Constructive Extended':
        return `Trend intact (EMA10>EMA20, price>SMA50) but RSI ${r.rsi.toFixed(1)}>70. Extended — do not chase.`;
      case 'Quality Trend Entry':
        return `Core/Strategic: healthy uptrend, RSI ${r.rsi.toFixed(1)} in 50-65 constructive zone. Suitable for staged entry.`;
      case 'Confirmed Constructive':
        return `EMA10>EMA20, price above SMA50, RSI ${r.rsi.toFixed(1)} — trend intact and not extended.`;
      case 'Early Reversal':
        return `Short-term momentum turning up (EMA10>EMA20) but price still below SMA50. Watch for reclaim.`;
      case 'Cooling':
        return `Short-term momentum fading (EMA10<EMA20), price still above SMA50. Pause on new buying.`;
      case 'Technical Caution':
        return `EMA10<EMA20 and price<SMA50. RSI above 35 — no panic, but technically weak.`;
      default:
        return 'Insufficient indicator data for setup classification.';
    }
  }

  private calcMomentumShift(
    r: RsiScanResult,
    ctx: ReturnType<DecisionEngineService['buildContext']>,
    ts: TrendSetup,
  ): MomentumShift {
    const rsi = r.rsi,
      sig = ctx.sig,
      close = r.currentPrice;
    const ema9 = r.ema9Price ?? 0,
      sma20 = r.sma20Price ?? 0;
    const {
      topHalfClose,
      bottomHalfClose,
      highVolume,
      macdImproving,
      macdWeakening,
      oversoldContext,
      overboughtContext,
      vol,
    } = ctx;
    const rsiAvail = r.rsiSignalAvailable;

    // 1. Active Buy Trigger: OversoldContext AND TopHalfClose AND (HighVol OR MACDImproving)
    //    Safety: Waterfall + RSI below signal + low volume → block
    const waterfallBlock =
      ts === 'Waterfall / Falling Knife' && rsiAvail && rsi < sig && !highVolume;
    if (oversoldContext && topHalfClose && (highVolume || macdImproving) && !waterfallBlock)
      return 'Active Buy Trigger';

    // 2. Active Sell Trigger: OverboughtContext AND BottomHalfClose AND (HighVol OR MACDWeakening)
    if (overboughtContext && bottomHalfClose && (highVolume || macdWeakening))
      return 'Active Sell Trigger';

    // 3. Warning (oversold, knife still falling)
    if (rsi < 35 && rsiAvail && rsi < sig) return 'Warning';

    // 4. Bullish Shift
    if (rsi < 35 && rsiAvail && rsi >= sig) return 'Bullish Shift';

    // 5. Bearish Shift
    if (rsi > 65 && rsiAvail && rsi <= sig) return 'Bearish Shift';

    // Overbought but no bearish candle confirmation → Warning — Overbought Run
    if (overboughtContext) return 'Warning — Overbought Run';

    // 6. Breakdown
    if (ema9 > 0 && close < ema9 && rsi < 40) return 'Breakdown';

    // 7. Consolidation / Dip-Buy
    if (
      sma20 > 0 &&
      Math.abs(close - sma20) / sma20 <= 0.01 &&
      rsi >= 40 &&
      rsi <= 50 &&
      vol >= 1.2
    )
      return 'Consolidation / Dip-Buy';

    // 8. Uptrend
    if ((ema9 > 0 && close > ema9 && rsi >= 50 && rsi <= 65) || (rsi >= 55 && rsi <= 65))
      return 'Uptrend';

    return 'Neutral';
  }

  private momentumShiftReason(ms: MomentumShift, r: RsiScanResult): string {
    switch (ms) {
      case 'Active Buy Trigger':
        return 'Buyers confirmed: candle closed in upper half of range from oversold with volume or MACD improvement. High-probability entry.';
      case 'Active Sell Trigger':
        return 'Distribution confirmed: candle closed in lower half of range from overbought with volume or MACD weakening. Bearish reversal in progress.';
      case 'Warning':
        return `RSI ${r.rsi.toFixed(1)} deeply oversold but still below signal line — waterfall selling may continue. Avoid entry.`;
      case 'Warning — Overbought Run':
        return `RSI ${r.rsi.toFixed(1)} elevated/overbought but candle has NOT confirmed bearish reversal (no bottom-half close + confirmation). Extended — do not chase.`;
      case 'Bullish Shift':
        return `RSI ${r.rsi.toFixed(1)} crossed above signal from oversold. Selling pressure easing — watch for candle confirmation.`;
      case 'Bearish Shift':
        return `RSI ${r.rsi.toFixed(1)} crossed below signal from overbought. Buying momentum fading — watch for follow-through.`;
      case 'Breakdown':
        return 'Price broke below EMA9 with RSI fading below 40. Short-term structure deteriorated. Defensive risk.';
      case 'Consolidation / Dip-Buy':
        return 'Price near 20-day SMA, RSI 40-50, volume elevated. Institutional dip-buy zone confirmed.';
      case 'Uptrend':
        return 'Short-term trend healthy — price above EMA9, RSI constructive. Hold existing. No new chase.';
      default:
        return 'RSI neutral zone. No directional confirmation. Stand by.';
    }
  }

  private calcBaseAction(ms: MomentumShift, ts: TrendSetup): BaseAction {
    switch (ms) {
      case 'Active Buy Trigger':
        return 'Confirmed Buy Signal';
      case 'Active Sell Trigger':
        return 'Confirmed Sell Signal';
      case 'Bullish Shift':
        return 'Early Buy Watch';
      case 'Bearish Shift':
        return 'Early Sell Watch';
      case 'Warning':
        return 'Avoid / Wait';
      case 'Warning — Overbought Run':
        return 'Watch / Do Not Chase';
      case 'Breakdown':
        return 'Reduce / Review';
      case 'Consolidation / Dip-Buy':
        return 'Buy / Accumulate';
      case 'Uptrend':
        return 'Hold Longs';
      default:
        return 'Stand By';
    }
  }

  private accumulateStarterGuard(
    action: string,
    ts: TrendSetup,
    ms: MomentumShift,
    changePercent: number,
  ): string {
    if (action !== 'Accumulate Starter') return action;
    const overbought =
      ts === 'Constructive Extended' || ms === 'Warning \u2014 Overbought Run' || changePercent > 5;
    return overbought ? 'Watch / No Chase' : action;
  }

  private watchlistFinalAction(dec: DecisionResult, role: InvestmentRole): string {
    const { trendSetup: ts, momentumShift: ms, baseAction: ba } = dec;
    if (ba === 'Hold Longs') return this.translateHoldLongsForWatchlist(role, ts);
    if (ba === 'Watch / Do Not Chase') return this.translateWatchDoNotChase(role, ms);
    switch (role) {
      case 'Core':
        return this.coreWatchlistAction(ts, ms, ba);
      case 'Strategic':
        return this.strategicWatchlistAction(ts, ms, ba);
      case 'Swing':
        return this.swingWatchlistAction(ts, ms, ba);
      case 'Options':
        return this.optionsWatchlistAction(ts, ms, ba);
      case 'Speculative':
        return this.speculativeWatchlistAction(ts, ms, ba);
      default:
        return ba;
    }
  }

  private translateHoldLongsForWatchlist(role: InvestmentRole, ts: TrendSetup): string {
    switch (role) {
      case 'Core':
      case 'Strategic':
        return ts === 'Quality Trend Entry' ? 'Accumulate Starter' : 'Watch / Starter OK';
      case 'Swing':
        return 'Watch / No Chase';
      case 'Options':
        return 'Call Watch / Entry OK';
      case 'Speculative':
        return 'Watch / Small Entry OK';
      default:
        return 'Watch / No Chase';
    }
  }

  private translateWatchDoNotChase(role: InvestmentRole, _ms: MomentumShift): string {
    switch (role) {
      case 'Core':
      case 'Strategic':
        return 'Watch / Do Not Chase';
      case 'Swing':
        return 'No Chase / Extended';
      case 'Options':
        return 'Call Watch / Extended';
      case 'Speculative':
        return 'No Chase / Trim Watch';
      default:
        return 'Watch / Do Not Chase';
    }
  }

  private coreWatchlistAction(ts: TrendSetup, ms: MomentumShift, ba: BaseAction): string {
    if (ts === 'Quality Trend Entry' && ms === 'Uptrend') return 'Accumulate Starter';
    if (ts === 'Confirmed Constructive' && ms === 'Uptrend') return 'Watch / Starter OK';
    if (ms === 'Active Buy Trigger') return 'Buy Candidate';
    if (ms === 'Bullish Shift') return 'Core Add Watch';
    if (ts === 'Constructive Extended' || ms === 'Warning — Overbought Run')
      return 'Watch / Do Not Chase';
    if (ts === 'Cooling') return 'Watch / No New Buy';
    if (ts === 'Technical Caution' || ms === 'Breakdown') return 'Wait / Review';
    if (ts === 'Waterfall / Falling Knife' || ms === 'Warning') return 'Avoid / Wait';
    if (ms === 'Active Sell Trigger') return 'Avoid New Buy / Review';
    return ba;
  }

  private strategicWatchlistAction(ts: TrendSetup, ms: MomentumShift, ba: BaseAction): string {
    if (ts === 'Quality Trend Entry' && ms === 'Uptrend') return 'Accumulate Starter';
    if (ts === 'Confirmed Constructive' && ms === 'Uptrend') return 'Watch / Starter OK';
    if (ms === 'Active Buy Trigger') return 'Buy Candidate / Staged Entry';
    if (ms === 'Consolidation / Dip-Buy') return 'Accumulate on Pullback';
    if (ms === 'Bullish Shift') return 'Early Buy Watch';
    if (ts === 'Constructive Extended' || ms === 'Warning — Overbought Run')
      return 'Watch / Do Not Chase';
    if (ts === 'Cooling') return 'Watch / No Entry';
    if (ts === 'Technical Caution' || ms === 'Breakdown') return 'Wait / Technical Caution';
    if (ts === 'Waterfall / Falling Knife' || ms === 'Warning') return 'Avoid / Wait';
    if (ms === 'Active Sell Trigger') return 'Avoid New Buy / Review';
    return ba;
  }

  private swingWatchlistAction(ts: TrendSetup, ms: MomentumShift, ba: BaseAction): string {
    if (ms === 'Active Buy Trigger') return 'Buy Trade / Starter Only';
    if (ms === 'Bullish Shift') return 'Starter Buy Watch';
    if (ms === 'Consolidation / Dip-Buy') return 'Buy Pullback Trade';
    if ((ts === 'Confirmed Constructive' || ts === 'Quality Trend Entry') && ms === 'Uptrend')
      return 'Watch / No Chase';
    if (ts === 'Constructive Extended' || ms === 'Warning — Overbought Run')
      return 'No Chase / Extended Risk';
    if (ts === 'Cooling') return 'No Entry / Wait';
    if (ms === 'Breakdown') return 'Avoid / Wait';
    if (ts === 'Waterfall / Falling Knife' || ms === 'Warning') return 'Avoid / Wait';
    if (ms === 'Active Sell Trigger') return 'Avoid / Short Watch';
    return ba;
  }

  private speculativeWatchlistAction(ts: TrendSetup, ms: MomentumShift, ba: BaseAction): string {
    if (ms === 'Active Buy Trigger') return 'Small Spec Buy Candidate';
    if ((ts === 'Confirmed Constructive' || ts === 'Quality Trend Entry') && ms === 'Uptrend')
      return 'Watch / Small Entry OK';
    if (ms === 'Bullish Shift') return 'Spec Starter Watch';
    if (ms === 'Consolidation / Dip-Buy') return 'Small Add Watch';
    if (ts === 'Constructive Extended' || ms === 'Warning — Overbought Run')
      return 'No Chase / Trim Watch';
    if (ts === 'Cooling') return 'Watch / No Add';
    if (ms === 'Breakdown') return 'Wait / Thesis Review';
    if (ts === 'Waterfall / Falling Knife' || ms === 'Warning') return 'Avoid / No Add';
    if (ms === 'Active Sell Trigger') return 'Avoid / Profit-Take Watch';
    return ba;
  }

  private optionsWatchlistAction(ts: TrendSetup, ms: MomentumShift, _ba: BaseAction): string {
    if (ms === 'Active Buy Trigger') return 'Call Entry / Buy Trigger';
    if (ms === 'Active Sell Trigger') return 'Put Entry / Sell Trigger';
    if (ms === 'Bullish Shift') return 'Call Watch / Wait for Confirmation';
    if (ms === 'Bearish Shift') return 'Put Watch / Entry OK';
    if (ms === 'Warning — Overbought Run') return 'Put Watch / Extended';
    if (ms === 'Uptrend') return 'Call Watch / No Chase';
    return 'Call Watch / Entry OK';
  }

  private portfolioFinalAction(
    dec: DecisionResult,
    role: InvestmentRole,
    isOwned: boolean,
  ): string {
    if (!isOwned) return this.watchlistFinalAction(dec, role);
    const { trendSetup: ts, momentumShift: ms, baseAction: ba } = dec;
    switch (role) {
      case 'Core':
        return this.corePortfolioAction(ts, ms);
      case 'Strategic':
        return this.strategicPortfolioAction(ts, ms);
      case 'Swing':
        return this.swingPortfolioAction(ts, ms);
      case 'Options':
        return this.optionsPortfolioAction(ts, ms);
      case 'Speculative':
        return this.speculativePortfolioAction(ts, ms);
      default:
        return ba;
    }
  }

  private corePortfolioAction(ts: TrendSetup, ms: MomentumShift): string {
    if ((ts === 'Confirmed Constructive' || ts === 'Quality Trend Entry') && ms === 'Uptrend')
      return 'Hold / Add If Underweight';
    if (ms === 'Active Buy Trigger') return 'Add to Position';
    if (ms === 'Bullish Shift') return 'Hold / Watch for Add';
    if (ts === 'Constructive Extended' || ms === 'Warning — Overbought Run')
      return 'Hold / Do Not Chase';
    if (ts === 'Cooling') return 'Hold / No New Buy';
    if (ts === 'Technical Caution' || ms === 'Breakdown' || ms === 'Active Sell Trigger')
      return 'Hold / Review';
    if (ts === 'Waterfall / Falling Knife' || ms === 'Warning') return 'Hold / Risk Review';
    if (ms === 'Uptrend') return 'Hold / No Chase';
    return 'Hold';
  }

  private strategicPortfolioAction(ts: TrendSetup, ms: MomentumShift): string {
    if ((ts === 'Confirmed Constructive' || ts === 'Quality Trend Entry') && ms === 'Uptrend')
      return 'Hold / No Chase';
    if (ms === 'Active Buy Trigger') return 'Add / Staged Entry';
    if (ms === 'Consolidation / Dip-Buy') return 'Add on Pullback';
    if (ms === 'Bullish Shift') return 'Hold / Watch for Add';
    if (ts === 'Constructive Extended' || ms === 'Warning — Overbought Run')
      return 'Hold / Do Not Chase';
    if (ts === 'Cooling') return 'Hold / No Add';
    if (ts === 'Technical Caution' || ms === 'Breakdown') return 'Hold / Technical Caution';
    if (ts === 'Waterfall / Falling Knife' || ms === 'Warning') return 'Hold / Risk Review';
    if (ms === 'Active Sell Trigger') return 'Trim / Review';
    if (ms === 'Uptrend') return 'Hold / No Chase';
    return 'Hold';
  }

  private swingPortfolioAction(ts: TrendSetup, ms: MomentumShift): string {
    if (ms === 'Active Buy Trigger') return 'Add to Trade';
    if (ms === 'Active Sell Trigger') return 'Exit / Take Profit';
    if (ms === 'Bullish Shift') return 'Hold Swing / Watch';
    if (ms === 'Bearish Shift') return 'Trim / Exit Watch';
    if ((ts === 'Confirmed Constructive' || ts === 'Quality Trend Entry') && ms === 'Uptrend')
      return 'Hold / Trail Stop';
    if (ts === 'Constructive Extended' || ms === 'Warning — Overbought Run')
      return 'Trail Stop / Trim';
    if (ts === 'Cooling' || ms === 'Breakdown') return 'Exit / Cut Loss';
    if (ts === 'Waterfall / Falling Knife' || ms === 'Warning') return 'Cut Loss / Exit';
    if (ms === 'Uptrend') return 'Hold / Trail Stop';
    return 'Hold Swing';
  }

  private speculativePortfolioAction(ts: TrendSetup, ms: MomentumShift): string {
    if (ms === 'Active Buy Trigger') return 'Small Add';
    if (ms === 'Active Sell Trigger') return 'Take Profit / Exit';
    if (ms === 'Bullish Shift') return 'Hold Spec / Watch';
    if ((ts === 'Confirmed Constructive' || ts === 'Quality Trend Entry') && ms === 'Uptrend')
      return 'Hold Spec / Trail Stop';
    if (ts === 'Constructive Extended' || ms === 'Warning — Overbought Run')
      return 'Trim / Trail Stop';
    if (ts === 'Cooling' || ms === 'Breakdown') return 'Cut Loss / Thesis Review';
    if (ts === 'Waterfall / Falling Knife' || ms === 'Warning') return 'Cut Loss / Exit';
    if (ms === 'Uptrend') return 'Hold Spec / Trail Stop';
    return 'Hold Spec';
  }

  private optionsPortfolioAction(ts: TrendSetup, ms: MomentumShift): string {
    if (ms === 'Active Sell Trigger') return 'Close / Take Profit';
    if (ms === 'Bearish Shift') return 'Close / Watch for Reversal';
    if (ms === 'Active Buy Trigger') return 'Roll / Extend';
    if (ms === 'Warning — Overbought Run') return 'Trail / Protect Gains';
    if (ms === 'Uptrend') return 'Hold / Trail Stop';
    if (ts === 'Cooling' || ms === 'Breakdown') return 'Close / Protect';
    return 'Hold Option';
  }

  private watchlistHover(dec: DecisionResult, _role: InvestmentRole): string {
    const { trendSetup: ts, momentumShift: ms } = dec;
    if (ts === 'Quality Trend Entry')
      return 'High-quality candidate in healthy uptrend with constructive RSI. Suitable for staged Core/Strategic entry.';
    if (ts === 'Waterfall / Falling Knife')
      return 'Deeply oversold with selling still active. RSI below signal. Do NOT enter.';
    if (ms === 'Active Buy Trigger')
      return 'Buyers confirmed: top-half candle close from oversold with volume or MACD improvement. High-probability entry.';
    if (ms === 'Active Sell Trigger')
      return 'Distribution confirmed: bottom-half candle close from overbought with volume or MACD weakening.';
    if (ms === 'Warning — Overbought Run')
      return 'RSI elevated but candle has NOT confirmed a bearish reversal. Extended — do not chase.';
    if (ms === 'Consolidation / Dip-Buy')
      return 'Price near 20-day SMA with controlled RSI and elevated volume. Institutional dip-buy zone.';
    if (ms === 'Breakdown')
      return 'Price broke below EMA9 with RSI fading. Defensive risk triggered.';
    if (ms === 'Uptrend') return 'Trend healthy. Hold existing, no new chase.';
    return dec.trendSetupReason;
  }

  private portfolioHover(
    dec: DecisionResult,
    role: InvestmentRole,
    isOwned: boolean,
    context?: PortfolioItemContext,
  ): string {
    const { trendSetup: ts, momentumShift: ms } = dec;
    if (!isOwned) return this.watchlistHover(dec, role);
    // TFSA profit-take rule fires first when triggered
    if (context && this.tfsaProfitTakeTriggered({} as any, context))
      return 'TFSA account with ≥20% gain, ≤6 month hold, RSI≥65 and price at/above analyst target. Consider taking partial profits — do not chase further.';
    if ((ts === 'Confirmed Constructive' || ts === 'Quality Trend Entry') && ms === 'Uptrend') {
      if (role === 'Core')
        return 'Core holding is technically healthy. Continue holding. Add only if under target weight.';
      if (role === 'Strategic')
        return 'Strategic holding in healthy trend. Hold — no chasing additional shares.';
    }
    if (ms === 'Warning — Overbought Run')
      return 'Trend extended but no bearish reversal confirmed. Maintain position, do not add.';
    if (ts === 'Technical Caution' || ms === 'Breakdown') {
      if (role === 'Core')
        return 'Core holding has short-term technical weakness. Review thesis/size. Do NOT sell based solely on momentum.';
      return `${role} holding shows technical weakness. Review thesis and position size.`;
    }
    if (ms === 'Active Sell Trigger' && (role === 'Core' || role === 'Strategic'))
      return 'Technical pressure building. Stop new buying and review stop-loss levels.';
    if (ms === 'Uptrend')
      return 'Holding in constructive uptrend. Continue with appropriate stop discipline.';
    return dec.trendSetupReason;
  }

  trendSetupClass(ts: TrendSetup): string {
    switch (ts) {
      case 'Waterfall / Falling Knife':
        return 'ts-waterfall';
      case 'Oversold Reversal Watch':
        return 'ts-reversal';
      case 'Constructive Extended':
        return 'ts-extended';
      case 'Quality Trend Entry':
        return 'ts-quality';
      case 'Confirmed Constructive':
        return 'ts-constructive';
      case 'Early Reversal':
        return 'ts-early-reversal';
      case 'Cooling':
        return 'ts-cooling';
      case 'Technical Caution':
        return 'ts-caution';
      default:
        return 'ts-neutral';
    }
  }

  momentumShiftClass(ms: MomentumShift): string {
    switch (ms) {
      case 'Active Buy Trigger':
        return 'ms-confirmed-buy';
      case 'Active Sell Trigger':
        return 'ms-confirmed-sell';
      case 'Bullish Shift':
        return 'ms-bullish';
      case 'Bearish Shift':
        return 'ms-bearish';
      case 'Warning':
        return 'ms-warning';
      case 'Warning — Overbought Run':
        return 'ms-warning';
      case 'Breakdown':
        return 'ms-breakdown';
      case 'Consolidation / Dip-Buy':
        return 'ms-consolidation';
      case 'Uptrend':
        return 'ms-uptrend';
      default:
        return 'ms-neutral';
    }
  }

  baseActionClass(ba: BaseAction): string {
    switch (ba) {
      case 'Confirmed Buy Signal':
        return 'ma-confirmed-buy';
      case 'Confirmed Sell Signal':
        return 'ma-confirmed-sell';
      case 'Early Buy Watch':
        return 'ma-early-warning';
      case 'Early Sell Watch':
        return 'ma-early-warning';
      case 'Watch / Do Not Chase':
        return 'ma-hold';
      case 'Avoid / Wait':
        return 'ma-avoid';
      case 'Reduce / Review':
        return 'ma-reduce';
      case 'Buy / Accumulate':
        return 'ma-accumulate';
      case 'Hold Longs':
        return 'ma-hold';
      default:
        return 'ma-standby';
    }
  }

  private finalActionClass(action: string): string {
    const a = action.toLowerCase();
    // TFSA profit-taking rule gets its own amber class
    if (a.includes('partial profit')) return 'ma-tfsa-profit';
    if (
      a.includes('buy') ||
      a.includes('accumulate') ||
      a.includes('add') ||
      a.includes('starter') ||
      a.includes('call entry')
    )
      return 'ma-confirmed-buy';
    if (
      a.includes('sell') ||
      a.includes('exit') ||
      a.includes('cut') ||
      a.includes('trim') ||
      a.includes('put entry')
    )
      return 'ma-confirmed-sell';
    if (a.includes('avoid') || a.includes('caution') || a.includes('review') || a.includes('wait'))
      return 'ma-avoid';
    if (a.includes('hold') || a.includes('no chase') || a.includes('watch')) return 'ma-hold';
    if (a.includes('reduce') || a.includes('trail') || a.includes('protect')) return 'ma-reduce';
    return 'ma-standby';
  }
}
