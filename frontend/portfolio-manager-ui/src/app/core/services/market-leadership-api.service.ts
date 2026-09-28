import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CreateMarketLeadershipTrackerRequest,
  MarketLeadershipTrackerDto,
} from '../models/portfolio.models';

@Injectable({ providedIn: 'root' })
export class MarketLeadershipApiService {
  private readonly http = inject(HttpClient);

  addTracker(
    request: CreateMarketLeadershipTrackerRequest,
  ): Observable<MarketLeadershipTrackerDto> {
    return this.http.post<MarketLeadershipTrackerDto>(
      '/api/dashboard/market-leadership/trackers',
      request,
    );
  }

  updateTracker(
    trackerId: number,
    request: CreateMarketLeadershipTrackerRequest,
  ): Observable<MarketLeadershipTrackerDto> {
    return this.http.put<MarketLeadershipTrackerDto>(
      `/api/dashboard/market-leadership/trackers/${trackerId}`,
      request,
    );
  }

  removeTracker(trackerId: number): Observable<void> {
    return this.http.delete<void>(`/api/dashboard/market-leadership/trackers/${trackerId}`);
  }
}
