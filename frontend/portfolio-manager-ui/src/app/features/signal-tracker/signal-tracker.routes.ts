import { Routes } from '@angular/router';

export const SIGNAL_TRACKER_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./signal-tracker-page.component').then((m) => m.SignalTrackerPageComponent),
  },
];
