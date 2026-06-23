import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { environment } from '../../../environments/environment';

export const planSelectionGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isLoggedIn()) {
    return router.parseUrl('/login');
  }

  if (!environment.features.enableUpgradePrompts) {
    return true;
  }

  if (authService.isPlanSelected()) {
    return true;
  }

  return router.parseUrl('/upgrade');
};
