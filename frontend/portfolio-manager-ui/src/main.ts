import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';

bootstrapApplication(App, appConfig)
  .catch((err) => console.error(err));

if ('serviceWorker' in navigator && window.isSecureContext) {
  navigator.serviceWorker.register('sw.js').catch((err) => console.warn('SW registration failed', err));
}
