import { Routes } from '@angular/router';

export const PORTFOLIO_VALUE_HISTORY_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./portfolio-value-history-page.component').then(
        (m) => m.PortfolioValueHistoryPageComponent,
      ),
  },
];
