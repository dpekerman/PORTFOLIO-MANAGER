import { Injectable, inject } from '@angular/core';
import {
  FinalActionSyncItem,
  PortfolioItem,
  PortfolioSummary,
  RsiScanResult,
} from '../models/portfolio.models';
import { CashStateService } from './cash-state.service';
import { DecisionEngineService, PortfolioItemContext } from './decision-engine.service';
import { OptionStateService } from './option-state.service';
import { PortfolioApiService } from './portfolio-api.service';
import { PortfolioStateService } from './portfolio-state.service';

/**
 * Single source of truth for the profit-taking/account context used by `translateForPortfolio`.
 * Computed here (not duplicated per caller) so the Portfolio grid and the values pushed to
 * Dashboard Action Center always agree.
 */
@Injectable({ providedIn: 'root' })
export class PortfolioFinalActionSyncService {
  private readonly portfolio = inject(PortfolioStateService);
  private readonly cashState = inject(CashStateService);
  private readonly optionState = inject(OptionStateService);
  private readonly engine = inject(DecisionEngineService);
  private readonly api = inject(PortfolioApiService);

  /** Builds the profit-taking/account context for a single held symbol. */
  buildContext(
    item: PortfolioItem,
    r: RsiScanResult,
    allSummaries: PortfolioSummary[],
    grandTotal: number,
  ): PortfolioItemContext {
    const price = r.currentPrice;
    const unrealizedGainPct =
      item.averageCostBasis > 0
        ? ((price - item.averageCostBasis) / item.averageCostBasis) * 100
        : null;
    const holdingDays = item.openDate
      ? Math.floor((Date.now() - new Date(item.openDate).getTime()) / (1000 * 60 * 60 * 24))
      : null;
    const distanceFrom52WeekHighPct =
      r.week52High > 0 ? ((price - r.week52High) / r.week52High) * 100 : null;
    const marketValue = item.isManual
      ? (item.manualMarketValue ?? item.averageCostBasis)
      : price * item.shares;
    const positionSizePct = grandTotal > 0 ? (marketValue / grandTotal) * 100 : null;

    return {
      accountType: item.accountType ?? null,
      unrealizedGainPct,
      holdingDays,
      distanceFrom52WeekHighPct,
      positionSizePct,
      riskControlClosePct: this.riskControlClosePct(item.symbol, allSummaries),
      decisionSource: item.decisionSource ?? null,
    };
  }

  /** % of a ticker's total original shares closed via Risk Control decisions, or null if none. */
  private riskControlClosePct(symbol: string, allSummaries: PortfolioSummary[]): number | null {
    const rcCloses = allSummaries.filter(
      (s) =>
        s.item.symbol === symbol &&
        s.item.transactionType === 'CLOSE' &&
        s.item.decisionSource === 'Risk Control',
    );
    if (rcCloses.length === 0) return null;
    const rcClosedShares = rcCloses.reduce((sum, s) => sum + s.item.shares, 0);
    const openShares = allSummaries
      .filter((s) => s.item.symbol === symbol && s.item.transactionType !== 'CLOSE')
      .reduce((sum, s) => sum + s.item.shares, 0);
    const total = openShares + rcClosedShares;
    return total > 0 ? (rcClosedShares / total) * 100 : null;
  }

  /** Computes the Final Action for every open, ticker-backed holding that has RSI data. */
  computeAll(rsiMap: Map<string, RsiScanResult>): FinalActionSyncItem[] {
    const summaries = this.portfolio.summaries();
    const grandTotal =
      this.portfolio.totalValue() +
      this.cashState.totalCash() +
      this.optionState.totalMarketValue();

    const items: FinalActionSyncItem[] = [];
    for (const s of summaries) {
      if (s.item.transactionType === 'CLOSE' || s.item.isManual) continue;
      const r = rsiMap.get(s.item.symbol.toUpperCase());
      if (!r) continue;

      const context = this.buildContext(s.item, r, summaries, grandTotal);
      const decision = this.engine.translateForPortfolio(
        r,
        s.item.holdingRole ?? null,
        true,
        context,
      );
      const { severity, priority } = this.engine.actionCenterClassification(
        decision.finalActionClass,
      );
      items.push({
        itemId: s.item.id,
        symbol: s.item.symbol,
        finalAction: decision.finalAction,
        severity,
        priority,
      });
    }
    return items;
  }

  /** Computes and best-effort pushes Final Actions to the backend snapshot. Non-critical — failures are silent. */
  computeAndSync(rsiMap: Map<string, RsiScanResult>): void {
    const items = this.computeAll(rsiMap);
    if (items.length === 0) return;
    this.api.syncFinalActions(items).subscribe({ error: () => {} });
  }
}
