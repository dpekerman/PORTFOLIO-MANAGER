import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PortfolioBetaResult } from '../models/portfolio.models';
import { PortfolioApiService } from './portfolio-api.service';

@Injectable({ providedIn: 'root' })
export class PortfolioBetaApiService {
  private readonly api = inject(PortfolioApiService);

  getBeta(betaOverrides?: Record<string, number>): Observable<PortfolioBetaResult> {
    if (betaOverrides && Object.keys(betaOverrides).length > 0) {
      return this.api.calculatePortfolioBeta(betaOverrides);
    }
    return this.api.getPortfolioBeta();
  }
}
