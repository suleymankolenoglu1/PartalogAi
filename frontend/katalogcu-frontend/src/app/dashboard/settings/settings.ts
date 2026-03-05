import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms'; // 🔥 HTML'de ngModel kullandığımız için şart
import {
  Catalog,
  CatalogService,
  EmbedDomainVerification,
  EmbedSettings,
  EmbedVerifyOriginResponse,
  PublicTokenStatus,
  ShowcaseMedia
} from '../../core/services/catalog.service'; // Interface'i import ettik
import { AuthService } from '../../core/services/auth.service';
import { ActivatedRoute } from '@angular/router';
import { Subscription } from 'rxjs';
import { PlanId } from '../../core/models/plan.model';
import { environment } from '../../../environments/environment';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, FormsModule], 
  templateUrl: './settings.html',
  styleUrl: './settings.css'
})
export class SettingsComponent implements OnInit, OnDestroy {
  private catalogService = inject(CatalogService);
  private authService = inject(AuthService);
  private route = inject(ActivatedRoute);
  private sanitizer = inject(DomSanitizer);
  private queryParamSub?: Subscription;

  // Aktif sekme (Type güvenliği için string literal kullandık)
  activeTab: 'general' | 'security' | 'notifications' | 'showcase' | 'public' | 'plan' = 'general';

  // --- PUBLIC LINK VERİLERİ ---
  publicToken: string | null = null;
  publicTokenStatus: PublicTokenStatus | null = null;
  publicActionLoading = false;
  publicActionMessage: string | null = null;
  publicActionError: string | null = null;
  publishedCatalogs: Catalog[] = [];
  selectedCatalogIds = new Set<string>();
  embedSettings: EmbedSettings | null = null;
  embedAllowedOrigins: string[] = [];
  embedOriginInput = '';
  embedTheme = 'default';
  embedMode = 'catalog';
  embedLoading = false;
  embedSaving = false;
  embedMessage: string | null = null;
  embedError: string | null = null;
  embedVerifyOriginInput = '';
  embedVerifying = false;
  embedVerifyResult: EmbedVerifyOriginResponse | null = null;
  embedDomainMethod: 'dns_txt' | 'file' = 'dns_txt';
  embedDomainsLoading = false;
  embedDomains: EmbedDomainVerification[] = [];

  // --- PROFİL (GERÇEK VERİ) ---
  profile = {
    firstName: '',
    lastName: '',
    email: '',
    companyName: '',
    phoneNumber: ''
  };
  isProfileLoading = false;
  isProfileSaving = false;
  profileSuccess: string | null = null;
  profileError: string | null = null;
  isPlanSubmitting = false;
  planSuccess: string | null = null;
  planError: string | null = null;

  // --- VITRIN (SHOWCASE) VERİLERİ ---

  // Mevcut Vitrin Listesi (şimdilik local state; backend entegrasyonu ayrı adım)
  showcaseItems: ShowcaseMedia[] = [];

  // Yeni eklenecek medya için geçici obje (Forma bağlı)
  newMedia: Partial<ShowcaseMedia> = {
    type: 'image',
    title: '',
    subtitle: '',
    url: ''
  };

  // --- FONKSİYONLAR ---

  // Sekme Değiştirme
  ngOnInit(): void {
    this.bindRouteTabParams();
    this.loadProfile();
    this.loadPublishedCatalogs();
    this.loadPublicLinkState();
  }

  ngOnDestroy(): void {
    this.queryParamSub?.unsubscribe();
  }

  get profileAvatarUrl(): string {
    const fullName = `${this.profile.firstName} ${this.profile.lastName}`.trim();
    const fallback = 'Katalogcu User';
    const encoded = encodeURIComponent(fullName || fallback);
    return `https://ui-avatars.com/api/?name=${encoded}&background=0F172A&color=ffffff&size=128`;
  }

  loadProfile() {
    this.isProfileLoading = true;
    this.profileError = null;
    this.profileSuccess = null;

    this.authService.getMe().subscribe({
      next: (me) => {
        this.profile.firstName = me.firstName || '';
        this.profile.lastName = me.lastName || '';
        this.profile.email = me.email || '';
        this.profile.companyName = me.companyName || '';
        this.profile.phoneNumber = me.phoneNumber || '';
        this.isProfileLoading = false;
      },
      error: () => {
        this.isProfileLoading = false;
        this.profileError = 'Profil bilgileri yüklenemedi.';
      }
    });
  }

