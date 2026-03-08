import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { Router } from '@angular/router';
import {
  CatalogService,
  EmbedDomainVerification,
  EmbedSettings,
  EmbedStoreSlugCheckResponse,
  EmbedVerifyOriginResponse,
  PublicTokenStatus
} from '../../core/services/catalog.service';
import { AuthService } from '../../core/services/auth.service';
import { environment } from '../../../environments/environment';

type SmokeSuiteKey = 'html' | 'wordpress' | 'shopify';

interface SmokeSuite {
  key: SmokeSuiteKey;
  title: string;
  summary: string;
  snippetType: 'html' | 'wordpress' | 'shopify';
  steps: string[];
}

@Component({
  selector: 'app-embed-integration',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './embed-integration.html',
  styleUrl: './embed-integration.css'
})
export class EmbedIntegrationComponent implements OnInit {
  private catalogService = inject(CatalogService);
  private sanitizer = inject(DomSanitizer);
  private authService = inject(AuthService);
  private router = inject(Router);

  publicToken: string | null = null;
  publicTokenStatus: PublicTokenStatus | null = null;
  storeSlug = '';
  storeSlugCheck: EmbedStoreSlugCheckResponse | null = null;
  storeSlugChecking = false;
  private storeSlugCheckTimer: ReturnType<typeof setTimeout> | null = null;
  private lastCheckedStoreSlug = '';

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

  smokeChecklistState: Record<string, boolean> = {};
  readonly smokeSuites: SmokeSuite[] = [
    {
      key: 'html',
      title: 'Duz HTML Testi',
      summary: 'En hizli teknik dogrulama. Ayrik bir test sayfasinda iframe yuklenmesini kontrol eder.',
      snippetType: 'html',
      steps: [
        'Snippeti bos bir HTML sayfasina yapistir.',
        'Sayfayi farkli bir origin veya local server uzerinden ac.',
        'Iframe ilk yuklemede kirpilmadan aciliyor mu kontrol et.',
        'Chat acildiginda ve viewera girdiginde yukseklik otomatik guncelleniyor mu bak.'
      ]
    },
    {
      key: 'wordpress',
      title: 'WordPress Testi',
      summary: 'Gutenberg veya Elementor icinde gercek editor davranisini kontrol eder.',
      snippetType: 'wordpress',
      steps: [
        'WordPress sayfasinda Ozel HTML veya HTML widget ac.',
        'WordPress snippetini yapistir ve sayfayi guncelle.',
        'Canli sayfada vitrin aciliyor mu kontrol et.',
        'Mobilde ve masaustunde iframe kirpilma veya cift scroll yapiyor mu bak.'
      ]
    },
    {
      key: 'shopify',
      title: 'Shopify Testi',
      summary: 'Tema dosyasina eklenen scriptin magaza sayfasinda sorunsuz calistigini dogrular.',
      snippetType: 'shopify',
      steps: [
        'Snippeti ilgili sectiona veya theme.liquid icinde body sonuna ekle.',
        'Storefront sayfasini ac ve iframe render aliyor mu kontrol et.',
        'Sepet veya checkout gecislerinde yukseklik guncelleniyor mu bak.',
        'Plan 1-2 ise Powered by Partalog rozeti gorunuyor mu kontrol et.'
      ]
    }
  ];

  get isSnippetReady(): boolean {
    return !!this.publicToken || !!this.normalizedStoreSlug;
  }

  get currentPlan(): number {
    return this.authService.getCurrentPlan();
  }

  get canUseEmbed(): boolean {
    return this.currentPlan >= 1;
  }

  get showPoweredByBadgeInfo(): boolean {
    return this.currentPlan < 3;
  }

  get hasVerifiedDomain(): boolean {
    return this.embedDomains.some((x) => x.status === 'verified');
  }

  get smokeProgress(): { done: number; total: number } {
    const total = this.smokeSuites.reduce((sum, suite) => sum + suite.steps.length, 0);
    const done = Object.values(this.smokeChecklistState).filter(Boolean).length;
    return { done, total };
  }

