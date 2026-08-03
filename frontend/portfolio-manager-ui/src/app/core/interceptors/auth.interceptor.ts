import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthApiService } from '../services/auth-api.service';
import { AuthStateService } from '../services/auth-state.service';

const SKIP_AUTH_URLS = [
  '/api/auth/login',
  '/api/auth/setup',
  '/api/auth/refresh',
  '/api/auth/setup-required',
];

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

      // One silent-refresh attempt, then give up
      return authApi.refreshToken().pipe(
        switchMap((response) => {
          authState.setAuth(response);
          const retried = req.clone({
            setHeaders: { Authorization: `Bearer ${response.accessToken}` },
          });
          return next(retried);
        }),
        catchError(() => {
          authState.clearAuth();
          router.navigate(['/login']);
          return throwError(() => error);
        }),
      );
    }),
  );
};
