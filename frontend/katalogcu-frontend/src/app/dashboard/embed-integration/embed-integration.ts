import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  Catalog,
  CatalogService,
  EmbedDomainVerification,
  EmbedSettings,
  EmbedTarget,
  EmbedTargetRequest,
  EmbedVerifyOriginResponse,
  PublicTokenStatus
} from '../../core/services/catalog.service';
import { AuthService } from '../../core/services/auth.service';
import { environment } from '../../../environments/environment';

type IntegrationModeGuide = {
  mode: NonNullable<EmbedTargetRequest['hostActionMode']> | 'catalog_only';
  title: string;
  summary: string;
  businessTask: string;
  developerTask: string;
  codeLevel: string;
  recommendedFor: string;
};

type DeveloperHandoffCard = {
  title: string;
  businessDoes: string;
  developerDoes: string;
  codeNeed: string;
  deliverables: string[];
};

type StockCartDeveloperPacket = {
  title: string;
  summary: string;
  businessInputs: string[];
  developerTasks: string[];
  fieldMapping: string[];
  testChecklist: string[];
};

type PlatformNote = {
  key: 'woocommerce' | 'ideasoft' | 'ticimax';
  title: string;
  status: 'ready' | 'likely';
  summary: string;
  recommendedMode: string;
  businessNote: string;
  developerNote: string;
};

type IntegrationWorkspacePanel = 'domains' | 'builder' | 'packages';
type PackageViewTab = 'brief' | 'code' | 'examples';

@Component({
  selector: 'app-embed-integration',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './embed-integration.html',
  styleUrl: './embed-integration.css'
})
export class EmbedIntegrationComponent implements OnInit {
  private catalogService = inject(CatalogService);
  private authService = inject(AuthService);

  publicToken: string | null = null;
  publicTokenStatus: PublicTokenStatus | null = null;
  storeSlug = '';

  catalogs: Catalog[] = [];
  embedTargets: EmbedTarget[] = [];
  selectedEmbedTargetId: string | null = null;
  targetsLoading = false;
  targetSaving = false;
  targetError: string | null = null;
  targetMessage: string | null = null;
  targetForm: EmbedTargetRequest = {
    name: '',
    type: 'catalog',
    catalogId: '',
    catalogPageId: null,
    commerceMode: 'catalog_only',
    hostActionMode: 'none',
    productUrlTemplate: null,
    searchUrlTemplate: null,
    existingCartUrl: null,
    existingCartMethod: 'POST',
    accessExpiresAt: null,
    isActive: true
  };

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
  activeWorkspacePanel: IntegrationWorkspacePanel = 'domains';
  activePackageTab: PackageViewTab = 'brief';

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

  get showEmptyStartState(): boolean {
    return this.embedTargets.length === 0 && this.embedDomains.length === 0;
  }

  get recommendedStrategyLabel(): string {
    return this.targetForm.commerceMode === 'catalog_only'
      ? 'Sadece Katalog'
      : this.getHostActionModeLabel(this.targetForm.hostActionMode);
  }

  get selectedModeGuide(): IntegrationModeGuide {
    if (this.targetForm.commerceMode === 'catalog_only') {
      return this.getIntegrationModeGuide('catalog_only');
    }

    return this.getIntegrationModeGuide(this.targetForm.hostActionMode || 'search_redirect');
  }

  get integrationModeGuides(): IntegrationModeGuide[] {
    return [
      this.getIntegrationModeGuide('catalog_only'),
      this.getIntegrationModeGuide('search_redirect'),
      this.getIntegrationModeGuide('product_redirect'),
      this.getIntegrationModeGuide('existing_cart_api'),
      this.getIntegrationModeGuide('existing_cart_js')
    ];
  }

  get workspacePanels(): Array<{ id: IntegrationWorkspacePanel; label: string; hint: string; locked?: boolean }> {
    return [
      { id: 'domains', label: 'Domain', hint: 'Hangi sitelerde calissin' },
      { id: 'builder', label: 'Embed Olustur', hint: 'Katalog ve calisma sekli' },
      { id: 'packages', label: 'Gelistirici Paketi', hint: 'Kodlar ve hazir metin', locked: this.embedTargets.length === 0 }
    ];
  }

  get platformNotes(): PlatformNote[] {
    return [
      {
        key: 'woocommerce',
        title: 'WooCommerce',
        status: 'ready',
        summary: 'Hazir plugin paketi ile kurulabilir.',
        recommendedMode: 'WooCommerce Sepete Ekle',
        businessNote: 'Musteri zip paketi indirir ve WordPress tarafinda kurdurur.',
        developerNote: 'Plugin kurulur, ayarlar doldurulur ve [partalog_embed] shortcode eklenir.'
      },
      {
        key: 'ideasoft',
        title: 'IdeaSoft',
        status: 'ready',
        summary: 'Script/HTML alanlari ve API yapisi oldugu icin uygulanabilir.',
        recommendedMode: 'Sitede Ara veya Urun Sayfasina Git',
        businessNote: 'Musteri arama veya urun sayfasi akisini secerek daha hizli canliya cikabilir.',
        developerNote: 'Tema veya uygun kod ekleme alanina embed ve host adapter eklenir. Cart entegrasyonu tema yapisina gore ayrica kontrol edilir.'
      },
      {
        key: 'ticimax',
        title: 'Ticimax',
        status: 'likely',
        summary: 'Esnek entegrasyon altyapisi var, ancak cart baglantisi tema implementasyonunda dogrulanmali.',
        recommendedMode: 'Sitede Ara veya Urun Sayfasina Git',
        businessNote: 'Ilk kurulumda yonlendirme modlari ile baslamak daha guvenlidir.',
        developerNote: 'Tema veya ozel kod alaninda embed kullanilir. Cart veya JS akisi kullanilacaksa mevcut tema yapisi incelenmelidir.'
      }
    ];
  }

