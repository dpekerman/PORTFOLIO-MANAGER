import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PortfolioActionDto } from '../../../core/models/portfolio.models';
import { priceStructureLabel, priceStructureTooltip } from '../../../core/price-structure-display';
import { DashboardStateService } from '../../../core/services/dashboard-state.service';
import {
  DecisionEngineService,
  evaluateWatchlistEntry,
  WatchlistEntryDecision,
} from '../../../core/services/decision-engine.service';
import { DemoModeService } from '../../../core/services/demo-mode.service';
import { ScannerStateService } from '../../../core/services/scanner-state.service';
import { formatTrendShift } from '../../../core/technical-display';

export const ACTION_CENTER_FILTER_STORAGE_KEY = 'dashboard_action_center_filter';
export type ActionCenterFilter = 'ALL' | 'REQUIRED' | 'DEVELOPING' | 'INFORMATIONAL';
export type ActionCenterSortColumn =
  | 'symbol'
  | 'role'
  | 'rsi'
  | 'maStructure'
  | 'momentum'
  | 'priceStructure'
  | 'allocation'
  | 'action';
export type ActionCenterSort = { column: ActionCenterSortColumn; direction: 1 | -1 };

@Component({
  selector: 'app-portfolio-actions-widget',
  templateUrl: './portfolio-actions-widget.component.html',
  styleUrl: './portfolio-actions-widget.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, MatIconModule, MatProgressBarModule, MatTooltipModule],
})
export class PortfolioActionsWidgetComponent {
  private static readonly FILTER_STORAGE_KEY = ACTION_CENTER_FILTER_STORAGE_KEY;
  private readonly demoMode = inject(DemoModeService);
  private readonly decisionEngine = inject(DecisionEngineService);
  private readonly scanner = inject(ScannerStateService);
  private readonly filter = signal<ActionCenterFilter>(this.loadFilter());
  private readonly holdingsSort = signal<ActionCenterSort>({ column: 'symbol', direction: 1 });
  private readonly watchlistSort = signal<ActionCenterSort>({ column: 'symbol', direction: 1 });
  protected readonly dashboard = inject(DashboardStateService);
  protected readonly actions = this.dashboard.portfolioActions;
  protected readonly loading = this.dashboard.actionsLoading;
  private readonly watchlistEntryDecisions = computed(() => {
    const decisions = new Map<string, WatchlistEntryDecision>();
    for (const action of this.actions().filter((row) => !row.isInPortfolio)) {
      decisions.set(action.symbol.toUpperCase(), this.calculateWatchlistEntryDecision(action));
    }
    return decisions;
  });

  protected readonly holdingActions = computed(() =>
    this.sortActions(
      this.filteredActions().filter((a) => a.isInPortfolio),
      this.holdingsSort(),
    ),
  );
  protected readonly watchlistActions = computed(() =>
    this.sortActions(
      this.filteredActions().filter((a) => !a.isInPortfolio),
      this.watchlistSort(),
    ),
  );
  protected readonly activeFilter = this.filter.asReadonly();
  private readonly filteredActions = computed(() => {
    const filter = this.filter();
    return filter === 'ALL'
      ? this.actions()
      : this.actions().filter((a) => a.actionPriority === filter);
  });

  protected readonly requiredCount = computed(
    () => this.actions().filter((a) => a.actionPriority === 'REQUIRED').length,
  );
  protected readonly developingCount = computed(
    () => this.actions().filter((a) => a.actionPriority === 'DEVELOPING').length,
  );
  protected readonly informationalCount = computed(
    () => this.actions().filter((a) => a.actionPriority === 'INFORMATIONAL').length,
  );

  protected toggleFilter(filter: 'REQUIRED' | 'DEVELOPING' | 'INFORMATIONAL'): void {
    this.setFilter(this.filter() === filter ? 'ALL' : filter);
  }

  protected resetFilter(): void {
    this.setFilter('ALL');
  }

  protected toggleHoldingsSort(column: ActionCenterSortColumn): void {
    this.holdingsSort.update((sort) => toggleSort(sort, column));
  }

  protected toggleWatchlistSort(column: ActionCenterSortColumn): void {
    this.watchlistSort.update((sort) => toggleSort(sort, column));
  }

  protected holdingsSortIndicator(column: ActionCenterSortColumn): string {
    return sortIndicator(this.holdingsSort(), column);
  }

  protected watchlistSortIndicator(column: ActionCenterSortColumn): string {
    return sortIndicator(this.watchlistSort(), column);
  }

  protected severityCls(a: PortfolioActionDto): string {
    return `action-${a.actionSeverity} priority-${a.actionPriority.toLowerCase()}`;
  }
  protected scanIcon(scanType: string): string {
    return scanType === 'Oversold' ? 'trending_down' : 'trending_up';
  }

  protected structureLabel(action: PortfolioActionDto): string {
    return priceStructureLabel(action.priceStructure, (value) => this.demoMode.maskValue(value));
  }

  protected structureTooltip(action: PortfolioActionDto): string {
    return priceStructureTooltip(action.priceStructure, (value) => this.demoMode.maskValue(value));
  }

  protected entryStatus(action: PortfolioActionDto): string {
    return this.watchlistEntryDecision(action).finalAction;
  }

