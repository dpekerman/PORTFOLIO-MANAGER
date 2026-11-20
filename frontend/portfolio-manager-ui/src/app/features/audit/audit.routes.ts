import { Routes } from '@angular/router';

export const AUDIT_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./audit-page.component').then((m) => m.AuditPageComponent),
  },
  // The Signals Audit dashboard moved to the Signal Tracker page; keep old bookmarks working.
  { path: 'signals', redirectTo: '/signal-tracker', pathMatch: 'full' },
];
