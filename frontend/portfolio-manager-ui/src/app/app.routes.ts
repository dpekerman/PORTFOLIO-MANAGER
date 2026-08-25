import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'setup',
    loadComponent: () =>
      import('./features/auth/setup/setup.component').then((m) => m.SetupComponent),
  },
  {
    path: '',
    loadComponent: () => import('./shared/layout/layout.component').then((m) => m.LayoutComponent),
    canActivate: [authGuard],
    children: [
      {
        path: 'dashboard',
        loadChildren: () =>
          import('./features/dashboard/dashboard.routes').then((m) => m.DASHBOARD_ROUTES),
      },
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
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
    ],
  },
  { path: '**', redirectTo: 'login' },
];