  protected entryStatusTooltip(action: PortfolioActionDto): string {
    const decision = this.watchlistEntryDecision(action);
    return `${decision.finalAction}\n\n${decision.finalActionReason}\n\nStructure: ${decision.priceStructureState}\nMomentum: ${decision.momentumState}\nMA Structure: ${decision.maStructure}\nBuy Score: ${decision.buyScore ?? '—'}\nRole: ${decision.role}`;
  }

  protected hasEodSignal(action: PortfolioActionDto): boolean {
    return !!action.latestEodSignalState;
  }

  protected eodBadgeLabel(action: PortfolioActionDto): string {
    if (action.latestEodIsInvalidated) return 'EOD X';
    if (action.latestEodSignalState === 'Active') return action.latestEodIsNew ? 'EOD NEW' : 'EOD';
    return 'EOD DEV';
  }

  protected eodTooltip(action: PortfolioActionDto): string {
    return `Latest EOD signal\nState: ${action.latestEodSignalState ?? 'n/a'}\nScan: ${action.latestEodScanType ?? 'n/a'}\nTrend: ${formatTrendShift(action.latestEodTrendShift, 'n/a')}`;
  }

  private setFilter(filter: ActionCenterFilter): void {
    this.filter.set(filter);
    saveActionCenterFilter(localStorage, filter);
  }

  private loadFilter(): ActionCenterFilter {
    return loadActionCenterFilter(localStorage);
  }

  private sortActions(actions: PortfolioActionDto[], sort: ActionCenterSort): PortfolioActionDto[] {
    return [...actions].sort((left, right) => {
      const comparison =
        sort.column === 'action' && !left.isInPortfolio && !right.isInPortfolio
          ? this.entryStatus(left).localeCompare(this.entryStatus(right))
          : compareActionCenterValues(left, right, sort.column);
      return comparison * sort.direction || left.symbol.localeCompare(right.symbol);
    });
  }

  private watchlistEntryDecision(action: PortfolioActionDto): WatchlistEntryDecision {
    return (
      this.watchlistEntryDecisions().get(action.symbol.toUpperCase()) ??
      this.calculateWatchlistEntryDecision(action)
    );
  }

  private calculateWatchlistEntryDecision(action: PortfolioActionDto): WatchlistEntryDecision {
    const scannerRow = [...this.scanner.oversold(), ...this.scanner.overbought()].find(
      (row) => row.symbol.toUpperCase() === action.symbol.toUpperCase(),
    );
    if (scannerRow) {
      const decision = this.decisionEngine.translateForWatchlist(scannerRow, action.holdingRole);
      if (decision.watchlistDiagnostics) return decision.watchlistDiagnostics;
    }
    return evaluateWatchlistEntry({
      role: action.holdingRole,
      rsi: action.rsi,
      buyScore: null,
      trendSetup: null,
      momentumShift: action.trendShift,
      momentumState: action.momentumState,
      maStructure: action.maStructure,
      priceStructure: action.priceStructure,
      chaseRisk: !!action.chaseRisk,
      trendDamage: action.fibZone === 'Trend Damage',
    });
  }
}

export function isActionCenterFilter(value: string | null): value is ActionCenterFilter {
  return (
    value === 'ALL' || value === 'REQUIRED' || value === 'DEVELOPING' || value === 'INFORMATIONAL'
  );
}

export function loadActionCenterFilter(storage: Pick<Storage, 'getItem'>): ActionCenterFilter {
  const stored = storage.getItem(ACTION_CENTER_FILTER_STORAGE_KEY);
  return isActionCenterFilter(stored) ? stored : 'ALL';
}

export function saveActionCenterFilter(
  storage: Pick<Storage, 'setItem'>,
  filter: ActionCenterFilter,
): void {
  storage.setItem(ACTION_CENTER_FILTER_STORAGE_KEY, filter);
}

export function toggleSort(
  sort: ActionCenterSort,
  column: ActionCenterSortColumn,
): ActionCenterSort {
  return sort.column === column
    ? { column, direction: sort.direction === 1 ? -1 : 1 }
    : { column, direction: 1 };
}

function sortIndicator(sort: ActionCenterSort, column: ActionCenterSortColumn): string {
  return sort.column !== column ? '' : sort.direction === 1 ? ' ▲' : ' ▼';
}

export function compareActionCenterValues(
  left: PortfolioActionDto,
  right: PortfolioActionDto,
  column: ActionCenterSortColumn,
): number {
  const allocationRank: Record<string, number> = { over: 3, 'on-target': 2, under: 1, '': 0 };
  switch (column) {
    case 'symbol':
      return left.symbol.localeCompare(right.symbol);
    case 'role':
      return left.holdingRole.localeCompare(right.holdingRole);
    case 'rsi':
      return (left.rsi ?? Number.POSITIVE_INFINITY) - (right.rsi ?? Number.POSITIVE_INFINITY);
    case 'maStructure':
      return (left.maStructure ?? '').localeCompare(right.maStructure ?? '');
    case 'momentum':
      return (left.momentumState ?? '').localeCompare(right.momentumState ?? '');
    case 'priceStructure':
      return priceStructureLabel(left.priceStructure).localeCompare(
        priceStructureLabel(right.priceStructure),
      );
    case 'allocation':
      return (
        (allocationRank[left.allocationStatus] ?? 0) - (allocationRank[right.allocationStatus] ?? 0)
      );
    case 'action':
      return left.actionLabel.localeCompare(right.actionLabel);
  }
}
