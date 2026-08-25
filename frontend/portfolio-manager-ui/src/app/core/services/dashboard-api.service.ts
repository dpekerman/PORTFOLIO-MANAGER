import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { DashboardResponse } from '../models/portfolio.models';
import { PortfolioApiService } from './portfolio-api.service';

@Injectable({ providedIn: 'root' })
export class DashboardApiService {
  private readonly portfolioApi = inject(PortfolioApiService);

  getLatest(): Observable<DashboardResponse | null> {
    return this.portfolioApi.getDashboard();
  }

  refresh(): Observable<DashboardResponse> {
    return this.portfolioApi.refreshDashboard();
  }
}
