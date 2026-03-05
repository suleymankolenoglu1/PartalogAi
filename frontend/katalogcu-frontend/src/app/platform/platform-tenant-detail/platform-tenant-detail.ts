import { Component, HostListener, OnInit, inject } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PlatformAdminService, PlatformTenantDetail, TenantAuditEvent, TenantUsagePoint } from '../../core/services/platform-admin.service';
import { ActionReasonModalComponent } from '../shared/action-reason-modal/action-reason-modal';

type UsageMetricKey = 'catalogs' | 'parts' | 'orders' | 'aiJobs';

@Component({
  selector: 'app-platform-tenant-detail',
  standalone: true,
  imports: [CommonModule, DatePipe, RouterLink, FormsModule, ActionReasonModalComponent],
  templateUrl: './platform-tenant-detail.html',
  styleUrl: './platform-tenant-detail.css'
})
export class PlatformTenantDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly platformService = inject(PlatformAdminService);

  ownerId = '';
  isLoading = false;
  errorMessage = '';
  detail: PlatformTenantDetail | null = null;
  selectedPlan: number | null = null;
  planExpiresAtInput = '';
  planActionReason = '';
  actionMessage = '';
  actionError = '';
  pendingAction: 'plan' | 'suspend' | 'unsuspend' | null = null;
  quickActionMenuOpen = false;
  reasonModalOpen = false;
  reasonModalTitle = '';
  reasonModalDescription = '';
  reasonModalRequired = false;
  reasonModalConfirmText = 'Onayla';
  reasonModalPlaceholder = 'İşlem notu yazın';
  reasonModalTemplates: string[] = [];
  reasonModalPending = false;
  auditTypeFilter = 'all';
  auditQuery = '';
  auditDateFrom = '';
  auditDateTo = '';
  orderStatusFilter = 'all';
  orderQuery = '';
  orderDateFrom = '';
  orderDateTo = '';
  expandedOrderId: string | null = null;
  private reasonModalAction: 'suspend' | 'unsuspend' | null = null;

  metricMax: Record<UsageMetricKey, number> = {
    catalogs: 1,
    parts: 1,
    orders: 1,
    aiJobs: 1
  };

  ngOnInit(): void {
    this.ownerId = this.route.snapshot.paramMap.get('ownerId') ?? '';
    if (!this.ownerId) {
      this.errorMessage = 'Geçersiz işletme kimliği.';
      return;
    }
    this.loadDetail();
  }

  @HostListener('document:click')
  onDocumentClick() {
    this.quickActionMenuOpen = false;
  }

  loadDetail() {
    this.isLoading = true;
    this.errorMessage = '';
    this.platformService.getTenantDetail(this.ownerId).subscribe({
      next: (detail) => {
        this.detail = detail;
        this.selectedPlan = detail.plan;
        this.planExpiresAtInput = this.toDateInputValue(detail.planExpiresAt);
        this.metricMax = {
          catalogs: this.getMax(detail.monthlyUsage, 'catalogs'),
          parts: this.getMax(detail.monthlyUsage, 'parts'),
          orders: this.getMax(detail.monthlyUsage, 'orders'),
          aiJobs: this.getMax(detail.monthlyUsage, 'aiJobs')
        };
        this.isLoading = false;
      },
      error: (err) => {
        this.errorMessage = err?.error?.message ?? 'İşletme detayı alınamadı.';
        this.isLoading = false;
      }
    });
  }

  updatePlan() {
    if (!this.detail || !this.selectedPlan) return;
    this.actionMessage = '';
    this.actionError = '';
    this.quickActionMenuOpen = false;
    this.pendingAction = 'plan';

    this.platformService.updateTenantPlan(this.detail.ownerId, {
      plan: this.selectedPlan,
      expiresAt: this.toIsoFromDateInput(this.planExpiresAtInput),
      reason: this.normalizeReason(this.planActionReason),
      operationId: this.newOperationId()
    }).subscribe({
      next: () => {
        this.pendingAction = null;
        this.planActionReason = '';
        this.actionMessage = 'Paket güncellendi.';
        this.loadDetail();
      },
      error: (err) => {
        this.pendingAction = null;
        this.actionError = err?.error?.message ?? 'Paket güncellenemedi.';
      }
    });
  }

  onQuickMenuContainerClick(event: Event) {
    event.stopPropagation();
  }

  toggleQuickActionMenu(event: Event) {
    event.stopPropagation();
    if (this.pendingAction !== null) return;
    this.quickActionMenuOpen = !this.quickActionMenuOpen;
  }

  selectQuickExpiry(days: number | null) {
    this.quickActionMenuOpen = false;
    if (days === null) {
      this.planExpiresAtInput = '';
      this.actionMessage = 'Bitiş tarihi süresiz olarak ayarlandı. Kaydetmek için "Planı Kaydet"e basın.';
      this.actionError = '';
      return;
    }

    this.planExpiresAtInput = this.toDateInputValue(this.buildExtendedExpiryIso(this.detail?.planExpiresAt ?? null, days));
    this.actionMessage = `Bitiş tarihi +${days} gün olarak ayarlandı. Kaydetmek için "Planı Kaydet"e basın.`;
    this.actionError = '';
  }

  selectQuickSuspendToggle() {
    this.quickActionMenuOpen = false;
    if (!this.detail || this.pendingAction !== null) return;
    if (this.detail.isSuspended) {
      this.unsuspend();
      return;
    }
    this.suspend();
  }

  suspend() {
    if (!this.detail || this.detail.isSuspended) return;
    this.quickActionMenuOpen = false;
    this.openReasonModal(
      'suspend',
      'İşletmeyi Askıya Al',
      'Bu işlem kullanıcı erişimini keser. İşlem notu zorunludur.',
      true,
      'Askıya Al'
    );
  }

  unsuspend() {
    if (!this.detail || !this.detail.isSuspended) return;
    this.quickActionMenuOpen = false;
    this.openReasonModal(
      'unsuspend',
      'İşletmeyi Aktifleştir',
      'Bu işlem kullanıcı erişimini tekrar açar. Not opsiyoneldir.',
      false,
      'Aktifleştir'
    );
  }

  canSubmitPlanUpdate(): boolean {
    if (!this.detail || !this.selectedPlan) return false;
    const currentExpiry = this.toDateInputValue(this.detail.planExpiresAt);
    const selectedExpiry = (this.planExpiresAtInput || '').trim();
    return this.selectedPlan !== this.detail.plan || currentExpiry !== selectedExpiry;
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
    if (dayDiff <= 7) return `${dayDiff} gün kaldı`;
    return `${dayDiff} gün kaldı`;
  }

  monthLabel(value: string): string {
    const [year, month] = value.split('-');
    return `${month}/${year.slice(2)}`;
  }

  usageBarHeight(value: number, metric: UsageMetricKey): string {
    const max = this.metricMax[metric] || 1;
    const ratio = Math.max(0.08, Math.min(1, value / max));
    return `${Math.round(ratio * 100)}%`;
  }

  get auditTypeOptions(): string[] {
    const set = new Set(
      (this.detail?.auditLog ?? [])
        .map((e) => (e.type || '').trim())
        .filter((x) => x.length > 0)
    );
    return ['all', ...Array.from(set).sort((a, b) => a.localeCompare(b))];
  }

  get filteredAuditLog(): TenantAuditEvent[] {
    const list = this.detail?.auditLog ?? [];
    const query = this.auditQuery.trim().toLocaleLowerCase('tr-TR');
    const from = this.auditDateFrom ? new Date(`${this.auditDateFrom}T00:00:00`) : null;
    const to = this.auditDateTo ? new Date(`${this.auditDateTo}T23:59:59.999`) : null;

    return list.filter((event) => {
      if (this.auditTypeFilter !== 'all' && event.type !== this.auditTypeFilter) {
        return false;
      }

      const eventDate = new Date(event.timestamp);
      if (from && eventDate < from) return false;
      if (to && eventDate > to) return false;

      if (!query) return true;

      const searchBlob = [
        event.type ?? '',
        event.title ?? '',
        event.detail ?? '',
        event.operationId ?? '',
        ...(event.changes ?? []).map((c) => `${c.field} ${c.before ?? ''} ${c.after ?? ''}`)
      ].join(' ').toLocaleLowerCase('tr-TR');

      return searchBlob.includes(query);
    });
  }

  get orderStatusOptions(): string[] {
    const set = new Set(
      (this.detail?.recentOrders ?? [])
        .map((o) => (o.status || '').trim())
        .filter((x) => x.length > 0)
    );
    return ['all', ...Array.from(set).sort((a, b) => a.localeCompare(b))];
  }

  get filteredRecentOrders() {
    const list = this.detail?.recentOrders ?? [];
    const query = this.orderQuery.trim().toLocaleLowerCase('tr-TR');
    const from = this.orderDateFrom ? new Date(`${this.orderDateFrom}T00:00:00`) : null;
    const to = this.orderDateTo ? new Date(`${this.orderDateTo}T23:59:59.999`) : null;

    return list.filter((order) => {
      if (this.orderStatusFilter !== 'all' && order.status !== this.orderStatusFilter) {
        return false;
      }

      const orderDate = new Date(order.createdAt);
      if (from && orderDate < from) return false;
      if (to && orderDate > to) return false;

      if (!query) return true;

      const searchBlob = [
        order.orderNumber ?? '',
        order.customerName ?? '',
        order.customerPhone ?? '',
        order.customerEmail ?? '',
        order.paymentMethod ?? '',
        ...(order.items ?? []).map((i) => `${i.productCode} ${i.productName}`)
      ].join(' ').toLocaleLowerCase('tr-TR');

      return searchBlob.includes(query);
    });
  }

  resetAuditFilters() {
    this.auditTypeFilter = 'all';
    this.auditQuery = '';
    this.auditDateFrom = '';
    this.auditDateTo = '';
  }

  resetOrderFilters() {
    this.orderStatusFilter = 'all';
    this.orderQuery = '';
    this.orderDateFrom = '';
    this.orderDateTo = '';
  }

  toggleOrderExpand(orderId: string) {
    this.expandedOrderId = this.expandedOrderId === orderId ? null : orderId;
  }

  isOrderExpanded(orderId: string): boolean {
    return this.expandedOrderId === orderId;
  }

  exportFilteredAuditCsv() {
    const rows = this.filteredAuditLog;
    if (!rows.length) {
      this.actionError = 'Dışa aktarılacak audit kaydı yok.';
      return;
    }

    const headers = ['timestamp', 'type', 'title', 'operationId', 'operationCount', 'detail', 'changes'];
    const csvRows = rows.map((event) => {
      const changes = (event.changes ?? [])
        .map((c) => `${c.field}: ${c.before ?? '-'} -> ${c.after ?? '-'}`)
        .join(' | ');

      return [
        event.timestamp ?? '',
        event.type ?? '',
        event.title ?? '',
        event.operationId ?? '',
        String(event.operationCount ?? ''),
        event.detail ?? '',
        changes
      ];
    });

    const csv = [
      headers.map((h) => this.escapeCsv(h)).join(','),
      ...csvRows.map((row) => row.map((cell) => this.escapeCsv(cell)).join(','))
    ].join('\n');

    const blob = new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    const stamp = new Date().toISOString().slice(0, 19).replace(/[:T]/g, '-');
    link.href = url;
    link.download = `tenant-audit-${this.ownerId}-${stamp}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }

  private getMax(points: TenantUsagePoint[], key: UsageMetricKey): number {
    const max = Math.max(...points.map(p => p[key]), 0);
    return Math.max(max, 1);
  }

  private toDateInputValue(value: string | null | undefined): string {
    if (!value) return '';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return '';
    return date.toISOString().slice(0, 10);
  }

  private toIsoFromDateInput(value: string): string | null {
    const normalized = value?.trim();
    if (!normalized) return null;
    const date = new Date(`${normalized}T00:00:00Z`);
    if (Number.isNaN(date.getTime())) return null;
    return date.toISOString();
  }

  private buildExtendedExpiryIso(currentIso: string | null, daysToAdd: number): string {
    const now = new Date();
    const current = currentIso ? new Date(currentIso) : null;
    const base = current && !Number.isNaN(current.getTime()) && current > now ? current : now;
    const next = new Date(base.getTime() + daysToAdd * 24 * 60 * 60 * 1000);
    return next.toISOString();
  }

  private normalizeReason(value: string): string | null {
    const normalized = (value || '').trim();
    return normalized.length ? normalized : null;
  }

  private escapeCsv(value: string): string {
    const text = `${value ?? ''}`;
    if (/[",\n\r]/.test(text)) {
      return `"${text.replace(/"/g, '""')}"`;
    }
    return text;
  }

  onReasonModalClosed() {
    if (this.reasonModalPending) return;
    this.resetReasonModal();
  }

  onReasonModalConfirmed(reason: string | null) {
    if (!this.detail || !this.reasonModalAction) {
      this.resetReasonModal();
      return;
    }

    const ownerId = this.detail.ownerId;
    const action = this.reasonModalAction;
    this.actionMessage = '';
    this.actionError = '';
    this.reasonModalPending = true;
    this.pendingAction = action;

    const request = action === 'suspend'
      ? this.platformService.suspendTenant(ownerId, reason, this.newOperationId())
      : this.platformService.unsuspendTenant(ownerId, reason, this.newOperationId());

    request.subscribe({
      next: () => {
        this.pendingAction = null;
        this.reasonModalPending = false;
        this.actionMessage = action === 'suspend'
          ? 'İşletme askıya alındı.'
          : 'İşletme tekrar aktifleştirildi.';
        this.resetReasonModal();
        this.loadDetail();
      },
      error: (err) => {
        this.pendingAction = null;
        this.reasonModalPending = false;
        this.actionError = err?.error?.message ?? (action === 'suspend' ? 'Askıya alma başarısız.' : 'Aktifleştirme başarısız.');
      }
    });
  }

  private openReasonModal(
    action: 'suspend' | 'unsuspend',
    title: string,
    description: string,
    required: boolean,
    confirmText: string
  ) {
    this.reasonModalAction = action;
    this.reasonModalTitle = title;
    this.reasonModalDescription = description;
    this.reasonModalRequired = required;
    this.reasonModalConfirmText = confirmText;
    this.reasonModalTemplates = this.getReasonTemplatesForAction(action);
    this.reasonModalPlaceholder = action === 'suspend'
      ? 'Örn: Fatura problemi nedeniyle geçici kapatma'
      : 'Örn: Ödeme onayı alındı';
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
  }

  private getReasonTemplatesForAction(action: 'suspend' | 'unsuspend'): string[] {
    if (action === 'suspend') {
      return [
        'Ödeme gecikmesi',
        'Kullanım koşulu ihlali',
        'Müşteri talebiyle geçici durdurma'
      ];
    }

    return [
      'Ödeme onayı alındı',
      'İhlal incelemesi tamamlandı',
      'Müşteri talebiyle tekrar aktif'
    ];
  }

  private newOperationId(): string {
    if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
      return `op_${crypto.randomUUID()}`;
    }
    return `op_${Date.now()}_${Math.random().toString(16).slice(2, 10)}`;
  }
}
