import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms'; // 🔥 HTML'de ngModel kullandığımız için şart
import { Catalog, CatalogService, PublicTokenStatus, ShowcaseMedia } from '../../core/services/catalog.service'; // Interface'i import ettik

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, FormsModule], 
  templateUrl: './settings.html',
  styleUrl: './settings.css'
})
export class SettingsComponent implements OnInit {
  private catalogService = inject(CatalogService);

  // Aktif sekme (Type güvenliği için string literal kullandık)
  activeTab: 'general' | 'security' | 'notifications' | 'showcase' | 'public' = 'general';

  // --- PUBLIC LINK VERİLERİ ---
  publicToken: string | null = null;
  publicTokenStatus: PublicTokenStatus | null = null;
  publicActionLoading = false;
  publicActionMessage: string | null = null;
  publicActionError: string | null = null;
  publishedCatalogs: Catalog[] = [];
  selectedCatalogIds = new Set<string>();

  // --- VITRIN (SHOWCASE) VERİLERİ ---

  // Mevcut Vitrin Listesi (Başlangıçta boş görünmesin diye örnek veri koyduk)
  showcaseItems: ShowcaseMedia[] = [
    {
      id: '1',
      type: 'image',
      url: 'https://images.unsplash.com/photo-1486262715619-01b80250e0dc?auto=format&fit=crop&q=80&w=1600',
      title: '2026 Yeni Motor Serisi',
      subtitle: 'Performans ve dayanıklılık bir arada.'
    }
  ];

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
    this.loadPublishedCatalogs();
    this.loadPublicLinkState();
  }

  setActiveTab(tabName: 'general' | 'security' | 'notifications' | 'showcase' | 'public') {
    this.activeTab = tabName;
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
    const url = `${window.location.origin}/public-view/${this.publicToken}`;
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
    // Gerçek uygulamada burada servise data gönderilir
    console.log('Kaydedilen Vitrin Verisi:', this.showcaseItems);
    alert('Tüm ayarlar ve vitrin düzenlemeleri başarıyla kaydedildi!');
  }
}
