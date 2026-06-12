import { Routes } from '@angular/router';
import { authGuard } from '../../core/guards/auth.guard';

export const WATCHLIST_ROUTES: Routes = [
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./watchlist-page.component').then((m) => m.WatchlistPageComponent),
  },
];
