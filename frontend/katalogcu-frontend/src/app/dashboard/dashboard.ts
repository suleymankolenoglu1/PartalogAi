import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import {
  CatalogAiJobItem,
  CatalogAiJobSummary,
  CatalogService,
  DashboardStats
} from '../core/services/catalog.service';
import { AuthService } from '../core/services/auth.service';
import { environment } from '../../environments/environment';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css'
})
export class DashboardComponent implements OnInit, OnDestroy {
  
  private catalogService = inject(CatalogService);
  private authService = inject(AuthService);
  private router = inject(Router);
  private aiJobsPollTimer: ReturnType<typeof setInterval> | null = null;

  stats: (DashboardStats & { visualEmbeddingCount?: number }) | null = null;
  isLoading = true;
  aiJobsLoading = true;
  aiJobsError: string | null = null;
  aiJobsSummary: CatalogAiJobSummary = {
    total: 0,
    pending: 0,
    processing: 0,
    completed: 0,
    failed: 0
  };
  aiJobs: CatalogAiJobItem[] = [];
  publishingCatalogIds = new Set<string>();

  ngOnInit() {
    this.loadStats();
    if (this.canUseAi) {
      this.loadAiJobs();
      this.startAiJobsPolling();
    }
  }

  ngOnDestroy() {
    if (this.aiJobsPollTimer) {
      clearInterval(this.aiJobsPollTimer);
      this.aiJobsPollTimer = null;
    }
  }

  loadStats() {
    this.isLoading = true;
    this.catalogService.getDashboardStats().subscribe({
      next: (data) => {
        this.stats = data;
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Dashboard verisi çekilemedi:', err);
        this.isLoading = false;
      }
    });
  }

  loadAiJobs(silent = false) {
    if (!this.canUseAi) return;
    if (!silent) {
      this.aiJobsLoading = true;
    }

    this.catalogService.getCatalogAiJobs(8).subscribe({
      next: (res) => {
        this.aiJobsSummary = res.summary ?? this.aiJobsSummary;
        this.aiJobs = res.jobs ?? [];
        this.aiJobsError = null;
        this.aiJobsLoading = false;
      },
      error: (err) => {
        if (!silent) {
          this.aiJobsError = err?.error?.message || 'AI işlemleri alınamadı.';
          this.aiJobsLoading = false;
        }
      }
    });
  }

  startAiJobsPolling() {
    if (!this.canUseAi) return;
    if (this.aiJobsPollTimer) {
      clearInterval(this.aiJobsPollTimer);
    }

    this.aiJobsPollTimer = setInterval(() => {
      this.loadAiJobs(true);
    }, 10000);
  }

  // Statüye göre renk sınıfı döndüren yardımcı fonksiyon
  getStatusClass(status: string): string {
    switch (status) {
      case 'Published': return 'badge-ok';    // Yeşil
      case 'Processing': return 'badge-wait'; // Turuncu/Sarı
      case 'Draft': return 'badge-gray';      // Gri
      default: return 'badge-gray';
    }
  }

  // Statü metnini Türkçeye çeviren yardımcı fonksiyon
  getStatusLabel(status: string): string {
    switch (status) {
      case 'Published': return 'YAYINDA';
      case 'Processing': return 'İŞLENİYOR';
      case 'Draft': return 'TASLAK';
      case 'Pending': return 'ONAY BEKLİYOR';
      default: return status;
    }
  }

  getAiJobStatusClass(status: string): string {
    const s = status?.toLowerCase();
    if (s === 'pending') return 'job-chip pending';
    if (s === 'processing') return 'job-chip processing';
    if (s === 'completed') return 'job-chip completed';
    if (s === 'failed') return 'job-chip failed';
    return 'job-chip';
  }

  getAiJobStatusLabel(status: string): string {
    const s = status?.toLowerCase();
    if (s === 'pending') return 'Beklemede';
    if (s === 'processing') return 'İşleniyor';
    if (s === 'completed') return 'Tamamlandı';
    if (s === 'failed') return 'Başarısız';
    return status;
  }

  openAiJobInCatalogs(job: CatalogAiJobItem, event?: Event) {
    event?.stopPropagation();
    this.router.navigate(['/dashboard/catalogs'], {
      queryParams: {
        jobId: job.jobId,
        catalogId: job.catalogId
      }
    });
  }

  goToUpload() {
    this.router.navigate(['/dashboard/catalogs/new']);
  }

  goToCatalog(catalogId: string) {
    this.router.navigate(['/dashboard/catalog', catalogId]);
  }

  goToCatalogOptions(catalogId: string, event?: Event) {
    event?.stopPropagation();
    this.router.navigate(['/dashboard/catalogs'], {
      queryParams: { catalogId }
    });
  }

  publishCatalog(catalogId: string, event?: Event) {
    event?.stopPropagation();
    if (this.publishingCatalogIds.has(catalogId)) return;
    this.publishingCatalogIds.add(catalogId);
    this.catalogService.publishCatalog(catalogId).subscribe({
      next: () => {
        this.publishingCatalogIds.delete(catalogId);
        this.loadStats();
      },
      error: (err) => {
        console.error('Katalog yayınlanamadı:', err);
        this.publishingCatalogIds.delete(catalogId);
      }
    });
  }

  isPublishing(catalogId: string): boolean {
    return this.publishingCatalogIds.has(catalogId);
  }

