import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  ActionScoreDto,
  AnalyticsDecisionPerformanceResponse,
  DashboardResponse,
  MarketLeadershipResponse,
  PerformanceSummaryResponse,
  PortfolioActionDto,
  StateChangeDto,
} from '../models/portfolio.models';
import { DashboardApiService } from './dashboard-api.service';

@Injectable({ providedIn: 'root' })
export class DashboardStateService {
  private readonly api = inject(DashboardApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly _data = signal<DashboardResponse | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);
  private readonly _portfolioActions = signal<PortfolioActionDto[]>([]);
  private readonly _actionsLoading = signal(false);
  private readonly _stateChanges = signal<StateChangeDto[]>([]);
  private readonly _stateChangesLoading = signal(false);
  private readonly _decisionPerformance = signal<AnalyticsDecisionPerformanceResponse | null>(null);
  private readonly _performanceLoading = signal(false);
  private readonly _marketLeadership = signal<MarketLeadershipResponse | null>(null);
  private readonly _marketLeadershipLoading = signal(false);
  private readonly _actionScores = signal<ActionScoreDto[]>([]);
  private readonly _actionScoresLoading = signal(false);
  private readonly _performanceSummary = signal<PerformanceSummaryResponse | null>(null);
  private readonly _performanceSummaryLoading = signal(false);

  readonly data = this._data.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly hasData = this._data.asReadonly();
  readonly portfolioActions = this._portfolioActions.asReadonly();
  readonly actionsLoading = this._actionsLoading.asReadonly();
  readonly stateChanges = this._stateChanges.asReadonly();
  readonly stateChangesLoading = this._stateChangesLoading.asReadonly();
  readonly decisionPerformance = this._decisionPerformance.asReadonly();
  readonly performanceLoading = this._performanceLoading.asReadonly();
  readonly marketLeadership = this._marketLeadership.asReadonly();
  readonly marketLeadershipLoading = this._marketLeadershipLoading.asReadonly();
  readonly actionScores = this._actionScores.asReadonly();
  readonly actionScoresLoading = this._actionScoresLoading.asReadonly();
  readonly performanceSummary = this._performanceSummary.asReadonly();
  readonly performanceSummaryLoading = this._performanceSummaryLoading.asReadonly();

  constructor() {
    this.load();
    this.loadPortfolioActions();
    this.loadStateChanges();
    this.loadMarketLeadership();
    this.loadPerformanceSummary();
  }

  load(): void {
    this._loading.set(true);
    this._error.set(null);
    this.api
      .getLatest()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => {
          this._data.set(data);
          this._loading.set(false);
        },
        error: () => {
          this._error.set('Dashboard snapshot unavailable');
          this._loading.set(false);
        },
      });
  }

  refresh(): void {
    this._loading.set(true);
    this._error.set(null);
    this.api
      .refresh()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (data) => {
          this._data.set(data);
          this._loading.set(false);
          this.loadPortfolioActions();
          this.loadStateChanges();
          this.loadMarketLeadership();
          this._actionScores.set([]);
          this.loadActionScores();
          this.loadPerformanceSummary();
        },
        error: () => {
          this._error.set('Dashboard refresh failed');
          this._loading.set(false);
        },
      });
  }

  loadPortfolioActions(): void {
    this._actionsLoading.set(true);
    this.api
      .getPortfolioActions()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (a) => {
          this._portfolioActions.set(a);
          this._actionsLoading.set(false);
        },
        error: () => this._actionsLoading.set(false),
      });
  }

  loadStateChanges(): void {
    this._stateChangesLoading.set(true);
    this.api
      .getStateChangesToday()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (c) => {
          this._stateChanges.set(c);
          this._stateChangesLoading.set(false);
        },
        error: () => this._stateChangesLoading.set(false),
      });
  }

  loadMarketLeadership(): void {
    this._marketLeadershipLoading.set(true);
    this.api
      .getMarketLeadership()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (ml) => {
          this._marketLeadership.set(ml);
          this._marketLeadershipLoading.set(false);
        },
        error: () => this._marketLeadershipLoading.set(false),
      });
  }

  loadDecisionPerformance(): void {
    if (this._decisionPerformance()) return;
    this._performanceLoading.set(true);
    this.api
      .getDecisionPerformance()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (d) => {
          this._decisionPerformance.set(d);
          this._performanceLoading.set(false);
        },
        error: () => this._performanceLoading.set(false),
      });
  }

  loadActionScores(): void {
    this._actionScoresLoading.set(true);
    this.api
      .getActionScores()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (s) => {
          this._actionScores.set(s);
          this._actionScoresLoading.set(false);
        },
        error: () => this._actionScoresLoading.set(false),
      });
  }

  loadPerformanceSummary(): void {
    this._performanceSummaryLoading.set(true);
    this.api
      .getPerformanceSummary()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (s) => {
          this._performanceSummary.set(s);
          this._performanceSummaryLoading.set(false);
        },
        error: () => this._performanceSummaryLoading.set(false),
      });
  }
}
