import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStore } from '../store/auth.store';

export const authGuard: CanActivateFn = () => {
  const authState = inject(AuthStore);
  const router = inject(Router);

  if (authState.setupRequired() === true) {
    return router.createUrlTree(['/setup']);
  }

  if (!authState.isAuthenticated()) {
    return router.createUrlTree(['/login']);
  }

  return true;
};