  getCoverImageUrl(item: any): string {
    return item?.imageUrl || item?.coverImageUrl || item?.thumbnailUrl || 'https://placehold.co/800x450?text=Catalog';
  }

  hasTechnicalDrawing(item: any): boolean {
    return item?.isTechnicalDrawing === true || item?.hasTechnicalDrawing === true;
  }

  getPageCount(item: any): number {
    const value = item?.pageCount ?? item?.totalPages ?? item?.pages ?? 0;
    return Number.isFinite(Number(value)) ? Number(value) : 0;
  }

  getPartCount(item: any): number {
    const value = item?.partCount ?? item?.totalParts ?? 0;
    return Number.isFinite(Number(value)) ? Number(value) : 0;
  }

  getUpdatedDate(item: any): string {
    return item?.updatedDate || item?.updatedAt || item?.createdDate || new Date().toISOString();
  }

  getCatalogErrorMessage(item: any): string {
    return item?.errorMessage || item?.lastError || 'Katalog işleme sırasında hata oluştu.';
  }

  getRelativeUpdateText(item: any): string {
    const raw = this.getUpdatedDate(item);
    const date = new Date(raw);
    if (!Number.isFinite(date.getTime())) return 'Güncelleme tarihi yok';

    const diffMs = Date.now() - date.getTime();
    const min = Math.floor(diffMs / 60000);
    if (min < 60) return `${Math.max(min, 1)} dk önce güncellendi`;
    const hour = Math.floor(min / 60);
    if (hour < 24) return `${hour} saat önce güncellendi`;
    const day = Math.floor(hour / 24);
    if (day < 30) return `${day} gün önce güncellendi`;
    const month = Math.floor(day / 30);
    return `${month} ay önce güncellendi`;
  }

  isProcessingStatus(status: string): boolean {
    const normalized = String(status || '').toLowerCase();
    return normalized === 'processing' || normalized === 'pending';
  }

  isErrorStatus(status: string): boolean {
    const normalized = String(status || '').toLowerCase();
    return normalized === 'failed' || normalized === 'error';
  }

  isDraftStatus(status: string): boolean {
    return String(status || '').toLowerCase() === 'draft';
  }

  goUpgrade() {
    this.router.navigate(['/upgrade']);
  }

  get currentPlan(): number {
    return this.authService.getCurrentPlan();
  }

  get isPlan1(): boolean {
    return this.currentPlan === 1;
  }

  get isPlan2(): boolean {
    return this.currentPlan === 2;
  }

  get isPlan3(): boolean {
    return this.currentPlan >= 3;
  }

  get canUseAi(): boolean {
    return this.supportsAiFeature && this.currentPlan >= 2;
  }

  get canUseEcommerce(): boolean {
    return this.supportsEcommerceFeature && this.currentPlan >= 3;
  }

  get supportsAiFeature(): boolean {
    return environment.features.enableChatbot || environment.features.enableCatalogAnalysis;
  }

  get supportsEcommerceFeature(): boolean {
    return environment.features.enableEcommerce;
  }

  get monthlyAiQueryCount(): number {
    return this.aiUsedThisMonth;
  }

  get aiUsedThisMonth(): number {
    if (!this.canUseAi) return 0;
    if (typeof this.stats?.aiUsedThisMonth === 'number') return this.stats.aiUsedThisMonth;
    return 0;
  }

  get aiMonthlyLimit(): number | null {
    if (!this.canUseAi) return 0;
    if (this.stats?.aiUnlimited) return null;
    if (this.stats?.aiMonthlyLimit === null) return null;
    if (typeof this.stats?.aiMonthlyLimit === 'number') return this.stats.aiMonthlyLimit;
    return this.currentPlan === 2 ? 500 : null;
  }

  get aiRemainingThisMonth(): number | null {
    if (!this.canUseAi) return 0;
    if (this.aiMonthlyLimit === null) return null;
    if (typeof this.stats?.aiRemainingThisMonth === 'number') return this.stats.aiRemainingThisMonth;
    return Math.max((this.aiMonthlyLimit ?? 0) - this.aiUsedThisMonth, 0);
  }

  get aiUsagePercent(): number {
    const limit = this.aiMonthlyLimit;
    if (limit === null || limit <= 0) return 0;
    return Math.min(100, Math.max(0, Math.round((this.aiUsedThisMonth / limit) * 100)));
  }

  get aiMonthlyLimitText(): string {
    const limit = this.aiMonthlyLimit;
    if (limit === null) return 'Sınırsız';
    return limit.toLocaleString('tr-TR');
  }

  get aiRemainingThisMonthText(): string {
    const remaining = this.aiRemainingThisMonth;
    if (remaining === null) return 'Sınırsız';
    return remaining.toLocaleString('tr-TR');
  }

  get storefrontVisitsTotal(): number {
    return Number(this.stats?.storefrontVisitsTotal ?? 0);
  }

  get storefrontVisitsToday(): number {
    return Number(this.stats?.storefrontVisitsToday ?? 0);
  }

  get storefrontVisitsLast7Days(): number {
    return Number(this.stats?.storefrontVisitsLast7Days ?? 0);
  }

  get storefrontUniqueVisitorsLast30Days(): number {
    return Number(this.stats?.storefrontUniqueVisitorsLast30Days ?? 0);
  }

  get storefrontVisitGrowthRatio(): number {
    const week = this.storefrontVisitsLast7Days;
    if (week <= 0) return 0;
    return Math.round((this.storefrontVisitsToday / week) * 100);
  }
}
