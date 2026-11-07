import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideNativeDateAdapter } from '@angular/material/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { baseUrlInterceptor } from './core/interceptors/base-url.interceptor';
import { AuthStore } from './core/store/auth.store';
import { SessionTimeoutService } from './core/services/session-timeout.service';

/**
 * Zoneless change detection is the Angular 21+ default (no zone.js polyfill is configured), so no
 * explicit zoneless provider is needed. Never add `provideZoneChangeDetection`: it would silently
 * re-enable zone-based change detection.
 */
export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withFetch(), withInterceptors([baseUrlInterceptor, authInterceptor])),
    provideNativeDateAdapter(),
    provideAppInitializer(() => inject(AuthStore).initializeAuth()),
    provideAppInitializer(() => void inject(SessionTimeoutService)),
  ],
};