  get currentPlan(): PlanId {
    return this.authService.getCurrentPlan();
  }

  get currentPlanLabel(): string {
    return this.authService.getCurrentPlanDisplayName();
  }

  setActiveTab(tabName: 'general' | 'security' | 'notifications' | 'showcase' | 'public' | 'plan') {
    this.activeTab = tabName;
    this.planSuccess = null;
    this.planError = null;
  }

  private bindRouteTabParams() {
    this.queryParamSub = this.route.queryParamMap.subscribe((query) => {
      const requestedTab = query.get('tab');
      if (requestedTab && this.isSettingsTab(requestedTab)) {
        this.activeTab = requestedTab;
      }

      const section = query.get('section');
      if (this.activeTab === 'public' && section === 'link-management') {
        setTimeout(() => {
          document.getElementById('link-management')?.scrollIntoView({
            behavior: 'smooth',
            block: 'start'
          });
        }, 0);
        return;
      }

    });
  }

  private isSettingsTab(tab: string): tab is 'general' | 'security' | 'notifications' | 'showcase' | 'public' | 'plan' {
    return tab === 'general'
      || tab === 'security'
      || tab === 'notifications'
      || tab === 'showcase'
      || tab === 'public'
      || tab === 'plan';
  }

  loadEmbedSettings() {
    this.embedLoading = true;
    this.embedError = null;
    this.catalogService.getEmbedSettings().subscribe({
      next: (settings) => {
        this.embedSettings = settings;
        this.embedAllowedOrigins = [...(settings.allowedOrigins || [])];
        this.embedTheme = settings.theme || 'default';
        this.embedMode = settings.mode || 'catalog';
        this.embedLoading = false;
      },
      error: () => {
        this.embedLoading = false;
        this.embedError = 'Embed ayarları yüklenemedi.';
      }
    });
  }

  loadEmbedDomains() {
    this.embedDomainsLoading = true;
    this.catalogService.getEmbedDomainVerifications().subscribe({
      next: (rows) => {
        this.embedDomains = rows || [];
        this.embedDomainsLoading = false;
      },
      error: () => {
        this.embedDomainsLoading = false;
      }
    });
  }

  createEmbedDomainChallenge() {
    const normalized = this.normalizeOrigin(this.embedOriginInput);
    if (!normalized) {
      this.embedError = 'Geçerli bir origin girin. Örn: https://www.site.com';
      return;
    }

    this.embedSaving = true;
    this.embedError = null;
    this.embedMessage = null;
    this.catalogService.createEmbedDomainChallenge({
      origin: normalized,
      method: this.embedDomainMethod
    }).subscribe({
      next: (row) => {
        this.embedSaving = false;
        this.embedOriginInput = '';
        this.embedDomains = [row, ...this.embedDomains.filter((x) => x.id !== row.id)];
        this.embedMessage = 'Domain doğrulama challenge oluşturuldu.';
      },
      error: (err) => {
        this.embedSaving = false;
        this.embedError = typeof err?.error === 'string'
          ? err.error
          : (err?.error?.message ?? 'Challenge oluşturulamadı.');
      }
    });
  }

  verifyDomainNow(row: EmbedDomainVerification) {
    this.catalogService.verifyEmbedDomainNow(row.id).subscribe({
      next: (updated) => {
        this.embedDomains = this.embedDomains.map((x) => x.id === updated.id ? updated : x);
        this.embedMessage = updated.status === 'verified'
          ? `${updated.origin} doğrulandı.`
          : `${updated.origin} doğrulanamadı.`;
        this.embedError = updated.status === 'verified' ? null : (updated.lastError || 'Doğrulama başarısız.');
      },
      error: (err) => {
        this.embedError = typeof err?.error === 'string'
          ? err.error
          : (err?.error?.message ?? 'Doğrulama başarısız.');
      }
    });
  }

