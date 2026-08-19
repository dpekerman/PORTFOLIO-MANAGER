import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer } from '@angular/core';
import { provideNativeDateAdapter } from '@angular/material/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { baseUrlInterceptor } from './core/interceptors/base-url.interceptor';
import { AuthStateService } from './core/services/auth-state.service';
import { SessionTimeoutService } from './core/services/session-timeout.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([baseUrlInterceptor, authInterceptor])),
    provideAnimationsAsync(),
    provideNativeDateAdapter(),
    provideAppInitializer(() => inject(AuthStateService).initializeAuth()),
    provideAppInitializer(() => void inject(SessionTimeoutService)),
  ],
};
