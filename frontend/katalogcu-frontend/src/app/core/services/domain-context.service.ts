import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

type DomainEnvironment = typeof environment & {
  domains?: {
    panelSubdomain?: string;
    panelOrigin?: string;
    portalOrigin?: string;
    enforcePanelHost?: boolean;
  };
};

@Injectable({ providedIn: 'root' })
export class DomainContextService {
  private readonly domainConfig = (environment as DomainEnvironment).domains ?? {};
  private readonly panelSubdomain = this.domainConfig.panelSubdomain || 'panel';

  get isLocalHost(): boolean {
    const hostname = this.hostname;
    return hostname === 'localhost' || hostname === '127.0.0.1' || hostname === '::1';
  }

  get isPanelHost(): boolean {
    const hostname = this.hostname;
    return hostname === `${this.panelSubdomain}.localhost`
      || hostname.startsWith(`${this.panelSubdomain}.`);
  }

  get enforcesPanelHost(): boolean {
    return this.domainConfig.enforcePanelHost ?? environment.production;
  }

  shouldRedirectPanelRoute(): boolean {
    return this.enforcesPanelHost && !this.isLocalHost && !this.isPanelHost;
  }

  shouldRedirectPortalRoute(): boolean {
    return this.enforcesPanelHost && !this.isLocalHost && this.isPanelHost;
  }

  panelUrl(path = '/login'): string {
    return this.joinOriginAndPath(this.panelOrigin, path);
  }

  portalUrl(path = '/'): string {
    return this.joinOriginAndPath(this.portalOrigin, path);
  }

  redirectToPanel(path = '/login') {
    this.assign(this.panelUrl(path));
  }

  redirectToPortal(path = '/') {
    this.assign(this.portalUrl(path));
  }

  private get panelOrigin(): string {
    if (this.domainConfig.panelOrigin) return this.trimTrailingSlash(this.domainConfig.panelOrigin);
    if (this.isLocalHost || this.isPanelHost) return this.origin;

    return `${this.protocol}//${this.panelSubdomain}.${this.host}`;
  }

  private get portalOrigin(): string {
    if (this.domainConfig.portalOrigin) return this.trimTrailingSlash(this.domainConfig.portalOrigin);
    if (this.isLocalHost || !this.isPanelHost) return this.origin;

    return `${this.protocol}//${this.host.replace(`${this.panelSubdomain}.`, '')}`;
  }

  private get location(): Location | null {
    return typeof window === 'undefined' ? null : window.location;
  }

  private get protocol(): string {
    return this.location?.protocol ?? 'https:';
  }

  private get host(): string {
    return this.location?.host ?? '';
  }

  private get hostname(): string {
    return (this.location?.hostname ?? '').toLowerCase();
  }

  private get origin(): string {
    return this.location?.origin ?? '';
  }

  private joinOriginAndPath(origin: string, path: string): string {
    const normalizedOrigin = this.trimTrailingSlash(origin || this.origin);
    const normalizedPath = path.startsWith('/') ? path : `/${path}`;
    return `${normalizedOrigin}${normalizedPath}`;
  }

  private trimTrailingSlash(value: string): string {
    return value.replace(/\/+$/, '');
  }

  private assign(url: string) {
    if (typeof window !== 'undefined') {
      window.location.assign(url);
    }
  }
}