  get embedReadinessItems(): Array<{ label: string; ok: boolean; text: string }> {
    return [
      {
        label: 'Mağaza kodu',
        ok: !!this.normalizedStoreSlug,
        text: this.normalizedStoreSlug || 'Henüz tanımlanmadı'
      },
      {
        label: 'Public link',
        ok: !!this.publicToken,
        text: this.publicToken ? 'Hazır' : 'Henüz üretilmedi'
      },
      {
        label: 'Doğrulanmış domain',
        ok: this.hasVerifiedDomain,
        text: this.hasVerifiedDomain ? 'En az 1 domain doğrulandı' : 'Domain doğrulaması bekleniyor'
      },
      {
        label: 'Embed durumu',
        ok: this.canUseEmbed && (!!this.normalizedStoreSlug || !!this.publicToken),
        text: this.canUseEmbed
          ? ((this.normalizedStoreSlug || this.publicToken) ? 'Kopyalanabilir' : 'Kimlik bilgisi eksik')
          : 'Embed kullanılamıyor'
      }
    ];
  }

  get hasLastVerifyResult(): boolean {
    return !!this.embedVerifyResult;
  }

  get lastVerifySummary(): { ok: boolean; title: string; lines: string[] } | null {
    const result = this.embedVerifyResult;
    if (!result) return null;

    const lines = [
      `Domain: ${result.origin || this.embedVerifyOriginInput || '-'}`,
      `Neden: ${result.reason || '-'}`,
      `Branding: ${result.whiteLabel ? 'White-label aktif' : 'Powered by Partalog görünür'}`
    ];

    if (result.appBaseUrl) {
      lines.push(`App URL: ${result.appBaseUrl}`);
    }

    if (result.storeSlug) {
      lines.push(`Mağaza kodu: ${result.storeSlug}`);
    }

    return {
      ok: result.allowed,
      title: result.allowed ? 'Son origin testi başarılı' : 'Son origin testi başarısız',
      lines
    };
  }

  ngOnInit(): void {
    this.loadPublicLinkState();
    this.loadEmbedSettings();
    this.loadEmbedDomains();
  }

