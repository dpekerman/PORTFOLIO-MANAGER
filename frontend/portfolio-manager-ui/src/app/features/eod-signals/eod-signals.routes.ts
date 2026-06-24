import { Routes } from '@angular/router';

export const EOD_SIGNALS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./eod-signals-page.component').then((m) => m.EodSignalsPageComponent),
  },
];
