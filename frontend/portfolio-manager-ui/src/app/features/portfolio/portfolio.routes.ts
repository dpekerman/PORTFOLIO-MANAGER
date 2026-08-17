import { Routes } from '@angular/router';
import { authGuard } from '../../core/guards/auth.guard';

export const PORTFOLIO_ROUTES: Routes = [
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./portfolio-page.component').then((m) => m.PortfolioPageComponent),
  },
];
