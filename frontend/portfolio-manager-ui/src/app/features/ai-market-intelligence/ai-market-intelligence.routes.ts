import { Routes } from '@angular/router';

export const AI_MARKET_INTELLIGENCE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./ai-market-intelligence-page.component').then((m) => m.AiMarketIntelligencePageComponent),
  },
];
