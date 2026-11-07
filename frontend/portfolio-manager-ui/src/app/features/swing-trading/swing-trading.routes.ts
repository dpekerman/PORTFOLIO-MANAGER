import { Routes } from '@angular/router';

export const SWING_TRADING_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./swing-trading-page.component').then((m) => m.SwingTradingPageComponent),
  },
];
