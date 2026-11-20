import { Routes } from '@angular/router';

export const TRADING_OPPORTUNITIES_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./trading-opportunities-page.component').then((m) => m.TradingOpportunitiesPageComponent),
  },
];
