import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { Router } from '@angular/router';
import {
  CatalogService,
  EmbedDomainVerification,
  EmbedSettings,
  EmbedVerifyOriginResponse,
  PublicTokenStatus
} from '../../core/services/catalog.service';
import { AuthService } from '../../core/services/auth.service';
import { environment } from '../../../environments/environment';

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

  get isSnippetReady(): boolean {
    return !!this.publicToken && this.canUseEmbed;
  }

  get currentPlan(): number {
    return this.authService.getCurrentPlan();
  }

  get canUseEmbed(): boolean {
    return this.currentPlan >= 2;
  }

  get showPoweredByBadgeInfo(): boolean {
    return this.currentPlan === 2;
  }

  ngOnInit(): void {
    if (!this.canUseEmbed) {
      return;
    }
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
      mode: this.embedMode
    }).subscribe({
      next: (settings) => {
        this.embedSettings = settings;
        this.embedAllowedOrigins = [...(settings.allowedOrigins || [])];
        this.embedTheme = settings.theme || 'default';
        this.embedMode = settings.mode || 'catalog';
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
    if (!this.publicToken) {
      this.embedError = 'Önce public link üretin.';
      return;
    }

    const apiRoot = this.getApiRoot();
    const scriptText = `<div id="partalog-embed-root"></div>\n<script src="${apiRoot}/embed.js"\n  data-public-token="${this.publicToken}"\n  data-api-base-url="${apiRoot}"\n  data-height="780px"></script>`;

    try {
      await navigator.clipboard.writeText(scriptText);
      this.embedMessage = 'Embed script panoya kopyalandı.';
      this.embedError = null;
    } catch {
      this.embedError = 'Script kopyalanamadı.';
      this.embedMessage = null;
    }
  }

  async copySnippet(type: 'html' | 'wordpress' | 'shopify') {
    if (!this.isSnippetReady) {
      this.embedError = 'Önce public link üretin.';
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
    if (!this.publicToken) {
      this.embedError = 'Önce public link üretin.';
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

  get htmlSnippet(): string {
    const apiRoot = this.getApiRoot();
    const token = this.publicToken ?? '<PUBLIC_TOKEN>';
    return `<div id="partalog-embed-root"></div>
<script src="${apiRoot}/embed.js"
  data-public-token="${token}"
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

  goUpgrade() {
    this.router.navigate(['/upgrade'], {
      queryParams: {
        requiredPlan: 2,
        feature: 'embed'
      }
    });
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
}