  activateDomain(row: EmbedDomainVerification) {
    this.catalogService.activateEmbedDomain(row.id).subscribe({
      next: () => {
        this.embedMessage = `${row.origin} allowlist'e eklendi.`;
        this.embedError = null;
        this.loadEmbedSettings();
      },
      error: (err) => {
        this.embedError = typeof err?.error === 'string'
          ? err.error
          : (err?.error?.message ?? 'Domain aktif edilemedi.');
      }
    });
  }

  deleteDomain(row: EmbedDomainVerification) {
    this.catalogService.deleteEmbedDomainVerification(row.id).subscribe({
      next: () => {
        this.embedDomains = this.embedDomains.filter((x) => x.id !== row.id);
      },
      error: () => {
        this.embedError = 'Domain kaydı silinemedi.';
      }
    });
  }

  addEmbedOrigin() {
    const normalized = this.normalizeOrigin(this.embedOriginInput);
    if (!normalized) {
      this.embedError = 'Geçerli bir origin girin. Örn: https://www.site.com';
      return;
    }

    if (this.embedAllowedOrigins.includes(normalized)) {
      this.embedOriginInput = '';
      return;
    }

    this.embedAllowedOrigins = [...this.embedAllowedOrigins, normalized];
    this.embedOriginInput = '';
    this.embedError = null;
  }

  removeEmbedOrigin(origin: string) {
    this.embedAllowedOrigins = this.embedAllowedOrigins.filter((x) => x !== origin);
  }

  saveEmbedSettings() {
    this.embedSaving = true;
    this.embedMessage = null;
    this.embedError = null;
    this.catalogService.updateEmbedSettings({
      allowedOrigins: this.embedAllowedOrigins,
      theme: this.embedTheme,
      mode: this.embedMode
    }).subscribe({
      next: (settings) => {
        this.embedSettings = settings;
        this.embedAllowedOrigins = [...(settings.allowedOrigins || [])];
        this.embedTheme = settings.theme || 'default';
        this.embedMode = settings.mode || 'catalog';
        this.embedSaving = false;
        this.embedMessage = 'Embed ayarları kaydedildi.';
      },
      error: (err) => {
        this.embedSaving = false;
        this.embedError = typeof err?.error === 'string'
          ? err.error
          : (err?.error?.message ?? 'Embed ayarları kaydedilemedi.');
      }
    });
  }

  async copyEmbedScript() {
    if (!this.publicToken) {
      this.embedError = 'Önce Public Link üretin.';
      return;
    }

    const apiRoot = this.getApiRoot();
    const scriptText = `<div id="partalog-embed-root"></div>
<script src="${apiRoot}/embed.js"
  data-public-token="${this.publicToken}"
  data-api-base-url="${apiRoot}"
  data-app-base-url="${window.location.origin}"
  data-target="partalog-embed-root"
  data-height="780px"></script>`;

    try {
      await navigator.clipboard.writeText(scriptText);
      this.embedMessage = 'Embed script panoya kopyalandı.';
      this.embedError = null;
    } catch {
      this.embedError = 'Script kopyalanamadı.';
      this.embedMessage = null;
    }
  }

  verifyEmbedOrigin() {
    if (!this.publicToken) {
      this.embedError = 'Önce Public Link üretin.';
      return;
    }

    const normalized = this.normalizeOrigin(this.embedVerifyOriginInput);
    if (!normalized) {
      this.embedError = 'Doğrulama için geçerli origin girin.';
      return;
    }

    this.embedVerifying = true;
    this.embedVerifyResult = null;
    this.embedError = null;
    this.catalogService.verifyEmbedOrigin(this.publicToken, normalized).subscribe({
      next: (result) => {
        this.embedVerifyResult = result;
        this.embedVerifying = false;
      },
      error: (err) => {
        this.embedVerifying = false;
        const reason = typeof err?.error === 'string'
          ? err.error
          : (err?.error?.reason ?? err?.error?.message ?? 'Doğrulama başarısız.');
        this.embedError = reason;
      }
    });
  }

  get embedPreviewUrl(): string | null {
    if (!this.publicToken) return null;
    return `${window.location.origin}/p/${this.publicToken}?embed=1`;
  }

  get embedPreviewSafeUrl(): SafeResourceUrl | null {
    const url = this.embedPreviewUrl;
    if (!url) return null;
    return this.sanitizer.bypassSecurityTrustResourceUrl(url);
  }

