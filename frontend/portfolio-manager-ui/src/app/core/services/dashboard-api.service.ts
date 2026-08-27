import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ActionScoreDto,
  AnalyticsDecisionPerformanceResponse,
  DashboardResponse,
  MarketLeadershipResponse,
  PerformanceSummaryResponse,
  PortfolioActionDto,
  StateChangeDto,
} from '../models/portfolio.models';
import { PortfolioApiService } from './portfolio-api.service';

@Injectable({ providedIn: 'root' })
export class DashboardApiService {
  private readonly portfolioApi = inject(PortfolioApiService);
  private readonly http = inject(HttpClient);

  getLatest(): Observable<DashboardResponse | null> {
    return this.portfolioApi.getDashboard();
  }

  refresh(): Observable<DashboardResponse> {
    return this.portfolioApi.refreshDashboard();
  }

  getPortfolioActions(): Observable<PortfolioActionDto[]> {
    return this.http.get<PortfolioActionDto[]>('/api/dashboard/portfolio-actions');
  }

  getStateChangesToday(): Observable<StateChangeDto[]> {
    return this.http.get<StateChangeDto[]>('/api/dashboard/state-changes-today');
  }

  getMarketLeadership(): Observable<MarketLeadershipResponse> {
    return this.http.get<MarketLeadershipResponse>('/api/dashboard/market-leadership');
  }

  getDecisionPerformance(): Observable<AnalyticsDecisionPerformanceResponse> {
    return this.http.get<AnalyticsDecisionPerformanceResponse>(
      '/api/analytics/decision-performance',
    );
  }

  getPerformanceSummary(): Observable<PerformanceSummaryResponse | null> {
    return this.http.get<PerformanceSummaryResponse>('/api/analytics/performance-summary');
  }

  getActionScores(): Observable<ActionScoreDto[]> {
    return this.http.get<ActionScoreDto[]>('/api/analytics/action-scores');
  }
}
