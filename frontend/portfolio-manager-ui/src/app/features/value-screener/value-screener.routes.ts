import { Routes } from '@angular/router';

export const VALUE_SCREENER_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./value-screener-page.component').then((m) => m.ValueScreenerPageComponent),
  },
];
