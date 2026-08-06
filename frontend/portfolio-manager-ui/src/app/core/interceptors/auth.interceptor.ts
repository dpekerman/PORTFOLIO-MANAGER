import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, Observable, shareReplay, switchMap, throwError } from 'rxjs';
import { AuthResponse } from '../models/portfolio.models';
import { AuthApiService } from '../services/auth-api.service';
import { AuthStateService } from '../services/auth-state.service';

const SKIP_AUTH_URLS = [
  '/api/auth/login',
  '/api/auth/setup',
  '/api/auth/refresh',
  '/api/auth/setup-required',
];

// Shared so concurrent 401s don't each trigger a separate refresh (token rotation would revoke the others)
let refreshInFlight: Observable<AuthResponse> | null = null;

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authState = inject(AuthStateService);
  const authApi = inject(AuthApiService);
  const router = inject(Router);

  const skipAuth = SKIP_AUTH_URLS.some((url) => req.url.includes(url));
  const token = authState.accessToken();

  const authReq =
    !skipAuth && token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status !== 401 || skipAuth) {
        return throwError(() => error);
      }

      if (!refreshInFlight) {
        refreshInFlight = authApi.refreshToken().pipe(shareReplay(1));
      }

      return refreshInFlight.pipe(
        switchMap((response) => {
          authState.setAuth(response);
          refreshInFlight = null;
          const retried = req.clone({
            setHeaders: { Authorization: `Bearer ${response.accessToken}` },
          });
          return next(retried);
        }),
        catchError(() => {
          refreshInFlight = null;
          authState.clearAuth();
          router.navigate(['/login']);
          return throwError(() => error);
        }),
      );
    }),
  );
};