  get embedScriptSourceUrl(): string {
    const apiRoot = this.getApiRoot();
    return `${apiRoot}/embed.js`;
  }

  copyDomainInstruction(value: string | null | undefined) {
    if (!value) return;
    navigator.clipboard.writeText(value).then(() => {
      this.embedMessage = 'Kopyalandı.';
      this.embedError = null;
    }).catch(() => {
      this.embedError = 'Kopyalanamadı.';
      this.embedMessage = null;
    });
  }

  private normalizeOrigin(value: string): string {
    const input = String(value || '').trim();
    if (!input) return '';
    try {
      const url = new URL(input);
      if (url.protocol !== 'https:' && url.protocol !== 'http:') return '';
      return `${url.protocol}//${url.host}`.toLowerCase();
    } catch {
      return '';
    }
  }

  private getApiRoot(): string {
    const raw = (environment.apiUrl || '').trim().replace(/\/+$/, '');
    return raw.endsWith('/api') ? raw.slice(0, -4) : raw;
  }

  isCurrentPlan(plan: PlanId): boolean {
    return this.currentPlan === plan;
  }

  getPlanActionLabel(plan: PlanId): string {
    if (this.isCurrentPlan(plan)) return 'Mevcut Plan';
    if (plan < this.currentPlan) return 'Bu Plana Düşür';
    return 'Bu Plana Yükselt';
  }

  changePlan(plan: PlanId) {
    if (this.isPlanSubmitting || this.isCurrentPlan(plan)) return;

    if (plan < this.currentPlan) {
      const approve = confirm('Planı düşürmek istediğine emin misin? Plan dışında kalan modüller kapanacak.');
      if (!approve) return;
    }

    this.isPlanSubmitting = true;
    this.planSuccess = null;
    this.planError = null;

    this.authService.selectPlan(plan).subscribe({
      next: () => {
        this.isPlanSubmitting = false;
        this.planSuccess = 'Plan başarıyla güncellendi.';
        this.loadProfile();
      },
      error: (err) => {
        this.isPlanSubmitting = false;
        this.planError = typeof err?.error === 'string'
          ? err.error
          : (err?.error?.message ?? 'Plan güncellenemedi.');
      }
    });
  }

  cancelPaidPlan() {
    if (this.isPlanSubmitting || this.currentPlan === 1) return;

    const approve = confirm('Ücretli planı iptal edip Katalog paketine dönmek istiyor musun?');
    if (!approve) return;

    this.isPlanSubmitting = true;
    this.planSuccess = null;
    this.planError = null;

    this.authService.cancelPlan().subscribe({
      next: () => {
        this.isPlanSubmitting = false;
        this.planSuccess = 'Ücretli plan iptal edildi. Katalog paketine geçildi.';
        this.loadProfile();
      },
      error: (err) => {
        this.isPlanSubmitting = false;
        this.planError = typeof err?.error === 'string'
          ? err.error
          : (err?.error?.message ?? 'Plan iptal edilemedi.');
      }
    });
  }