  get wooCommerceInstallSteps(): string[] {
    return [
      'WooCommerce test veya canli magazanda Plugins > Add New > Upload Plugin adimina gir.',
      'Partalog WooCommerce paketini indirip yukle ve aktiflestir.',
      'Settings > Partalog WooCommerce ekraninda API Base URL ve Embed Key alanlarini doldur.',
      'Calisma modunu sec: Sitede Ara, Urun Sayfasina Git veya WooCommerce Sepete Ekle.',
      'Partalog gostermek istedigin sayfaya [partalog_embed] shortcode\'unu ekle.',
      'Gercek bir SKU/partCode ile katalog, yonlendirme ve sepet akisini test et.'
    ];
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

  ngOnInit(): void {
    this.loadPublicLinkState();
    this.loadEmbedSettings();
    this.loadEmbedDomains();
    this.loadCatalogs();
    this.loadEmbedTargets();
  }

  get publishedCatalogs(): Catalog[] {
    return (this.catalogs || []).filter(x => String(x.status || '').toLowerCase() === 'published');
  }

  get selectedTargetCatalog(): Catalog | null {
    return this.publishedCatalogs.find(x => x.id === this.targetForm.catalogId) ?? null;
  }

  get selectedEmbedTarget(): EmbedTarget | null {
    return this.embedTargets.find(x => x.id === this.selectedEmbedTargetId) ?? null;
  }

  get selectedTargetPages() {
    return (this.selectedTargetCatalog?.pages || []).slice().sort((a, b) => a.pageNumber - b.pageNumber);
  }

  loadCatalogs() {
    this.catalogService.getCatalogs().subscribe({
      next: (catalogs) => {
        this.catalogs = catalogs || [];
      },
      error: () => {
        this.catalogs = [];
      }
    });
  }

  loadEmbedTargets() {
    this.targetsLoading = true;
    this.catalogService.getEmbedTargets().subscribe({
      next: (targets) => {
        this.embedTargets = targets || [];
        this.selectedEmbedTargetId = this.selectedEmbedTargetId && this.embedTargets.some(x => x.id === this.selectedEmbedTargetId)
          ? this.selectedEmbedTargetId
          : (this.embedTargets[0]?.id ?? null);
        this.targetsLoading = false;
      },
      error: () => {
        this.embedTargets = [];
        this.targetsLoading = false;
      }
    });
  }

  setActiveWorkspacePanel(panel: IntegrationWorkspacePanel) {
    if (panel === 'packages' && this.embedTargets.length === 0) {
      return;
    }

    this.activeWorkspacePanel = panel;
  }

  setActivePackageTab(tab: PackageViewTab) {
    this.activePackageTab = tab;
  }

  onTargetCatalogChange() {
    if (this.targetForm.type === 'catalog_page') {
      const firstPageId = this.selectedTargetPages[0]?.id ?? null;
      this.targetForm.catalogPageId = firstPageId;
      return;
    }

    this.targetForm.catalogPageId = null;
  }

  onTargetTypeChange() {
    if (this.targetForm.type === 'catalog_page') {
      this.targetForm.catalogPageId = this.selectedTargetPages[0]?.id ?? null;
      return;
    }

    this.targetForm.catalogPageId = null;
  }

  onTargetCommerceModeChange() {
    if (this.targetForm.commerceMode === 'catalog_only') {
      this.targetForm.hostActionMode = 'none';
      this.targetForm.productUrlTemplate = null;
      this.targetForm.searchUrlTemplate = null;
      this.targetForm.existingCartUrl = null;
      this.targetForm.existingCartMethod = 'POST';
      return;
    }

    if (this.targetForm.hostActionMode === 'none' || !this.targetForm.hostActionMode) {
      this.targetForm.hostActionMode = 'search_redirect';
    }

    this.applyRecommendedDefaults();
  }

  onTargetHostActionModeChange() {
    if (this.targetForm.hostActionMode !== 'product_redirect') {
      this.targetForm.productUrlTemplate = null;
    }

    if (this.targetForm.hostActionMode !== 'search_redirect') {
      this.targetForm.searchUrlTemplate = null;
    }

    if (this.targetForm.hostActionMode !== 'existing_cart_api') {
      this.targetForm.existingCartUrl = null;
      this.targetForm.existingCartMethod = 'POST';
    }

    this.applyRecommendedDefaults();
  }

  applyRecommendedDefaults() {
    if (this.targetForm.commerceMode === 'catalog_only') {
      return;
    }

    if (this.targetForm.hostActionMode === 'search_redirect' && !String(this.targetForm.searchUrlTemplate || '').trim()) {
      this.targetForm.searchUrlTemplate = '/search?q={partCode}';
    }

    if (this.targetForm.hostActionMode === 'product_redirect' && !String(this.targetForm.productUrlTemplate || '').trim()) {
      this.targetForm.productUrlTemplate = '/urun/{partCode}';
    }

    if (this.targetForm.hostActionMode === 'existing_cart_api' && !String(this.targetForm.existingCartUrl || '').trim()) {
      this.targetForm.existingCartUrl = '/cart/add.js';
      this.targetForm.existingCartMethod = 'POST';
    }
  }

  useRecommendedStrategy(mode: NonNullable<EmbedTargetRequest['hostActionMode']>) {
    if (this.targetForm.commerceMode === 'catalog_only') {
      this.targetForm.commerceMode = 'host_cart';
    }

    this.targetForm.hostActionMode = mode;
    this.onTargetHostActionModeChange();
  }

  getHostActionModeLabel(mode: EmbedTarget['hostActionMode'] | EmbedTargetRequest['hostActionMode'] | null | undefined): string {
    return ({
      none: 'Aksiyon Yok',
      product_redirect: 'Urun Sayfasina Git',
      search_redirect: 'Sitede Ara',
      existing_cart_api: 'Mevcut Cart API',
      existing_cart_js: 'Mevcut JS Fonksiyonu',
      custom: 'Custom Handler'
    } as const)[mode || 'none'] || 'Aksiyon Yok';
  }

  getIntegrationModeGuide(mode: NonNullable<EmbedTargetRequest['hostActionMode']> | 'catalog_only'): IntegrationModeGuide {
    switch (mode) {
      case 'catalog_only':
        return {
          mode,
          title: 'Sadece Katalog',
          summary: 'Katalog sitende açılır. Stok, fiyat ve sepet butonu kullanılmaz.',
          businessTask: 'Sadece hangi katalog veya sayfanın gösterileceğine karar verir.',
          developerTask: 'Yalnızca embed kodunu ilgili sayfaya ekler.',
          codeLevel: 'Çok düşük',
          recommendedFor: 'Katalogu hızlı yayınlamak isteyen işletmeler'
        };
      case 'product_redirect':
        return {
          mode,
          title: 'Urun Sayfasina Git',
          summary: 'Kullanıcı parça seçince host sitenin ürün detay sayfasına gider.',
          businessTask: 'Ürün sayfası URL kuralını bilir. Ornek: /urun/{partCode}',
          developerTask: 'Embed ile birlikte ürün URL şablonunu bağlar. Yeni API yazmaz.',
          codeLevel: 'Çok düşük',
          recommendedFor: 'Sitede her parça için ayrı ürün sayfası olan işletmeler'
        };
      case 'existing_cart_api':
        return {
          mode,
          title: 'Mevcut Cart API',
          summary: 'Partalog, host sitenin zaten kullandığı sepet endpointine istek atar.',
          businessTask: 'Sitedeki mevcut sepet yolunu geliştiricisine iletir. Ornek: /cart/add.js',
          developerTask: 'Yeni endpoint açmaz; var olan cart API yolunu ve gerekiyorsa alan eşlemesini bağlar.',
          codeLevel: 'Düşük',
          recommendedFor: 'Zaten çalışan bir cart API\'si olan e-ticaret siteleri'
        };
      case 'existing_cart_js':
        return {
          mode,
          title: 'Mevcut JS Fonksiyonu',
          summary: 'Partalog, host sayfadaki mevcut global sepet JS fonksiyonunu çağırır.',
          businessTask: 'Sitede zaten kullanılan JS sepet akışını geliştiricisine gösterir.',
          developerTask: 'window.MyStore.cart.add benzeri mevcut fonksiyonu PartalogHostConfig ile bağlar.',
          codeLevel: 'Düşük',
          recommendedFor: 'Sepet mantığı frontend tarafında çalışan siteler'
        };
      case 'custom':
        return {
          mode,
          title: 'Custom Handler',
          summary: 'Tüm davranış host sayfadaki özel handlerlarla yönetilir.',
          businessTask: 'Özel iş akışını geliştiricisine tarif eder.',
          developerTask: 'window.PartalogHostConfig içindeki handlerları kendisi yazar.',
          codeLevel: 'Orta',
          recommendedFor: 'Özel akışı olan ileri seviye entegrasyonlar'
        };
      case 'search_redirect':
      default:
        return {
          mode: 'search_redirect',
          title: 'Sitede Ara',
          summary: 'Kullanıcı parça seçince host sitede part code ile arama sonucuna gider.',
          businessTask: 'Site içi arama adresini bilir. Ornek: /search?q={partCode}',
          developerTask: 'Embed ile birlikte arama URL şablonunu ekler. Yeni endpoint yazmaz.',
          codeLevel: 'En düşük',
          recommendedFor: 'En hızlı canlıya çıkmak isteyen işletmeler'
        };
    }
  }

  saveEmbedTarget() {
    if (!this.targetForm.catalogId) {
      this.targetError = 'Önce yayınlanmış bir katalog seçin.';
      this.targetMessage = null;
      return;
    }

    if (this.targetForm.type === 'catalog_page' && !this.targetForm.catalogPageId) {
      this.targetError = 'Tek sayfa embed için sayfa seçimi zorunlu.';
      this.targetMessage = null;
      return;
    }

    if (this.targetForm.commerceMode !== 'catalog_only') {
      if (this.targetForm.hostActionMode === 'product_redirect' && !String(this.targetForm.productUrlTemplate || '').trim()) {
        this.targetError = 'Ürün yönlendirmesi için bir URL şablonu girin. Örn: /urun/{partCode}';
        this.targetMessage = null;
        return;
      }

      if (this.targetForm.hostActionMode === 'search_redirect' && !String(this.targetForm.searchUrlTemplate || '').trim()) {
        this.targetError = 'Site içi arama için bir URL şablonu girin. Örn: /search?q={partCode}';
        this.targetMessage = null;
        return;
      }

      if (this.targetForm.hostActionMode === 'existing_cart_api' && !String(this.targetForm.existingCartUrl || '').trim()) {
        this.targetError = 'Mevcut cart API modu için endpoint veya relative URL girin.';
        this.targetMessage = null;
        return;
      }
    }

    this.targetSaving = true;
    this.targetError = null;
    this.targetMessage = null;

    this.catalogService.createEmbedTarget(this.targetForm).subscribe({
      next: (target) => {
        this.targetSaving = false;
        this.embedTargets = [target, ...this.embedTargets.filter(x => x.id !== target.id)];
        this.selectedEmbedTargetId = target.id;
        this.activeWorkspacePanel = 'packages';
        this.activePackageTab = 'brief';
        this.targetMessage = 'Yeni embed kaydı oluşturuldu.';
        this.targetForm = {
          name: '',
          type: 'catalog',
          catalogId: '',
          catalogPageId: null,
          commerceMode: 'catalog_only',
          hostActionMode: 'none',
          productUrlTemplate: null,
          searchUrlTemplate: null,
          existingCartUrl: null,
          existingCartMethod: 'POST',
          accessExpiresAt: null,
          isActive: true
        };
      },
      error: (err) => {
        this.targetSaving = false;
        this.targetError = typeof err?.error === 'string'
          ? err.error
          : (err?.error?.message ?? 'Embed kaydı oluşturulamadı.');
      }
    });
  }

  deleteEmbedTarget(target: EmbedTarget) {
    this.targetError = null;
    this.targetMessage = null;
    this.catalogService.deleteEmbedTarget(target.id).subscribe({
      next: () => {
        this.embedTargets = this.embedTargets.filter(x => x.id !== target.id);
        if (this.selectedEmbedTargetId === target.id) {
          this.selectedEmbedTargetId = this.embedTargets[0]?.id ?? null;
        }
        if (this.embedTargets.length === 0 && this.activeWorkspacePanel === 'packages') {
          this.activeWorkspacePanel = 'builder';
        }
        this.targetMessage = 'Embed kaydı silindi.';
      },
      error: () => {
        this.targetError = 'Embed kaydı silinemedi.';
      }
    });
  }

  async copyTargetScript(target: EmbedTarget) {
    const scriptText = this.getTargetEmbedSnippet(target);

    try {
      await navigator.clipboard.writeText(scriptText);
      this.targetMessage = `${target.name} için embed kodu kopyalandı.`;
      this.targetError = null;
    } catch {
      this.targetError = 'Embed kodu kopyalanamadı.';
      this.targetMessage = null;
    }
  }

  async copyTargetDeveloperBrief(target: EmbedTarget) {
    const brief = this.getTargetDeveloperBrief(target);

    try {
      await navigator.clipboard.writeText(brief);
      this.targetMessage = `${target.name} için geliştirici notu kopyalandı.`;
      this.targetError = null;
    } catch {
      this.targetError = 'Geliştirici notu kopyalanamadı.';
      this.targetMessage = null;
    }
  }

  async copyTargetImplementationGuide(target: EmbedTarget) {
    const guide = this.getTargetImplementationGuide(target);

    try {
      await navigator.clipboard.writeText(guide);
      this.targetMessage = `${target.name} için uygulama rehberi kopyalandı.`;
      this.targetError = null;
    } catch {
      this.targetError = 'Uygulama rehberi kopyalanamadı.';
      this.targetMessage = null;
    }
  }

  async copyWooCommerceGuide() {
    try {
      await navigator.clipboard.writeText(this.getWooCommerceGuideText());
      this.targetMessage = 'WooCommerce kurulum rehberi kopyalandı.';
      this.targetError = null;
    } catch {
      this.targetError = 'WooCommerce kurulum rehberi kopyalanamadı.';
      this.targetMessage = null;
    }
  }

  async copyTargetHostAdapterScript(target: EmbedTarget) {
    const text = this.getTargetHostAdapterSnippet(target);
    if (!text) {
      this.targetError = 'Bu embed için host adapter gerekmiyor.';
      this.targetMessage = null;
      return;
    }

    try {
      await navigator.clipboard.writeText(text);
      this.targetMessage = `${target.name} için host adapter kodu kopyalandı.`;
      this.targetError = null;
    } catch {
      this.targetError = 'Host adapter kodu kopyalanamadı.';
      this.targetMessage = null;
    }
  }

  getTargetPreviewUrl(target: EmbedTarget): string {
    return `${window.location.origin}/embed/runtime/${target.embedKey}`;
  }

  getTargetEmbedSnippet(target: EmbedTarget): string {
    const apiRoot = this.getApiRoot();
    return `<div id="partalog-embed-root"></div>
<script src="${apiRoot}/embed.js"
  data-embed-key="${target.embedKey}"
  data-api-base-url="${apiRoot}"
  data-height="780px"></script>`;
  }

  getTargetHostAdapterSnippet(target: EmbedTarget): string {
    if (target.commerceMode === 'catalog_only') {
      return '';
    }

    const apiRoot = this.getApiRoot();
    const attrs: string[] = [];

    if (target.commerceMode === 'host_availability_cart') {
      attrs.push(`data-availability-url="/api/partalog/availability"`);
    }

    if (target.hostActionMode === 'existing_cart_api' && target.existingCartUrl) {
      attrs.push(`data-add-to-cart-url="${target.existingCartUrl}"`);
      attrs.push(`data-add-to-cart-method="${target.existingCartMethod || 'POST'}"`);
    }

    if (target.hostActionMode === 'product_redirect' && target.productUrlTemplate) {
      attrs.push(`data-product-url-template="${target.productUrlTemplate}"`);
    }

    if (target.hostActionMode === 'search_redirect' && target.searchUrlTemplate) {
      attrs.push(`data-search-url-template="${target.searchUrlTemplate}"`);
    }

    const attrLines = attrs.length > 0 ? `\n  ${attrs.join('\n  ')}` : '';
    const baseScript = `<script src="${apiRoot}/host-adapter.js"${attrLines}></script>`;

    if (target.hostActionMode === 'existing_cart_js') {
      return `<script>
window.PartalogHostConfig = {
  onAddToCart: function (item) {
    return window.MyStore.cart.add(item.partCode, item.quantity);
  }
};
</script>
${baseScript}`;
    }

    if (target.hostActionMode === 'custom') {
      return `<script>
window.PartalogHostConfig = {
  onAddToCart: async function (item) {
    return { success: true, message: "Host handler tetiklendi." };
  },
  onViewProduct: function (item) {
    window.location.assign("/urun/" + encodeURIComponent(item.partCode || ""));
  },
  onSearch: function (item) {
    window.location.assign("/search?q=" + encodeURIComponent(item.partCode || ""));
  }
};
</script>
${baseScript}`;
    }

    return baseScript;
  }

  getTargetAvailabilityRequestExample(target: EmbedTarget): string {
    return `POST /api/partalog/availability
{
  "catalogId": "${target.catalogId}",
  "pageNumber": ${target.pageNumber ?? 1},
  "items": [
    {
      "catalogItemId": "item-001",
      "productId": null,
      "partCode": "101139",
      "partName": "NUT"
    }
  ]
}`;
  }

  getTargetAvailabilityResponseExample(): string {
    return `{
  "items": [
    {
      "catalogItemId": "item-001",
      "partCode": "101139",
      "stockStatus": "in_stock",
      "availabilityLabel": "In stock",
      "unitPrice": 0.44,
      "currency": "EUR",
      "canAddToCart": true
    }
  ]
}`;
  }

  getTargetAddToCartRequestExample(target: EmbedTarget): string {
    if (target.hostActionMode === 'existing_cart_js' || target.hostActionMode === 'custom') {
      return `window.PartalogHostConfig.onAddToCart({
  catalogId: "${target.catalogId}",
  pageNumber: ${target.pageNumber ?? 1},
  catalogItemId: "item-001",
  partCode: "101139",
  partName: "NUT",
  quantity: 2
})`;
    }

    if (target.hostActionMode === 'product_redirect') {
      return `window.location.assign("${target.productUrlTemplate || '/urun/{partCode}'}")`;
    }

    if (target.hostActionMode === 'search_redirect') {
      return `window.location.assign("${target.searchUrlTemplate || '/search?q={partCode}'}")`;
    }

    return `POST /api/partalog/cart/add
{
  "catalogId": "${target.catalogId}",
  "pageNumber": ${target.pageNumber ?? 1},
  "catalogItemId": "item-001",
  "productId": null,
  "partCode": "101139",
  "partName": "NUT",
  "quantity": 2
}`;
  }

  getTargetActionResponseExample(target: EmbedTarget): string {
    if (target.hostActionMode === 'product_redirect' || target.hostActionMode === 'search_redirect') {
      return `{
  "success": true,
  "message": "Yonlendirme tetiklendi."
}`;
    }

    return `{
  "success": true,
  "message": "Added to cart",
  "cartCount": 3
}`;
  }

  getTargetStockCartPacket(target: EmbedTarget): StockCartDeveloperPacket {
    const addToCartBinding = target.hostActionMode === 'existing_cart_api'
      ? `Mevcut cart endpoint'i bagla: ${target.existingCartUrl || '/cart/add.js'} (${target.existingCartMethod || 'POST'})`
      : target.hostActionMode === 'existing_cart_js'
        ? 'Mevcut global JS cart fonksiyonunu window.PartalogHostConfig.onAddToCart ile bagla.'
        : target.hostActionMode === 'custom'
          ? 'Host sayfada onAddToCart ve gerekirse onAvailability handlerlarini yaz.'
          : 'Sepete ekleme akisini host sitedeki mevcut cart davranisina bagla.';

    return {
      title: 'Canli Stok + Sepete Ekle Paketi',
      summary: 'Bu embed, katalog icinde stok durumunu gosterir ve parca uygunsa host sitenin kendi sepet akisina ekler.',
      businessInputs: [
        'Embed key ve hangi katalog/sayfanin acilacagi',
        'Hangi domainde calisacagi',
        'Stok bilgisinin kaynagi: Partalog senkronu mu, host sistem mi',
        'Parca kodunun host sitede hangi alana denk geldigi: SKU, productCode, itemCode',
        'Varsa mevcut cart URL veya mevcut JS cart fonksiyon bilgisi'
      ],
      developerTasks: [
        'Embed kodunu parca katalogunun gorunecegi sayfaya ekle.',
        'Host adapter kodunu ayni sayfaya veya ortak layouta ekle.',
        addToCartBinding,
        'Stok hosttan geliyorsa availability sonucunu stockStatus, availabilityLabel ve canAddToCart alanlariyla dondur.',
        'Stok yoksa butonu pasiflestirecek veya uygun mesaj gosterecek akisi dogrula.'
      ],
      fieldMapping: [
        'partCode -> sku / productCode / itemCode',
        'quantity -> qty / amount / quantity',
        'catalogItemId -> opsiyonel satir metadata',
        'stockStatus -> in_stock | available_to_order | out_of_stock',
        'canAddToCart -> true ise buton aktif, false ise pasif',
        'availabilityLabel -> kullaniciya gosterilecek stok etiketi'
      ],
      testChecklist: [
        'Katalog dogru sayfada aciliyor',
        'Parca listesinde stok etiketi gorunuyor',
        'Stok varsa Sepete butonu aktif',
        'Stok yoksa Sepete butonu pasif veya kontrollu hata gosteriyor',
        'Sepete basinca host sitenin kendi sepet sayisi / mini cart guncelleniyor',
        'Gercek bir partCode ile en az bir kez basarili ekleme testi yapildi'
      ]
    };
  }

  getTargetActionSummary(target: EmbedTarget): string {
    switch (target.hostActionMode) {
      case 'product_redirect':
        return 'Kullanici butona bastiginda host urun detay sayfasina yonlendirilir.';
      case 'search_redirect':
        return 'Kullanici host sitede part code ile arama sonucuna yonlendirilir.';
      case 'existing_cart_api':
        return 'Host adapter mevcut cart endpointini cagirir; yeni endpoint yazmak gerekmez.';
      case 'existing_cart_js':
        return 'Host adapter mevcut global JS cart fonksiyonunu tetikler.';
      case 'custom':
        return 'Host sayfa window.PartalogHostConfig handlerlari ile davranisi kendisi tanimlar.';
      default:
        return 'Bu embed sadece katalogu gosterir.';
    }
  }

  getWooCommercePluginDownloadUrl(): string {
    return `${this.getApiRoot()}/downloads/partalog-woocommerce.zip`;
  }

  getWooCommerceGuideText(): string {
    const lines = [
      'WooCommerce Kurulum Rehberi',
      '',
      '1. Partalog WooCommerce zip paketini indir.',
      '2. WordPress admin > Plugins > Add New > Upload Plugin adimina gir.',
      '3. Zip dosyasini yukle, kur ve aktiflestir.',
      '4. Settings > Partalog WooCommerce ekraninda API Base URL ve Embed Key alanlarini doldur.',
      '5. Mod secimini yap.',
      '6. Partalog gostermek istedigin sayfaya [partalog_embed] shortcode\'unu ekle.',
      '7. SKU ile partCode eslesmesini kontrol ederek test et.',
      '',
      `Indirme linki: ${this.getWooCommercePluginDownloadUrl()}`
    ];

    return lines.join('\n');
  }

  getTargetDeveloperSteps(target: EmbedTarget): string[] {
    const steps = [
      'Önce sitede parça arama sayfası, ürün detay sayfası veya mevcut sepet akışı var mı kontrol et.',
      'Partalog embed kodunu katalogun görüneceği sayfaya ekle.',
      'Gerekliyse host adapter kodunu aynı sayfaya veya ortak layout dosyasına ekle.'
    ];

    switch (target.hostActionMode) {
      case 'search_redirect':
        steps.push(`Site içi arama yolunu bağla. Şablon: ${target.searchUrlTemplate || '/search?q={partCode}'}`);
        break;
      case 'product_redirect':
        steps.push(`Ürün detay yolunu bağla. Şablon: ${target.productUrlTemplate || '/urun/{partCode}'}`);
        break;
      case 'existing_cart_api':
        steps.push(`Sitenin mevcut cart endpoint'ini bağla. Yol: ${target.existingCartUrl || '/cart/add.js'} (${target.existingCartMethod || 'POST'})`);
        steps.push('Eğer alan isimleri farklıysa partCode ve quantity için küçük bir mapping yap.');
        break;
      case 'existing_cart_js':
        steps.push('Host sayfadaki mevcut global cart fonksiyonunu PartalogHostConfig.onAddToCart ile bağla.');
        break;
      case 'custom':
        steps.push('window.PartalogHostConfig içindeki custom handlerları doldur.');
        break;
      default:
        steps.push('Bu modda ek bir commerce bağlantısı gerekmez.');
        break;
    }

    steps.push('Son olarak canlıda test et: katalog açılıyor mu, buton doğru akışı tetikliyor mu kontrol et.');
    return steps;
  }

  getTargetDeveloperChecklist(target: EmbedTarget): string[] {
    const items = [
      'Katalogun yerleşeceği sayfa bulundu',
      'Embed kodu doğru sayfaya eklendi',
      'Domain panelde izinli ve doğrulanmış durumda'
    ];

    if (target.commerceMode !== 'catalog_only') {
      items.push('Host adapter eklendi');
    }

    if (target.hostActionMode === 'existing_cart_api') {
      items.push('Mevcut cart URL ve method doğrulandı');
    }

    if (target.hostActionMode === 'existing_cart_js') {
      items.push('Global JS cart fonksiyon adı doğrulandı');
    }

    if (target.hostActionMode === 'product_redirect') {
      items.push('Ürün URL şablonu gerçek part code ile test edildi');
    }

    if (target.hostActionMode === 'search_redirect') {
      items.push('Arama URL şablonu gerçek part code ile test edildi');
    }

    if (target.commerceMode === 'host_availability_cart') {
      items.push('Stok ve fiyat cevabı için host veri kaynağı hazır');
    }

    return items;
  }

  getTargetImplementationGuide(target: EmbedTarget): string {
    const lines = [
      'Partalog Uygulama Rehberi',
      '',
      `Embed: ${target.name}`,
      `Katalog: ${target.catalogName}${target.pageNumber ? ` / Sayfa ${target.pageNumber}` : ''}`,
      `Mod: ${this.getHostActionModeLabel(target.hostActionMode)}`,
      ...(target.accessExpiresAt ? [`Bitis: ${new Date(target.accessExpiresAt).toLocaleString('tr-TR')}`] : []),
      '',
      'Adımlar:'
    ];

    this.getTargetDeveloperSteps(target).forEach((step, index) => {
      lines.push(`${index + 1}. ${step}`);
    });

    lines.push('', 'Kontrol Listesi:');
    this.getTargetDeveloperChecklist(target).forEach((item) => {
      lines.push(`- ${item}`);
    });

    return lines.join('\n');
  }

  getTargetDeveloperHandoffCard(target: EmbedTarget): DeveloperHandoffCard {
    const commonDeliverables = [
      'Embed kodu',
      'Domain izni',
      'Secilen katalog veya sayfa bilgisi',
      ...(target.accessExpiresAt ? [`Bitis tarihi: ${new Date(target.accessExpiresAt).toLocaleString('tr-TR')}`] : [])
    ];

    switch (target.hostActionMode) {
      case 'search_redirect':
        return {
          title: 'Sitede Ara Paketi',
          businessDoes: 'Isletme sadece site ici arama adresini verir. Ornek: /search?q={partCode}',
          developerDoes: 'Embed kodunu ekler ve host adapteri mevcut arama sonucuna yonlendirir.',
          codeNeed: 'Cok dusuk',
          deliverables: [...commonDeliverables, `Arama sablonu: ${target.searchUrlTemplate || '/search?q={partCode}'}`]
        };
      case 'product_redirect':
        return {
          title: 'Urun Sayfasina Git Paketi',
          businessDoes: 'Isletme parca icin kullanilan urun detay adresini verir. Ornek: /urun/{partCode}',
          developerDoes: 'Embed kodunu ekler ve host adapteri urun detay sayfasina yonlendirir.',
          codeNeed: 'Cok dusuk',
          deliverables: [...commonDeliverables, `Urun sablonu: ${target.productUrlTemplate || '/urun/{partCode}'}`]
        };
      case 'existing_cart_api':
        return {
          title: 'Mevcut Cart API Paketi',
          businessDoes: 'Isletme mevcut sepete ekleme yolunu geliştiricisine iletir.',
          developerDoes: 'Yeni endpoint yazmaz; var olan cart URL ve methodunu baglar, gerekiyorsa partCode/quantity mapping yapar.',
          codeNeed: 'Dusuk',
          deliverables: [
            ...commonDeliverables,
            `Cart URL: ${target.existingCartUrl || '/cart/add.js'}`,
            `Method: ${target.existingCartMethod || 'POST'}`,
            'Gerekirse alan esleme notu'
          ]
        };
      case 'existing_cart_js':
        return {
          title: 'Mevcut JS Fonksiyonu Paketi',
          businessDoes: 'Isletme mevcut sepete ekleme akisinin frontend tarafinda calistigini geliştiricisine gosterir.',
          developerDoes: 'Var olan global JS cart fonksiyonunu window.PartalogHostConfig.onAddToCart ile baglar.',
          codeNeed: 'Dusuk',
          deliverables: [...commonDeliverables, 'Global JS fonksiyon adi', 'PartalogHostConfig ornegi']
        };
      case 'custom':
        return {
          title: 'Custom Handler Paketi',
          businessDoes: 'Isletme ozel akisin nasil calisacagini geliştiricisine tarif eder.',
          developerDoes: 'window.PartalogHostConfig icine onAddToCart, onSearch, onViewProduct gibi handlerlari yazar.',
          codeNeed: 'Orta',
          deliverables: [...commonDeliverables, 'Custom handler ornegi', 'Beklenen akıs notu']
        };
      default:
        return {
          title: 'Sadece Katalog Paketi',
          businessDoes: 'Isletme sadece hangi katalog veya diyagramin gosterilecegine karar verir.',
          developerDoes: 'Sadece embed kodunu sayfaya ekler. Sepet veya yonlendirme baglamaz.',
          codeNeed: 'En dusuk',
          deliverables: commonDeliverables
        };
    }
  }

  getTargetDeveloperBrief(target: EmbedTarget): string {
    const scriptText = this.getTargetEmbedSnippet(target);
    const adapterText = this.getTargetHostAdapterSnippet(target);
    const targetTypeText = target.type === 'catalog_page' ? 'tek sayfa / diyagram' : 'tek katalog';
    const strategyText = this.getTargetStrategyBrief(target);
    const availabilityText = target.commerceMode === 'host_availability_cart'
      ? 'Stok ve fiyat bilgisi de host sistemden beslenecek.'
      : 'Bu entegrasyonda stok ve fiyat zorunlu degil.';

    return `Merhaba,

Bu sayfada Partalog katalog embed'i kullanılacak.

Ne gösterilecek:
- ${targetTypeText}
- Katalog: ${target.catalogName}
${target.pageNumber ? `- Sayfa: ${target.pageNumber}\n` : ''}- Embed adı: ${target.name}
${target.accessExpiresAt ? `- Erişim bitişi: ${new Date(target.accessExpiresAt).toLocaleString('tr-TR')}\n` : ''}

Nasıl çalışacak:
- Bu embed storefront açmaz; doğrudan seçilen katalog içeriğini açar.
- ${strategyText}
- ${availabilityText}

Yapman gereken:
1. Aşağıdaki embed kodunu ilgili sayfaya ekle.
2. Eğer host adapter varsa onu da aynı sayfaya ekle.
3. Partalog event'lerini sitenin mevcut akışına bağla.

Embed kodu:
${scriptText}
${adapterText ? `\n\nHost adapter:\n${adapterText}` : ''}

Not:
- Yeni endpoint yazmak zorunlu değil.
- Mümkünse mevcut arama, ürün sayfası veya cart akışı yeniden kullanılmalı.
- Domain güvenliği Partalog panelinden yönetiliyor.`;
  }

  private getTargetStrategyBrief(target: EmbedTarget): string {
    switch (target.hostActionMode) {
      case 'search_redirect':
        return `Kullanıcı butona bastığında host sitede parça kodu ile arama yapılacak. Kullanılacak şablon: ${target.searchUrlTemplate || '/search?q={partCode}'}`;
      case 'product_redirect':
        return `Kullanıcı butona bastığında host sitedeki ürün detay sayfasına yönlendirilecek. Kullanılacak şablon: ${target.productUrlTemplate || '/urun/{partCode}'}`;
      case 'existing_cart_api':
        return `Kullanıcı butona bastığında sitenin mevcut cart endpoint'i çağrılacak. Kullanılacak URL: ${target.existingCartUrl || '/cart/add.js'} (${target.existingCartMethod || 'POST'})`;
      case 'existing_cart_js':
        return 'Kullanıcı butona bastığında host sayfadaki mevcut global JS cart fonksiyonu tetiklenecek.';
      case 'custom':
        return 'Kullanıcı aksiyonları window.PartalogHostConfig içindeki custom handlerlar üzerinden yönetilecek.';
      default:
        return 'Bu embed sadece katalog gösterimi yapacak.';
    }
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

  get normalizedStoreSlug(): string {
    return this.normalizeStoreSlug(this.storeSlug);
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
