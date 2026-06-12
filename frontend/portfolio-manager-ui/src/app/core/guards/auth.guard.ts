import { CanActivateFn } from '@angular/router';

/**
 * Auth guard — placeholder for future authentication & authorization.
 * Once auth is implemented, inject AuthService here and check if the
 * user is authenticated / has the required role before granting access.
 */
export const authGuard: CanActivateFn = () => {
  // TODO: implement when authentication is added
  // const authService = inject(AuthService);
  // const router = inject(Router);
  // if (!authService.isAuthenticated()) {
  //   return router.createUrlTree(['/login']);
  // }
  return true;
};
