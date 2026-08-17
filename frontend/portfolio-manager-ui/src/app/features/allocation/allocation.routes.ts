import { Routes } from '@angular/router';
import { authGuard } from '../../core/guards/auth.guard';

export const ALLOCATION_ROUTES: Routes = [
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./allocation-page.component').then((m) => m.AllocationPageComponent),
  },
];
