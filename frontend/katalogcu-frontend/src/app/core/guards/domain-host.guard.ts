import { inject } from '@angular/core';
import { CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { DomainContextService } from '../services/domain-context.service';

export const panelHostGuard: CanActivateFn = (_route, state) => {
  const domainContext = inject(DomainContextService);

  if (!domainContext.shouldRedirectPanelRoute()) return true;

  domainContext.redirectToPanel(state.url || '/login');
  return false;
};

export const portalHostGuard: CanActivateFn = (_route, state) => {
  const domainContext = inject(DomainContextService);

  if (!domainContext.shouldRedirectPortalRoute()) return true;

  domainContext.redirectToPortal(state.url || '/');
  return false;
};

export const panelHomeGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const domainContext = inject(DomainContextService);

  if (!domainContext.isPanelHost || domainContext.isLocalHost) return true;

  domainContext.redirectToPanel(authService.isLoggedIn() ? '/dashboard' : '/login');
  return false;
};