  loadPublishedCatalogs() {
    this.catalogService.getCatalogs().subscribe({
      next: (catalogs) => {
        this.publishedCatalogs = catalogs.filter(c => c.status === 'Published');
      },
      error: () => {
        this.publishedCatalogs = [];
      }
    });
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

  toggleCatalogSelection(catalogId: string, checked: boolean) {
    if (checked) this.selectedCatalogIds.add(catalogId);
    else this.selectedCatalogIds.delete(catalogId);
  }

  isCatalogSelected(catalogId: string): boolean {
    return this.selectedCatalogIds.has(catalogId);
  }

  private getSelectedCatalogIdList(): string[] {
    return Array.from(this.selectedCatalogIds.values());
  }

  generatePublicLink() {
    if (this.publicActionLoading) return;
    this.publicActionLoading = true;
    this.publicActionMessage = null;
    this.publicActionError = null;

    const selectedIds = this.getSelectedCatalogIdList();
    if (this.publicTokenStatus?.enabled === false) {
      this.catalogService.rotatePublicToken(selectedIds.length ? selectedIds : undefined).subscribe({
        next: (res) => {
          this.publicToken = res.token;
          this.publicTokenStatus = { enabled: res.enabled, version: res.version };
          this.publicActionMessage = 'Public link yeniden aktif edildi ve üretildi.';
          this.publicActionLoading = false;
        },
        error: () => {
          this.publicActionError = 'Public link üretilemedi.';
          this.publicActionLoading = false;
        }
      });
      return;
    }

    this.catalogService.getPublicToken(selectedIds.length ? selectedIds : undefined).subscribe({
      next: (res) => {
        this.publicToken = res.token;
        if (!this.publicTokenStatus) {
          this.publicTokenStatus = { enabled: true, version: 1 };
        }
        this.publicActionMessage = 'Yeni public link üretildi.';
        this.publicActionLoading = false;
      },
      error: () => {
        this.publicActionError = 'Public link üretilemedi.';
        this.publicActionLoading = false;
      }
    });
  }

  rotatePublicLink() {
    if (this.publicActionLoading) return;
    this.publicActionLoading = true;
    this.publicActionMessage = null;
    this.publicActionError = null;

    const selectedIds = this.getSelectedCatalogIdList();
    this.catalogService.rotatePublicToken(selectedIds.length ? selectedIds : undefined).subscribe({
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
      this.publicActionMessage = null;
      this.publicActionError = 'Link kopyalanamadı.';
    }
  }

  // Dosya Seçme Simülasyonu 
  // (Backend olmadan dosyayı tarayıcıda önizlemek için)
  onFileSelected(event: any) {
    const file = event.target.files[0];
    if (file) {
      // Dosyadan geçici bir URL oluşturuyoruz
      const fakeUrl = URL.createObjectURL(file);
      
      this.newMedia.url = fakeUrl;
      // Dosya tipine göre video mu resim mi karar veriyoruz
      this.newMedia.type = file.type.includes('video') ? 'video' : 'image';
    }
  }

  // Listeye Ekleme
  addMedia() {
    if (!this.newMedia.url) return;

    // Yeni öğeyi listenin en başına ekle (unshift)
    this.showcaseItems.unshift({
      id: Date.now().toString(), // Benzersiz ID
      type: this.newMedia.type || 'image',
      url: this.newMedia.url!,
      title: this.newMedia.title,
      subtitle: this.newMedia.subtitle
    });

    // Ekleme bitince formu temizle
    this.newMedia = { type: 'image', title: '', subtitle: '', url: '' };
  }

  // Listeden Silme
  deleteMedia(id: string) {
    this.showcaseItems = this.showcaseItems.filter(item => item.id !== id);
  }

  // Genel Kayıt
  saveSettings() {
    if (this.activeTab === 'general') {
      this.saveProfile();
      return;
    }

    if (this.activeTab === 'plan') {
      this.planSuccess = this.planSuccess ?? 'Plan değişiklikleri anlık uygulanır.';
      return;
    }

    if (this.activeTab === 'showcase') {
      alert('Vitrin yönetimi bu aşamada local state ile çalışıyor. Kalıcı kayıt backend adımı sonraki geliştirmede eklenecek.');
      return;
    }

    alert('Bu sekmede değişiklikler canlıdır veya ayrı endpoint ile yönetilir.');
  }

  private saveProfile() {
    const firstName = this.profile.firstName.trim();
    const lastName = this.profile.lastName.trim();
    if (!firstName || !lastName) {
      this.profileError = 'Ad ve soyad zorunludur.';
      this.profileSuccess = null;
      return;
    }

    this.isProfileSaving = true;
    this.profileError = null;
    this.profileSuccess = null;

    this.authService.updateMe({
      firstName,
      lastName,
      companyName: this.profile.companyName?.trim() || null,
      phoneNumber: this.profile.phoneNumber?.trim() || null
    }).subscribe({
      next: (user) => {
        this.profile.firstName = user.firstName || '';
        this.profile.lastName = user.lastName || '';
        this.profile.email = user.email || '';
        this.profile.companyName = user.companyName || '';
        this.profile.phoneNumber = user.phoneNumber || '';
        this.profileSuccess = 'Profil bilgileri kaydedildi.';
        this.isProfileSaving = false;
      },
      error: (err) => {
        this.profileError = typeof err?.error === 'string'
          ? err.error
          : (err?.error?.message ?? 'Profil kaydedilemedi.');
        this.profileSuccess = null;
        this.isProfileSaving = false;
      }
    });
  }
}
