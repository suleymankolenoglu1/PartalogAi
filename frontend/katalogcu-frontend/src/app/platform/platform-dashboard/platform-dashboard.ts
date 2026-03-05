import { Component, HostListener, OnInit, inject } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { forkJoin } from 'rxjs';
import { PlatformAdminService, PlatformMetricsResponse, PlatformTenant } from '../../core/services/platform-admin.service';
import { ActionReasonModalComponent } from '../shared/action-reason-modal/action-reason-modal';

@Component({
  selector: 'app-platform-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePipe, ActionReasonModalComponent],
  templateUrl: './platform-dashboard.html',
  styleUrl: './platform-dashboard.css'
})
export class PlatformDashboardComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly platformService = inject(PlatformAdminService);
  private readonly router = inject(Router);

  isLoading = false;
  errorMessage = '';
  query = '';
  selectedPlan: number | null = null;
  selectedStatus: 'all' | 'active' | 'suspended' = 'all';
  selectedExpiry: 'all' | 'warning' | 'expired' = 'all';
  selectedUsageRisk: 'all' | 'warning' | 'critical' = 'all';
  total = 0;
  allTenants: PlatformTenant[] = [];
  tenants: PlatformTenant[] = [];
  metrics: PlatformMetricsResponse['totals'] = {
    tenants: 0,
    activeTenants: 0,
    suspendedTenants: 0,
    catalogs: 0,
    parts: 0,
    orders: 0,
    aiJobs: 0
  };
  planDistribution: PlatformMetricsResponse['plans'] = [];
  actionMessage = '';
  actionError = '';
  pendingOwnerId: string | null = null;
  bulkPending = false;
  bulkExtendMenuOpen = false;
  openTenantExtendMenuOwnerId: string | null = null;
  activeQuickFilter: 'all' | 'active' | 'suspended' | 'plan-1' | 'plan-2' | 'plan-3' | 'expiry-warning' | 'expiry-expired' | 'usage-warning' | 'usage-critical' = 'all';
  reasonModalOpen = false;
  reasonModalTitle = '';
  reasonModalDescription = '';
  reasonModalRequired = false;
  reasonModalConfirmText = 'Onayla';
  reasonModalPlaceholder = 'İşlem notu yazın';
  reasonModalTemplates: string[] = [];
  reasonModalPending = false;
  private reasonModalAction: 'suspend' | 'unsuspend' | 'bulk-extend' | 'bulk-unlimited' | 'plan-change' | 'single-extend' | 'single-unlimited' | null = null;
  private reasonModalOwnerId: string | null = null;
  private reasonModalDays: number | null = null;
  private reasonModalPlan: number | null = null;

  ngOnInit(): void {
    this.loadDashboard();
  }

  @HostListener('document:click')
  onDocumentClick() {
    this.closeExtendMenus();
  }

  loadDashboard() {
    this.isLoading = true;
    this.errorMessage = '';
    forkJoin({
      tenants: this.platformService.getTenants(this.query, this.selectedPlan, this.selectedStatus),
      metrics: this.platformService.getMetrics()
    }).subscribe({
      next: ({ tenants, metrics }) => {
        this.total = tenants.total;
        this.allTenants = tenants.items;
        this.applyClientFilters();
        this.metrics = metrics.totals;
        this.planDistribution = metrics.plans;
        this.isLoading = false;
      },
      error: (err) => {
        this.isLoading = false;
        this.errorMessage = err?.error?.message ?? 'İşletme listesi alınamadı.';
      }
    });
  }

  onSearch() {
    this.loadDashboard();
  }

  clearSearch() {
    this.query = '';
    this.selectedPlan = null;
    this.selectedStatus = 'all';
    this.selectedExpiry = 'all';
    this.selectedUsageRisk = 'all';
    this.activeQuickFilter = 'all';
    this.loadDashboard();
  }

  applyAllQuickFilter() {
    this.selectedPlan = null;
    this.selectedStatus = 'all';
    this.selectedExpiry = 'all';
    this.selectedUsageRisk = 'all';
    this.activeQuickFilter = 'all';
    this.loadDashboard();
  }

  onFilterChange() {
    this.selectedExpiry = 'all';
    this.selectedUsageRisk = 'all';
    this.activeQuickFilter = 'all';
    this.loadDashboard();
  }

  applyStatusQuickFilter(status: 'active' | 'suspended') {
    this.selectedStatus = status;
    this.selectedPlan = null;
    this.selectedExpiry = 'all';
    this.selectedUsageRisk = 'all';
    this.activeQuickFilter = status;
    this.loadDashboard();
  }

  applyPlanQuickFilter(plan: 1 | 2 | 3) {
    this.selectedPlan = plan;
    this.selectedStatus = 'all';
    this.selectedExpiry = 'all';
    this.selectedUsageRisk = 'all';
    this.activeQuickFilter = `plan-${plan}` as const;
    this.loadDashboard();
  }

  applyExpiryQuickFilter(filter: 'warning' | 'expired') {
    this.selectedExpiry = filter;
    this.selectedUsageRisk = 'all';
    this.activeQuickFilter = `expiry-${filter}` as const;
    this.applyClientFilters();
  }

  applyUsageQuickFilter(filter: 'warning' | 'critical') {
    this.selectedUsageRisk = filter;
    this.selectedExpiry = 'all';
    this.activeQuickFilter = `usage-${filter}` as const;
    this.applyClientFilters();
  }

  isQuickFilterActive(target: 'active' | 'suspended' | 'plan-1' | 'plan-2' | 'plan-3' | 'expiry-warning' | 'expiry-expired' | 'usage-warning' | 'usage-critical') {
    return this.activeQuickFilter === target;
  }

  isQuickFilterActiveDynamic(target: string) {
    return this.activeQuickFilter === target;
  }

  get expiringSoonCount(): number {
    return this.allTenants.filter(t => this.getExpiryState(t.planExpiresAt) === 'warning').length;
  }

  get expiredCount(): number {
    return this.allTenants.filter(t => this.getExpiryState(t.planExpiresAt) === 'expired').length;
  }

  get usageWarningCount(): number {
    return this.allTenants.filter(t => this.getUsageRisk(t) === 'warning').length;
  }

  get usageCriticalCount(): number {
    return this.allTenants.filter(t => this.getUsageRisk(t) === 'critical').length;
  }

  get recommendation(): { tone: 'info' | 'warning' | 'critical'; message: string; actionText: string } | null {
    if (!this.tenants.length) return null;

    if (this.activeQuickFilter === 'expiry-warning') {
      return {
        tone: 'warning',
        message: `Bu listedeki ${this.tenants.length} işletmenin planı 7 gün içinde bitecek.`,
        actionText: 'Toplu +30g Aç'
      };
    }

    if (this.activeQuickFilter === 'expiry-expired') {
      return {
        tone: 'critical',
        message: `Bu listedeki ${this.tenants.length} işletmenin plan süresi dolmuş.`,
        actionText: 'Toplu +90g Aç'
      };
    }

    if (this.activeQuickFilter === 'usage-warning') {
      return {
        tone: 'warning',
        message: `Bu listedeki işletmeler katalog limitinin %80 üstünde.`,
        actionText: 'Toplu +30g Aç'
      };
    }

    if (this.activeQuickFilter === 'usage-critical') {
      return {
        tone: 'critical',
        message: `Bu listedeki işletmeler katalog limitini doldurmuş/aşmış.`,
        actionText: 'Toplu +90g Aç'
      };
    }

    return null;
  }

  updatePlan(ownerId: string, plan: number) {
    if (this.bulkPending || this.pendingOwnerId !== null) return;
    this.openReasonModal(
      'plan-change',
      ownerId,
      'Plan Güncelleme',
      `Yeni plan: ${this.planLabel(plan)}. İşlem notu opsiyoneldir.`,
      false,
      'Planı Uygula',
      null,
      plan
    );
  }

  extendPlan30Days(tenant: PlatformTenant) {
    this.extendPlanByDays(tenant, 30);
  }

  extendPlan90Days(tenant: PlatformTenant) {
    this.extendPlanByDays(tenant, 90);
  }

  makePlanUnlimited(tenant: PlatformTenant) {
    if (this.bulkPending || this.pendingOwnerId !== null) return;
    this.closeExtendMenus();
    this.openReasonModal(
      'single-unlimited',
      tenant.ownerId,
      'Tekil Süresiz Yap',
      `${tenant.companyName || tenant.ownerFullName} için paket süresi süresiz olacak. İşlem notu zorunlu.`,
      true,
      'Süresiz Yap',
      null,
      tenant.plan
    );
  }

  extendVisibleTenants30Days() {
    this.extendVisibleTenantsByDays(30);
  }

  extendVisibleTenants90Days() {
    this.extendVisibleTenantsByDays(90);
  }

  makeVisibleTenantsUnlimited() {
    if (!this.tenants.length || this.bulkPending || this.pendingOwnerId !== null) return;
    this.closeExtendMenus();
    this.openReasonModal(
      'bulk-unlimited',
      null,
      'Toplu Plan Güncelleme',
      `${this.tenants.length} işletmenin planı süresiz yapılacak. İşlem notu zorunlu.`,
      true,
      'Toplu Süresiz Yap'
    );
  }

  suspend(ownerId: string) {
    if (this.bulkPending || this.pendingOwnerId !== null) return;
    this.openReasonModal(
      'suspend',
      ownerId,
      'İşletmeyi Askıya Al',
      'Bu işlem kullanıcı erişimini keser. İşlem notu zorunludur.',
      true,
      'Askıya Al'
    );
  }

  unsuspend(ownerId: string) {
    if (this.bulkPending || this.pendingOwnerId !== null) return;
    this.openReasonModal(
      'unsuspend',
      ownerId,
      'İşletmeyi Aktifleştir',
      'Bu işlem kullanıcı erişimini tekrar açar. Not opsiyoneldir.',
      false,
      'Aktifleştir'
    );
  }

  onMenuContainerClick(event: Event) {
    event.stopPropagation();
  }

  toggleBulkExtendMenu(event: Event) {
    event.stopPropagation();
    if (this.bulkPending || this.pendingOwnerId !== null || !this.tenants.length) return;
    const next = !this.bulkExtendMenuOpen;
    this.closeExtendMenus();
    this.bulkExtendMenuOpen = next;
  }

  toggleTenantExtendMenu(ownerId: string, event: Event) {
    event.stopPropagation();
    if (this.bulkPending || this.pendingOwnerId !== null) return;
    const next = this.openTenantExtendMenuOwnerId !== ownerId;
    this.closeExtendMenus();
    this.openTenantExtendMenuOwnerId = next ? ownerId : null;
  }

  selectBulkExtend30() {
    this.closeExtendMenus();
    this.extendVisibleTenants30Days();
  }

  selectBulkExtend90() {
    this.closeExtendMenus();
    this.extendVisibleTenants90Days();
  }

  selectBulkUnlimited() {
    this.closeExtendMenus();
    this.makeVisibleTenantsUnlimited();
  }

  selectTenantExtend30(tenant: PlatformTenant) {
    this.closeExtendMenus();
    this.extendPlan30Days(tenant);
  }

  selectTenantExtend90(tenant: PlatformTenant) {
    this.closeExtendMenus();
    this.extendPlan90Days(tenant);
  }

  selectTenantUnlimited(tenant: PlatformTenant) {
    this.closeExtendMenus();
    this.makePlanUnlimited(tenant);
  }

  selectTenantSuspend(ownerId: string) {
    this.closeExtendMenus();
    this.suspend(ownerId);
  }

  selectTenantUnsuspend(ownerId: string) {
    this.closeExtendMenus();
    this.unsuspend(ownerId);
  }

  goTenantDetail(ownerId: string) {
    this.closeExtendMenus();
    this.router.navigate(['/platform/tenants', ownerId]);
  }

  logout() {
    this.authService.logout();
  }

  getExpiryState(expiryIso: string | null | undefined): 'none' | 'ok' | 'warning' | 'expired' {
    if (!expiryIso) return 'none';

    const expiry = new Date(expiryIso);
    if (Number.isNaN(expiry.getTime())) return 'none';

    const now = new Date();
    const msDiff = expiry.getTime() - now.getTime();
    const dayDiff = Math.ceil(msDiff / (1000 * 60 * 60 * 24));

    if (dayDiff < 0) return 'expired';
    if (dayDiff <= 7) return 'warning';
    return 'ok';
  }

  getExpiryLabel(expiryIso: string | null | undefined): string {
    const state = this.getExpiryState(expiryIso);
    if (state === 'none') return 'Süresiz';

    const expiry = new Date(expiryIso!);
    if (Number.isNaN(expiry.getTime())) return 'Süresiz';

    const now = new Date();
    const msDiff = expiry.getTime() - now.getTime();
    const dayDiff = Math.ceil(msDiff / (1000 * 60 * 60 * 24));

    if (dayDiff < 0) return 'Süresi doldu';
    if (dayDiff === 0) return 'Bugün bitiyor';
    return `${dayDiff} gün kaldı`;
  }

  getUsageRisk(tenant: PlatformTenant): 'none' | 'warning' | 'critical' {
    const max = tenant.limits.maxCatalogCount;
    const current = tenant.usage.catalogCount;
    if (!max || max <= 0 || max >= 2147483647) return 'none';
    const ratio = current / max;
    if (ratio >= 1) return 'critical';
    if (ratio >= 0.8) return 'warning';
    return 'none';
  }

  getUsageRiskLabel(tenant: PlatformTenant): string {
    const max = tenant.limits.maxCatalogCount;
    const current = tenant.usage.catalogCount;
    if (!max || max <= 0 || max >= 2147483647) return 'Limitsiz';
    const ratio = Math.round((current / max) * 100);
    if (ratio >= 100) return `Limit aşıldı (%${ratio})`;
    return `%${ratio} dolu`;
  }

  runRecommendationAction() {
    if (!this.tenants.length || this.bulkPending || this.pendingOwnerId !== null) return;

    if (this.activeQuickFilter === 'expiry-warning' || this.activeQuickFilter === 'usage-warning') {
      this.extendVisibleTenants30Days();
      return;
    }

    if (this.activeQuickFilter === 'expiry-expired' || this.activeQuickFilter === 'usage-critical') {
      this.extendVisibleTenants90Days();
    }
  }

  exportVisibleTenantsCsv() {
    if (!this.tenants.length) {
      this.actionError = 'Dışa aktarılacak işletme yok.';
      return;
    }

    const headers = [
      'ownerId',
      'companyName',
      'ownerFullName',
      'ownerEmail',
      'phoneNumber',
      'status',
      'planName',
      'planExpiresAt',
      'catalogCount',
      'maxCatalogCount',
      'usageRisk'
    ];

    const rows = this.tenants.map((tenant) => [
      tenant.ownerId,
      tenant.companyName ?? '',
      tenant.ownerFullName,
      tenant.ownerEmail,
      tenant.phoneNumber ?? '',
      tenant.isSuspended ? 'Suspended' : 'Active',
      tenant.planName,
      tenant.planExpiresAt ?? '',
      String(tenant.usage.catalogCount),
      String(tenant.limits.maxCatalogCount),
      this.getUsageRisk(tenant)
    ]);

    const csv = [
      headers.map((h) => this.escapeCsv(h)).join(','),
      ...rows.map((row) => row.map((cell) => this.escapeCsv(cell)).join(','))
    ].join('\n');

    const blob = new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    const stamp = new Date().toISOString().slice(0, 19).replace(/[:T]/g, '-');
    link.href = url;
    link.download = `platform-tenants-${stamp}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }

  private applyClientFilters() {
    let list = this.allTenants;

    if (this.selectedExpiry !== 'all') {
      list = list.filter(t => this.getExpiryState(t.planExpiresAt) === this.selectedExpiry);
    }

    if (this.selectedUsageRisk !== 'all') {
      list = list.filter(t => this.getUsageRisk(t) === this.selectedUsageRisk);
    }

    this.tenants = list;
  }

  private extendPlanByDays(tenant: PlatformTenant, daysToAdd: number) {
    if (this.bulkPending || this.pendingOwnerId !== null) return;
    this.closeExtendMenus();
    this.openReasonModal(
      'single-extend',
      tenant.ownerId,
      'Tekil Süre Uzatma',
      `${tenant.companyName || tenant.ownerFullName} için paket süresi +${daysToAdd} gün uzatılacak.`,
      false,
      `+${daysToAdd} Gün Uygula`,
      daysToAdd,
      tenant.plan
    );
  }

  private extendVisibleTenantsByDays(daysToAdd: number) {
    if (!this.tenants.length || this.bulkPending || this.pendingOwnerId !== null) return;
    this.closeExtendMenus();
    this.openReasonModal(
      'bulk-extend',
      null,
      'Toplu Plan Süresi Uzatma',
      `${this.tenants.length} işletmenin plan süresi +${daysToAdd} gün uzatılacak. İşlem notu zorunlu.`,
      true,
      `+${daysToAdd} Gün Uygula`,
      daysToAdd
    );
  }

  private buildExtendedExpiryIso(currentIso: string | null, daysToAdd: number): string {
    const now = new Date();
    const current = currentIso ? new Date(currentIso) : null;
    const base = current && !Number.isNaN(current.getTime()) && current > now ? current : now;
    const next = new Date(base.getTime() + daysToAdd * 24 * 60 * 60 * 1000);
    return next.toISOString();
  }

  onReasonModalClosed() {
    if (this.reasonModalPending) return;
    this.resetReasonModal();
  }

  onReasonModalConfirmed(reason: string | null) {
    const ownerId = this.reasonModalOwnerId;
    const action = this.reasonModalAction;
    if (!action) {
      this.resetReasonModal();
      return;
    }

    this.actionMessage = '';
    this.actionError = '';
    this.reasonModalPending = true;
    this.pendingOwnerId = ownerId;

    if (action === 'plan-change' && ownerId && this.reasonModalPlan) {
      const operationId = this.newOperationId();
      this.platformService.updateTenantPlan(ownerId, {
        plan: this.reasonModalPlan,
        reason: reason ?? `Dashboard plan değişikliği: ${this.planLabel(this.reasonModalPlan)}`,
        operationId
      }).subscribe({
        next: () => {
          this.pendingOwnerId = null;
          this.reasonModalPending = false;
          this.actionMessage = 'Plan güncellendi.';
          this.resetReasonModal();
          this.loadDashboard();
        },
        error: (err) => {
          this.pendingOwnerId = null;
          this.reasonModalPending = false;
          this.actionError = err?.error?.message ?? 'Plan güncellenemedi.';
        }
      });
      return;
    }

    if (action === 'single-extend' && ownerId && this.reasonModalDays && this.reasonModalPlan) {
      const daysToAdd = this.reasonModalDays;
      const tenant = this.allTenants.find(t => t.ownerId === ownerId);
      const expiresAt = this.buildExtendedExpiryIso(tenant?.planExpiresAt ?? null, daysToAdd);
      const operationId = this.newOperationId();
      this.platformService.updateTenantPlan(ownerId, {
        plan: this.reasonModalPlan,
        expiresAt,
        reason: reason ?? `Dashboard kart aksiyonu: +${daysToAdd} gün`,
        operationId
      }).subscribe({
        next: () => {
          this.pendingOwnerId = null;
          this.reasonModalPending = false;
          this.actionMessage = `Paket süresi ${daysToAdd} gün uzatıldı.`;
          this.resetReasonModal();
          this.loadDashboard();
        },
        error: (err) => {
          this.pendingOwnerId = null;
          this.reasonModalPending = false;
          this.actionError = err?.error?.message ?? 'Süre uzatılamadı.';
        }
      });
      return;
    }

    if (action === 'single-unlimited' && ownerId && this.reasonModalPlan) {
      const operationId = this.newOperationId();
      this.platformService.updateTenantPlan(ownerId, {
        plan: this.reasonModalPlan,
        expiresAt: null,
        reason: reason ?? 'Dashboard kart aksiyonu: süresiz',
        operationId
      }).subscribe({
        next: () => {
          this.pendingOwnerId = null;
          this.reasonModalPending = false;
          this.actionMessage = 'Paket süresi süresiz yapıldı.';
          this.resetReasonModal();
          this.loadDashboard();
        },
        error: (err) => {
          this.pendingOwnerId = null;
          this.reasonModalPending = false;
          this.actionError = err?.error?.message ?? 'Süre güncellenemedi.';
        }
      });
      return;
    }

    if (action === 'bulk-extend' && this.reasonModalDays) {
      this.bulkPending = true;
      this.reasonModalPending = true;
      const daysToAdd = this.reasonModalDays;
      const operationId = this.newOperationId();
      const requests = this.tenants.map((tenant) =>
        this.platformService.updateTenantPlan(tenant.ownerId, {
          plan: tenant.plan,
          expiresAt: this.buildExtendedExpiryIso(tenant.planExpiresAt, daysToAdd),
          reason: reason ?? `Toplu aksiyon: +${daysToAdd} gün`,
          operationId
        })
      );

      forkJoin(requests).subscribe({
        next: () => {
          this.bulkPending = false;
          this.reasonModalPending = false;
          this.actionMessage = `${this.tenants.length} işletmenin süresi ${daysToAdd} gün uzatıldı.`;
          this.resetReasonModal();
          this.loadDashboard();
        },
        error: (err) => {
          this.bulkPending = false;
          this.reasonModalPending = false;
          this.actionError = err?.error?.message ?? 'Toplu süre uzatma başarısız.';
        }
      });
      return;
    }

    if (action === 'bulk-unlimited') {
      this.bulkPending = true;
      this.reasonModalPending = true;
      const operationId = this.newOperationId();
      const requests = this.tenants.map((tenant) =>
        this.platformService.updateTenantPlan(tenant.ownerId, {
          plan: tenant.plan,
          expiresAt: null,
          reason: reason ?? 'Toplu aksiyon: süresiz',
          operationId
        })
      );

      forkJoin(requests).subscribe({
        next: () => {
          this.bulkPending = false;
          this.reasonModalPending = false;
          this.actionMessage = `${this.tenants.length} işletmenin planı süresiz yapıldı.`;
          this.resetReasonModal();
          this.loadDashboard();
        },
        error: (err) => {
          this.bulkPending = false;
          this.reasonModalPending = false;
          this.actionError = err?.error?.message ?? 'Toplu plan güncelleme başarısız.';
        }
      });
      return;
    }

    if (ownerId) {
      this.reasonModalPending = true;
      this.pendingOwnerId = ownerId;
      const operationId = this.newOperationId();
      const request = action === 'suspend'
        ? this.platformService.suspendTenant(ownerId, reason, operationId)
        : this.platformService.unsuspendTenant(ownerId, reason, operationId);

      request.subscribe({
        next: () => {
          this.pendingOwnerId = null;
          this.reasonModalPending = false;
          this.actionMessage = action === 'suspend'
            ? 'İşletme askıya alındı.'
            : 'İşletme tekrar aktifleştirildi.';
          this.resetReasonModal();
          this.loadDashboard();
        },
        error: (err) => {
          this.pendingOwnerId = null;
          this.reasonModalPending = false;
          this.actionError = err?.error?.message ?? (action === 'suspend' ? 'Askıya alma başarısız.' : 'Aktifleştirme başarısız.');
        }
      });
    }
  }

  private openReasonModal(
    action: 'suspend' | 'unsuspend' | 'bulk-extend' | 'bulk-unlimited' | 'plan-change' | 'single-extend' | 'single-unlimited',
    ownerId: string | null,
    title: string,
    description: string,
    required: boolean,
    confirmText: string,
    days: number | null = null,
    plan: number | null = null
  ) {
    this.reasonModalAction = action;
    this.reasonModalOwnerId = ownerId;
    this.reasonModalDays = days;
    this.reasonModalPlan = plan;
    this.reasonModalTitle = title;
    this.reasonModalDescription = description;
    this.reasonModalRequired = required;
    this.reasonModalConfirmText = confirmText;
    this.reasonModalTemplates = this.getReasonTemplatesForAction(action);
    this.reasonModalPlaceholder = action === 'suspend'
      ? 'Örn: Fatura problemi nedeniyle geçici kapatma'
      : action === 'unsuspend'
        ? 'Örn: Ödeme onayı alındı'
        : action === 'plan-change'
          ? 'Örn: müşteri talebiyle plan yükseltildi'
          : action === 'single-extend'
            ? 'Örn: satış kampanyası nedeniyle +30 gün'
            : action === 'single-unlimited'
              ? 'Örn: yönetim onayı ile süresiz yapıldı'
        : 'Toplu işlem notu (zorunlu)';
    this.reasonModalPending = false;
    this.reasonModalOpen = true;
  }

  private resetReasonModal() {
    this.reasonModalOpen = false;
    this.reasonModalTitle = '';
    this.reasonModalDescription = '';
    this.reasonModalRequired = false;
    this.reasonModalConfirmText = 'Onayla';
    this.reasonModalTemplates = [];
    this.reasonModalPlaceholder = 'İşlem notu yazın';
    this.reasonModalPending = false;
    this.reasonModalAction = null;
    this.reasonModalOwnerId = null;
    this.reasonModalDays = null;
    this.reasonModalPlan = null;
  }

  private closeExtendMenus() {
    this.bulkExtendMenuOpen = false;
    this.openTenantExtendMenuOwnerId = null;
  }

  private planLabel(plan: number): string {
    if (plan === 1) return 'CatalogOnly';
    if (plan === 2) return 'CatalogWithAI';
    if (plan === 3) return 'CatalogWithAIAndEcommerce';
    return `Plan-${plan}`;
  }

  private newOperationId(): string {
    if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
      return `op_${crypto.randomUUID()}`;
    }
    return `op_${Date.now()}_${Math.random().toString(16).slice(2, 10)}`;
  }

  private escapeCsv(value: string): string {
    const text = `${value ?? ''}`;
    if (/[",\n\r]/.test(text)) {
      return `"${text.replace(/"/g, '""')}"`;
    }
    return text;
  }

  private getReasonTemplatesForAction(
    action: 'suspend' | 'unsuspend' | 'bulk-extend' | 'bulk-unlimited' | 'plan-change' | 'single-extend' | 'single-unlimited'
  ): string[] {
    switch (action) {
      case 'suspend':
        return [
          'Ödeme gecikmesi',
          'Kullanım koşulu ihlali',
          'Müşteri talebiyle geçici durdurma'
        ];
      case 'unsuspend':
        return [
          'Ödeme onayı alındı',
          'İhlal incelemesi tamamlandı',
          'Müşteri talebiyle tekrar aktif'
        ];
      case 'bulk-extend':
      case 'single-extend':
        return [
          'Kampanya nedeniyle süre uzatımı',
          'Destek kararı ile süre uzatımı',
          'Müşteri memnuniyeti amacıyla uzatma'
        ];
      case 'bulk-unlimited':
      case 'single-unlimited':
        return [
          'Yönetim kararı ile süresiz erişim',
          'Kurumsal anlaşma kapsamında süresiz',
          'Kalıcı paket upgrade onayı'
        ];
      case 'plan-change':
        return [
          'Müşteri talebiyle plan değişikliği',
          'Satış ekibi yönlendirmesiyle plan güncelleme',
          'Kullanım ihtiyacına göre plan revizyonu'
        ];
      default:
        return [];
    }
  }
}
