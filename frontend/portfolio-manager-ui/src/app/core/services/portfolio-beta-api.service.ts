import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PortfolioBetaResult } from '../models/portfolio.models';
import { PortfolioApiService } from './portfolio-api.service';

@Injectable({ providedIn: 'root' })
export class PortfolioBetaApiService {
  private readonly api = inject(PortfolioApiService);

  getBeta(): Observable<PortfolioBetaResult> {
    return this.api.getPortfolioBeta();
  }
}
