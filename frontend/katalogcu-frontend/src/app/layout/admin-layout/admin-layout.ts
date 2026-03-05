import { Component, HostListener, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService, AuthUserInfo } from '../../core/services/auth.service';
import { CatalogService, PublicTokenStatus } from '../../core/services/catalog.service';
import { OrderService } from '../../core/services/order.service';
import { Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './admin-layout.html',
  styleUrl: './admin-layout.css'
})
export class AdminLayoutComponent implements OnDestroy {
  private authService = inject(AuthService);
  private catalogService = inject(CatalogService);
  private orderService = inject(OrderService);
  private router = inject(Router);

  private pollHandle: ReturnType<typeof setInterval> | null = null;
  private routeSub?: Subscription;
  private readonly orderSeenAtKey = 'admin_orders_seen_at';

  isSidebarOpen = true;
  publicToken: string | null = null;
  publicTokenStatus: PublicTokenStatus | null = null;
  publicActionLoading = false;
  publicActionMessage: string | null = null;
  publicActionError: string | null = null;
  pendingOrderCount = 0;
  unreadOrderCount = 0;
  currentUser: AuthUserInfo | null = null;
  openLockedMenuKey: string | null = null;

  constructor() {
    this.loadCurrentUser();
    this.loadPublicLinkState();
    this.initializeOrderNotifications();
  }

  get userDisplayName(): string {
    const first = this.currentUser?.firstName?.trim() || '';
    const last = this.currentUser?.lastName?.trim() || '';
    const fullName = `${first} ${last}`.trim();
    if (fullName) return fullName;
    return this.currentUser?.email || 'Kullanıcı';
  }

  get userRoleLabel(): string {
    const role = this.currentUser?.role?.toLowerCase();
    if (role === 'admin' || role === 'owner') return 'Firma Sahibi';
    if (role === 'customer') return 'Kullanıcı';
    return this.currentUser?.role || 'Kullanıcı';
  }

  get userAvatarUrl(): string {
    const encoded = encodeURIComponent(this.userDisplayName);
    return `https://ui-avatars.com/api/?name=${encoded}&background=0F172A&color=ffffff`;
  }

  get currentPlan(): number {
    return this.authService.getCurrentPlan();
  }

  get canUseAi(): boolean {
    return this.supportsAiFeature && this.currentPlan >= 2;
  }

  get canUseEcommerce(): boolean {
    return this.supportsEcommerceFeature && this.currentPlan >= 3;
  }

  hasPlan(minPlan: number): boolean {
    if (minPlan >= 2 && !this.supportsAiFeature) return false;
    if (minPlan >= 3 && !this.supportsEcommerceFeature) return false;
    return this.currentPlan >= minPlan;
  }

  get supportsAiFeature(): boolean {
    return environment.features.enableAi;
  }

  get supportsEcommerceFeature(): boolean {
    return environment.features.enableEcommerce;
  }

  get showUpgradePrompts(): boolean {
    return environment.features.enableUpgradePrompts;
  }

  get shouldShowHeaderUpgradeButton(): boolean {
    if (!this.showUpgradePrompts) return false;
    if (this.supportsAiFeature && this.currentPlan < 2) return true;
    if (this.supportsEcommerceFeature && this.currentPlan < 3) return true;
    return false;
  }

  get shouldShowSidebarUpgradePromo(): boolean {
    return this.shouldShowHeaderUpgradeButton;
  }

  get nextUpgradePlan(): number {
    if (this.supportsAiFeature && this.currentPlan < 2) return 2;
    if (this.supportsEcommerceFeature && this.currentPlan < 3) return 3;
    return this.currentPlan;
  }

  get nextUpgradeFeature(): string {
    if (this.supportsAiFeature && this.currentPlan < 2) return 'ai';
    if (this.supportsEcommerceFeature && this.currentPlan < 3) return 'ecommerce';
    return 'catalog';
  }

  goUpgrade(requiredPlan?: number, feature?: string) {
    this.openLockedMenuKey = null;
    this.router.navigate(['/upgrade'], {
      queryParams: {
        ...(requiredPlan ? { requiredPlan } : {}),
        ...(feature ? { feature } : {})
      }
    });
  }

  get currentPlanLabel(): string {
    return this.authService.getCurrentPlanDisplayName();
  }

  private loadCurrentUser() {
    const cached = this.authService.getStoredUserInfo();
    if (cached) this.currentUser = cached;

    this.authService.getMe().subscribe({
      next: (me) => {
        this.currentUser = me;
        this.authService.setStoredUserInfo(me);
        const session = this.authService.getSession();
        const token = this.authService.getToken();
        if (session && token) {
          this.authService.setSession({
            ...session,
            token,
            userId: me.userId || me.id || session.userId,
            plan: this.authService.getCurrentPlan(),
            planName: this.authService.getCurrentPlanDisplayName(),
            planSelected: !!me.planSelected,
            maxCatalogs: me.maxCatalogCount ?? session.maxCatalogs,
            expiresAt: me.planExpiresAt ?? session.expiresAt
          });
        }
      },
      error: () => {
        // cache varsa onunla devam
      }
    });
  }

