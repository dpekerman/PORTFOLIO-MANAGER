import { HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { environment } from '../../../environments/environment';

// Prepends the production API base URL to all relative /api calls.
// In development the base URL is empty so requests pass through unchanged to the proxy.
export const baseUrlInterceptor: HttpInterceptorFn = (req, next) => {
  if (!environment.apiBaseUrl || !req.url.startsWith('/api')) {
    return next(req);
  }
  const apiReq: HttpRequest<unknown> = req.clone({
    url: `${environment.apiBaseUrl}${req.url}`,
  });
  return next(apiReq);
};
