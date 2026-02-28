import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const planGuard: CanActivateFn = (route) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const minPlan = Number(route.data?.['minPlan'] ?? 1);
  const currentPlan = authService.getCurrentPlan();

  if (currentPlan >= minPlan) {
    return true;
  }

  return router.parseUrl('/upgrade');
};
