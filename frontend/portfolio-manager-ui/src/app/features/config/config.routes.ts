import { Routes } from '@angular/router';

export const CONFIG_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./config-page.component').then((m) => m.ConfigPageComponent),
  },
];