  logout() {
    this.authService.logout();
  }

  toggleLockedMenu(key: string, event: MouseEvent) {
    event.stopPropagation();
    this.openLockedMenuKey = this.openLockedMenuKey === key ? null : key;
  }

  closeLockedMenu() {
    this.openLockedMenuKey = null;
  }

  goToLinkManagement() {
    this.router.navigate(['/dashboard/settings'], {
      queryParams: {
        tab: 'public',
        section: 'link-management'
      }
    });
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: Event) {
    const target = event.target as HTMLElement | null;
    if (!target?.closest('.nav-item-menu-wrap')) {
      this.closeLockedMenu();
    }
  }

  loadPublicLinkState() {
    this.publicActionError = null;
    this.catalogService.getPublicTokenStatus().subscribe({
      next: (status) => {
        this.publicTokenStatus = status;
        if (!status.enabled) {
          this.publicToken = null;
          return;
        }
        this.catalogService.getPublicToken().subscribe({
          next: (res) => { this.publicToken = res.token; },
          error: () => {
            this.publicToken = null;
            this.publicActionError = 'Public link alınamadı.';
          }
        });
      },
      error: () => {
        this.publicTokenStatus = null;
        this.publicToken = null;
        this.publicActionError = 'Public link durumu okunamadı.';
      }
    });
  }

  rotatePublicLink() {
    if (this.publicActionLoading) return;
    this.publicActionLoading = true;
    this.publicActionMessage = null;
    this.publicActionError = null;
    this.catalogService.rotatePublicToken().subscribe({
      next: (res) => {
        this.publicToken = res.token;
        this.publicTokenStatus = { enabled: res.enabled, version: res.version };
        this.publicActionMessage = 'Public link yenilendi. Eski linkler iptal edildi.';
        this.publicActionLoading = false;
      },
      error: () => {
        this.publicActionError = 'Public link yenilenemedi.';
        this.publicActionLoading = false;
      }
    });
  }

  revokePublicLink() {
    if (this.publicActionLoading) return;
    this.publicActionLoading = true;
    this.publicActionMessage = null;
    this.publicActionError = null;
    this.catalogService.revokePublicToken().subscribe({
      next: (res) => {
        this.publicToken = null;
        this.publicTokenStatus = res;
        this.publicActionMessage = 'Public link iptal edildi.';
        this.publicActionLoading = false;
      },
      error: () => {
        this.publicActionError = 'Public link iptal edilemedi.';
        this.publicActionLoading = false;
      }
    });
  }

  async copyPublicLink() {
    if (!this.publicToken) return;
    const url = `${window.location.origin}/p/${this.publicToken}`;
    try {
      await navigator.clipboard.writeText(url);
      this.publicActionMessage = 'Public link panoya kopyalandı.';
      this.publicActionError = null;
    } catch {
      this.publicActionError = 'Link kopyalanamadı.';
      this.publicActionMessage = null;
    }
  }

  private initializeOrderNotifications() {
    if (!this.supportsEcommerceFeature) return;

    this.routeSub = this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe((event) => {
        if (event.urlAfterRedirects.startsWith('/dashboard/orders')) {
          this.markOrdersAsSeen();
        }
      });

    this.refreshOrderNotifications();
    this.pollHandle = setInterval(() => {
      this.refreshOrderNotifications();
    }, 30000);
  }

  private getSeenAt(): number {
    const raw = localStorage.getItem(this.orderSeenAtKey);
    const parsed = raw ? Number(raw) : 0;
    return Number.isFinite(parsed) ? parsed : 0;
  }

  private markOrdersAsSeen() {
    const now = Date.now();
    localStorage.setItem(this.orderSeenAtKey, String(now));
    this.unreadOrderCount = 0;
  }

  private refreshOrderNotifications() {
    this.orderService.getIncomingOrders().subscribe({
      next: (orders) => {
        const list = orders || [];
        this.pendingOrderCount = list.filter(o => o.status === 0).length;

        if (this.router.url.startsWith('/dashboard/orders')) {
          this.markOrdersAsSeen();
          return;
        }

        const seenAt = this.getSeenAt();
        this.unreadOrderCount = list.filter(o => {
          const createdAt = new Date(o.createdDate).getTime();
          return Number.isFinite(createdAt) && createdAt > seenAt;
        }).length;
      },
      error: () => {
        this.pendingOrderCount = 0;
        this.unreadOrderCount = 0;
      }
    });
  }

  openOrdersFromNotification() {
    if (!this.supportsEcommerceFeature) return;
    this.markOrdersAsSeen();
    this.router.navigate(['/dashboard/orders']);
  }

  ngOnDestroy(): void {
    if (this.pollHandle) {
      clearInterval(this.pollHandle);
      this.pollHandle = null;
    }
    this.routeSub?.unsubscribe();
  }
}
