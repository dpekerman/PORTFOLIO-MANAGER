import { Routes } from '@angular/router';

export const TRANSACTION_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./transactions-page.component').then((m) => m.TransactionsPageComponent),
  },
];
