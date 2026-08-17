import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStateService } from '../services/auth-state.service';

export const authGuard: CanActivateFn = () => {
  const authState = inject(AuthStateService);
  const router = inject(Router);

  if (authState.setupRequired() === true) {
    return router.createUrlTree(['/setup']);
  }

  if (!authState.isAuthenticated()) {
    return router.createUrlTree(['/login']);
  }

  return true;
};
