import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./shared/layout/layout.component').then((m) => m.LayoutComponent),
    children: [
      {
        path: 'portfolio',
        loadChildren: () =>
          import('./features/portfolio/portfolio.routes').then((m) => m.PORTFOLIO_ROUTES),
      },
      {
        path: 'transactions',
        loadChildren: () =>
          import('./features/transactions/transactions.routes').then((m) => m.TRANSACTION_ROUTES),
      },
      {
        path: 'scanner',
        loadChildren: () =>
          import('./features/scanner/scanner.routes').then((m) => m.SCANNER_ROUTES),
      },
      {
        path: 'allocation',
        loadChildren: () =>
          import('./features/allocation/allocation.routes').then((m) => m.ALLOCATION_ROUTES),
      },
      {
        path: 'watchlist',
        loadChildren: () =>
          import('./features/watchlist-page/watchlist.routes').then((m) => m.WATCHLIST_ROUTES),
      },
      {
        path: 'value-screener',
        loadChildren: () =>
          import('./features/value-screener/value-screener.routes').then(
            (m) => m.VALUE_SCREENER_ROUTES,
          ),
      },
      {
        path: 'config',
        loadChildren: () => import('./features/config/config.routes').then((m) => m.CONFIG_ROUTES),
      },
      {
        path: 'eod-signals',
        loadChildren: () =>
          import('./features/eod-signals/eod-signals.routes').then((m) => m.EOD_SIGNALS_ROUTES),
      },
      { path: '', redirectTo: 'scanner', pathMatch: 'full' },
    ],
  },
  { path: '**', redirectTo: 'scanner' },
];
