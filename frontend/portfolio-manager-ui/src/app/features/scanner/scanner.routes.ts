import { Routes } from '@angular/router';
import { authGuard } from '../../core/guards/auth.guard';

export const SCANNER_ROUTES: Routes = [
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./scanner-page.component').then((m) => m.ScannerPageComponent),
  },
];