  loadPublicLinkState() {
    this.embedError = null;
    this.catalogService.getPublicTokenStatus().subscribe({
      next: (status) => {
        this.publicTokenStatus = status;
        if (!status.enabled) {
          this.publicToken = null;
          return;
        }
        this.catalogService.getPublicToken().subscribe({
          next: (res) => {
            this.publicToken = res.token;
          },
          error: () => {
            this.publicToken = null;
            this.embedError = 'Public link alınamadı. Önce Public Link sekmesinden üretin.';
          }
        });
      },
      error: () => {
        this.publicTokenStatus = null;
        this.publicToken = null;
      }
    });
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
        this.storeSlug = settings.storeSlug || '';
        this.storeSlugCheck = settings.storeSlug
          ? {
              normalized: settings.storeSlug,
              current: settings.storeSlug,
              available: true,
              suggested: settings.storeSlug
            }
          : null;
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

  addEmbedOrigin() {
    const normalized = this.normalizeOrigin(this.embedOriginInput);
    if (!normalized) {
      this.embedError = 'Geçerli bir domain girin. Örn: https://www.site.com';
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

  createEmbedDomainChallenge() {
    const normalized = this.normalizeOrigin(this.embedOriginInput);
    if (!normalized) {
      this.embedError = 'Challenge için geçerli bir domain girin. Örn: https://www.site.com';
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
        this.embedMessage = 'Domain challenge oluşturuldu. Aşağıdaki adımları tamamlayın.';
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

  saveEmbedSettings() {
    this.embedSaving = true;
    this.embedMessage = null;
    this.embedError = null;
    this.catalogService.updateEmbedSettings({
      allowedOrigins: this.embedAllowedOrigins,
      theme: this.embedTheme,
      mode: this.embedMode,
      storeSlug: this.normalizeStoreSlug(this.storeSlug)
    }).subscribe({
      next: (settings) => {
        this.embedSettings = settings;
        this.embedAllowedOrigins = [...(settings.allowedOrigins || [])];
        this.embedTheme = settings.theme || 'default';
        this.embedMode = settings.mode || 'catalog';
        this.storeSlug = settings.storeSlug || '';
        this.storeSlugCheck = settings.storeSlug
          ? {
              normalized: settings.storeSlug,
              current: settings.storeSlug,
              available: true,
              suggested: settings.storeSlug
            }
          : null;
        this.embedSaving = false;
        this.embedMessage = 'Entegrasyon ayarları kaydedildi.';
      },
      error: (err) => {
        this.embedSaving = false;
        this.embedError = typeof err?.error === 'string'
          ? err.error
          : (err?.error?.message ?? 'Entegrasyon ayarları kaydedilemedi.');
      }
    });
  }

  async copyEmbedScript() {
    const storeSlug = this.normalizedStoreSlug;
    if (!storeSlug && !this.publicToken) {
      this.embedError = 'Önce mağaza kodu oluşturun veya public link üretin.';
      return;
    }

    const apiRoot = this.getApiRoot();
    const identityAttr = storeSlug
      ? `data-store="${storeSlug}"`
      : `data-public-token="${this.publicToken}"`;
    const scriptText = `<div id="partalog-embed-root"></div>\n<script src="${apiRoot}/embed.js"\n  ${identityAttr}\n  data-api-base-url="${apiRoot}"\n  data-height="780px"></script>`;

    try {
      await navigator.clipboard.writeText(scriptText);
      this.embedMessage = 'Embed script panoya kopyalandı.';
      this.embedError = null;
    } catch {
      this.embedError = 'Script kopyalanamadı.';
      this.embedMessage = null;
    }
  }

  toggleSmokeStep(suiteKey: SmokeSuiteKey, stepIndex: number) {
    const key = `${suiteKey}:${stepIndex}`;
    this.smokeChecklistState[key] = !this.smokeChecklistState[key];
  }

  isSmokeStepDone(suiteKey: SmokeSuiteKey, stepIndex: number): boolean {
    return !!this.smokeChecklistState[`${suiteKey}:${stepIndex}`];
  }

  getSmokeSuiteDoneCount(suiteKey: SmokeSuiteKey): number {
    const suite = this.smokeSuites.find((x) => x.key === suiteKey);
    if (!suite) return 0;
    return suite.steps.filter((_, index) => this.isSmokeStepDone(suiteKey, index)).length;
  }

  async copySnippet(type: 'html' | 'wordpress' | 'shopify') {
    if (!this.isSnippetReady) {
      this.embedError = 'Önce mağaza kodu oluşturun veya public link üretin.';
      return;
    }
    const map = {
      html: this.htmlSnippet,
      wordpress: this.wordpressSnippet,
      shopify: this.shopifySnippet
    };
    try {
      await navigator.clipboard.writeText(map[type]);
      this.embedMessage = 'Kod panoya kopyalandı.';
      this.embedError = null;
    } catch {
      this.embedError = 'Kod kopyalanamadı.';
      this.embedMessage = null;
    }
  }

  verifyEmbedOrigin() {
    if (!this.publicToken && !this.normalizedStoreSlug) {
      this.embedError = 'Önce mağaza kodu oluşturun veya public link üretin.';
      return;
    }

    const normalized = this.normalizeOrigin(this.embedVerifyOriginInput);
    if (!normalized) {
      this.embedError = 'Doğrulama için geçerli bir domain girin.';
      return;
    }

    this.embedVerifying = true;
    this.embedVerifyResult = null;
    this.embedError = null;
    this.catalogService.verifyEmbedOrigin({
      publicToken: this.publicToken || undefined,
      storeSlug: this.normalizedStoreSlug || undefined,
      origin: normalized
    }).subscribe({
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

  get htmlSnippet(): string {
    const apiRoot = this.getApiRoot();
    const identityAttr = this.normalizedStoreSlug
      ? `data-store="${this.normalizedStoreSlug}"`
      : `data-public-token="${this.publicToken ?? '<PUBLIC_TOKEN>'}"`;
    return `<div id="partalog-embed-root"></div>
<script src="${apiRoot}/embed.js"
  ${identityAttr}
  data-api-base-url="${apiRoot}"
  data-height="780px"></script>`;
  }

  get wordpressSnippet(): string {
    return `<!-- WordPress: Özel HTML bloğuna yapıştır -->
${this.htmlSnippet}`;
  }

  get shopifySnippet(): string {
    return `{%- comment -%} Shopify: theme.liquid dosyasında </body> öncesine ekleyin {%- endcomment -%}
${this.htmlSnippet}`;
  }

  get localTestSnippet(): string {
    const apiRoot = this.getApiRoot();
    const identityAttr = this.normalizedStoreSlug
      ? `data-store="${this.normalizedStoreSlug}"`
      : `data-public-token="${this.publicToken ?? '<PUBLIC_TOKEN>'}"`;
    return `<div id="partalog-embed-root"></div>
<script src="${apiRoot}/embed.js"
  ${identityAttr}
  data-api-base-url="${apiRoot}"
  data-app-base-url="${window.location.origin}"
  data-target="#partalog-embed-root"
  data-height="820px"></script>`;
  }

  get prodTestSnippet(): string {
    return this.htmlSnippet;
  }

  goUpgrade() {
    this.router.navigate(['/upgrade'], {
      queryParams: {
        requiredPlan: 2,
        feature: 'embed'
      }
    });
  }

  async copyTestSnippet(mode: 'local' | 'prod') {
    const text = mode === 'local' ? this.localTestSnippet : this.prodTestSnippet;
    try {
      await navigator.clipboard.writeText(text);
      this.embedMessage = mode === 'local'
        ? 'Local test snippet panoya kopyalandı.'
        : 'Prod test snippet panoya kopyalandı.';
      this.embedError = null;
    } catch {
      this.embedError = 'Test snippet kopyalanamadı.';
      this.embedMessage = null;
    }
  }

  get normalizedStoreSlug(): string {
    return this.normalizeStoreSlug(this.storeSlug);
  }

  checkStoreSlug() {
    const normalizedSlug = this.normalizedStoreSlug;
    if (!normalizedSlug) {
      this.storeSlugCheck = null;
      this.storeSlugChecking = false;
      this.lastCheckedStoreSlug = '';
      return;
    }

    if (normalizedSlug === this.lastCheckedStoreSlug && this.storeSlugCheck) {
      this.storeSlug = normalizedSlug;
      return;
    }

    this.storeSlugChecking = true;
    this.embedError = null;
    this.catalogService.checkEmbedStoreSlug(normalizedSlug).subscribe({
      next: (result) => {
        this.storeSlugCheck = result;
        this.lastCheckedStoreSlug = normalizedSlug;
        this.storeSlugChecking = false;
      },
      error: (err) => {
        this.storeSlugChecking = false;
        this.embedError = typeof err?.error === 'string'
          ? err.error
          : (err?.error?.message ?? 'Mağaza kodu kontrol edilemedi.');
      }
    });
  }

  onStoreSlugChange() {
    this.lastCheckedStoreSlug = '';

    if (this.storeSlugCheckTimer) {
      clearTimeout(this.storeSlugCheckTimer);
      this.storeSlugCheckTimer = null;
    }

    if (!this.normalizedStoreSlug) {
      this.storeSlugCheck = null;
      this.storeSlugChecking = false;
      return;
    }

    this.storeSlugCheckTimer = setTimeout(() => {
      this.checkStoreSlug();
    }, 350);
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

  private normalizeStoreSlug(value: string): string {
    return String(value || '')
      .trim()
      .toLowerCase()
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/-{2,}/g, '-')
      .replace(/^-|-$/g, '')
      .slice(0, 96);
  }
}
