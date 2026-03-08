import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import {
  CatalogService,
  Catalog,
  Folder,
  CatalogAiJobItem,
  CatalogAiJobSummary,
  CatalogPage,
  CatalogPageItem,
  Hotspot
} from '../../core/services/catalog.service';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-catalogs',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  templateUrl: './catalogs.html',
  styleUrl: './catalogs.css'
})
export class CatalogsComponent implements OnInit, OnDestroy {
  private catalogService = inject(CatalogService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private aiJobsPollTimer: ReturnType<typeof setInterval> | null = null;
  private readonly aiJobsTake = 20;
  private queryParamSub: Subscription | null = null;
  private pendingFocusJobId: string | null = null;

  isLoading = true;
  isProcessing = false; // AI işlemi sırasında kilit
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
  focusedJobId: string | null = null;

  // --- Veri Havuzu ---
  allCatalogs: Catalog[] = [];
  allFolders: Folder[] = [];

  // --- Görünüm Durumu (State) ---
  // DİKKAT: Backend GUID kullandığı için ID tipi 'string' oldu.
  currentFolderId: string | null = null; 
  breadcrumbs: { id: string | null, name: string }[] = [{ id: null, name: 'Ana Dizin' }];

  // Ekranda gösterilenler
  visibleFolders: Folder[] = [];
  visibleCatalogs: Catalog[] = [];
  catalogReviewStats: Record<string, CatalogReviewSnapshot> = {};

  // ✨ Sürükle Bırak için
  draggedCatalogId: string | null = null;

  ngOnInit() {
    this.queryParamSub = this.route.queryParamMap.subscribe(params => {
      const jobId = params.get('jobId');
      if (!jobId) {
        return;
      }

      this.pendingFocusJobId = jobId;
      this.focusedJobId = jobId;
      this.loadAiJobs();
    });

    this.loadData();
    this.loadAiJobs();
    this.startAiJobsPolling();
  }

  ngOnDestroy() {
    this.queryParamSub?.unsubscribe();
    this.queryParamSub = null;

    if (this.aiJobsPollTimer) {
      clearInterval(this.aiJobsPollTimer);
      this.aiJobsPollTimer = null;
    }
  }

  loadData() {
    this.isLoading = true;

    // 1. Klasörleri Çek (API: GET /api/folders)
    this.catalogService.getFolders().subscribe({
      next: (folders) => {
        this.allFolders = folders;
        
        // 2. Katalogları Çek (API: GET /api/catalogs)
        this.catalogService.getCatalogs().subscribe({
          next: (catalogs) => {
            this.allCatalogs = catalogs;
            this.updateFolderCounts();
            this.refreshView();
            this.isLoading = false;
          },
          error: (err) => {
            console.error('Katalog hatası:', err);
            this.isLoading = false;
          }
        });
      },
      error: (err) => {
        console.error('Klasör hatası:', err);
        this.isLoading = false;
      }
    });
  }

  loadAiJobs(silent = false) {
    if (!silent) {
      this.aiJobsLoading = true;
    }

    this.catalogService.getCatalogAiJobs(this.aiJobsTake).subscribe({
      next: (res) => {
        this.aiJobsSummary = res.summary ?? this.aiJobsSummary;
        this.aiJobs = res.jobs ?? [];
        this.aiJobsError = null;
        this.aiJobsLoading = false;
        this.tryFocusPendingJob();
      },
      error: (err) => {
        if (!silent) {
          this.aiJobsError = err?.error?.message || 'AI işlem listesi alınamadı.';
          this.aiJobsLoading = false;
        }
      }
    });
  }

  startAiJobsPolling() {
    if (this.aiJobsPollTimer) {
      clearInterval(this.aiJobsPollTimer);
    }

    this.aiJobsPollTimer = setInterval(() => {
      this.loadAiJobs(true);
    }, 10000);
  }

  private tryFocusPendingJob() {
    if (!this.pendingFocusJobId) {
      return;
    }

    const exists = this.aiJobs.some(x => x.jobId === this.pendingFocusJobId);
    if (!exists) {
      return;
    }

    const targetId = this.pendingFocusJobId;
    this.focusedJobId = targetId;

    setTimeout(() => {
      const row = document.getElementById(`ai-job-row-${targetId}`);
      if (!row) {
        return;
      }

      row.scrollIntoView({ behavior: 'smooth', block: 'center' });

      this.pendingFocusJobId = null;
      this.router.navigate([], {
        relativeTo: this.route,
        queryParams: { jobId: null, catalogId: null },
        queryParamsHandling: 'merge',
        replaceUrl: true
      });
    }, 120);
  }

  // --- KLASÖR İŞLEMLERİ ---

  createFolder() {
    const folderName = prompt("Yeni Klasör Adı:");
    if (!folderName) return;

    // Backend: POST /api/folders
    this.catalogService.createFolder(folderName).subscribe({
      next: (newFolder) => {
        this.allFolders.push(newFolder); // Listeye ekle
        this.refreshView();
      },
      error: (err) => alert("Klasör oluşturulamadı: " + err.message)
    });
  }

  // 🔥 YENİ: KLASÖR SİLME
  deleteFolder(folder: Folder, event: Event) {
    event.stopPropagation(); // Klasörün içine girmeyi engelle
    
    if (!confirm(`"${folder.name}" klasörünü ve görünümünü silmek istiyor musun? (İçindeki kataloglar Ana Dizin'e düşer.)`)) return;

    // Backend: DELETE /api/folders/{id}
    this.catalogService.deleteFolder(folder.id).subscribe({
      next: () => {
        // Listeden çıkar
        this.allFolders = this.allFolders.filter(f => f.id !== folder.id);
        
        // Eğer silinen klasörün içindeki kataloglar varsa, onları "Ana Dizin"e (null) çek
        // (Backend zaten FolderId'yi null yaptı, biz de UI'da güncelleyelim)
        this.allCatalogs.forEach(c => {
            if (c.folderId === folder.id) c.folderId = null; // veya undefined
        });

        this.updateFolderCounts();
        this.refreshView();
      },
      error: (err) => alert("Silme başarısız: " + err.message)
    });
  }

  enterFolder(folder: Folder) {
    this.currentFolderId = folder.id;
    this.breadcrumbs.push({ id: folder.id, name: folder.name });
    this.refreshView();
  }

  navigateToBreadcrumb(index: number) {
    this.breadcrumbs = this.breadcrumbs.slice(0, index + 1);
    this.currentFolderId = this.breadcrumbs[this.breadcrumbs.length - 1].id;
    this.refreshView();
  }

  // --- GÖRÜNÜM GÜNCELLEME ---

  refreshView() {
    // 1. Hangi Klasörleri Göstereceğiz?
    if (this.currentFolderId === null) {
      // Ana Dizindeysek: Tüm klasörleri göster
      this.visibleFolders = this.allFolders;
    } else {
      // Bir klasörün içindeysek: Alt klasör yok (Backend yapısı düz olduğu için)
      this.visibleFolders = [];
    }

    // 2. Hangi Katalogları Göstereceğiz?
    // Catalog.folderId ile CurrentFolderId eşleşmeli (null ise null, doluysa dolu)
    this.visibleCatalogs = this.allCatalogs.filter(c => c.folderId === this.currentFolderId || (this.currentFolderId === null && !c.folderId));
    this.ensureVisibleCatalogReviewStats();
  }

  updateFolderCounts() {
    this.allFolders.forEach(folder => {
      // Bu klasöre ait katalog sayısı
      const count = this.allCatalogs.filter(c => c.folderId === folder.id).length;
      folder.itemCount = count;
    });
  }

  // --- SÜRÜKLE & BIRAK (DRAG & DROP) ---

  onDragStart(event: DragEvent, catalogId: string) {
    this.draggedCatalogId = catalogId;
    if (event.dataTransfer) event.dataTransfer.effectAllowed = "move";
  }

  onDragOver(event: DragEvent) {
    event.preventDefault();
  }

  onDrop(event: DragEvent, targetFolder: Folder) {
    event.preventDefault();
    if (!this.draggedCatalogId) return;

    const catId = this.draggedCatalogId;
    const targetFolderId = targetFolder.id;

    // Backend'de güncelleme yapılması lazım (Catalog Update endpoint'i)
    // Serviste updateCatalog metodunu kullanıyoruz
    const catalog = this.allCatalogs.find(c => c.id === catId);
    if (!catalog) return;

    // Eski halini yedekle (hata olursa geri almak için)
    const oldFolderId = catalog.folderId;

    // UI'da hemen güncelle (Hız hissi için optimistic update)
    catalog.folderId = targetFolderId;
    this.updateFolderCounts();
    this.refreshView();
    this.draggedCatalogId = null;

    // Backend'e haber ver
    // (Burada moveCatalog veya updateCatalog metodu backend'e Catalog nesnesini göndermeli)
    this.catalogService.moveCatalog(catId, targetFolderId).subscribe({
      error: (err) => {
        console.error("Taşıma hatası:", err);
        // Hata olursa geri al
        catalog.folderId = oldFolderId;
        this.updateFolderCounts();
        this.refreshView();
        alert("Katalog taşınamadı.");
      }
    });
  }

  // --- YARDIMCI / STATUS ---

  getStatusText(status: string): string {
    const s = status?.toLowerCase();
    const map: any = { 
        'published': 'Yayında', 
        'processing': 'İşleniyor', 
        'uploading': 'Yükleniyor',
        'readytoprocess': 'Analiz Bekliyor',
        'ai_completed': 'Analiz Tamamlandı',
        'error': 'Hata',
        'draft': 'Taslak' 
    };
    return map[s] || 'Taslak';
  }

  getStatusClass(status: string): string {
    const s = status?.toLowerCase();
    if (s === 'published') return 'bg-green-100 text-green-700 border-green-200';
    if (s === 'ai_completed') return 'bg-teal-100 text-teal-700 border-teal-200';
    if (s === 'processing' || s === 'uploading') return 'bg-blue-100 text-blue-700 border-blue-200 animate-pulse';
    if (s === 'readytoprocess') return 'bg-purple-100 text-purple-700 border-purple-200';
    if (s === 'error') return 'bg-red-100 text-red-700 border-red-200';
    return 'bg-gray-100 text-gray-600 border-gray-200';
  }

  deleteCatalog(id: string, event: Event) {
    event.stopPropagation();
    if (confirm('Bu kataloğu silmek istediğinize emin misiniz?')) {
      this.catalogService.deleteCatalog(id).subscribe({
        next: () => {
          this.allCatalogs = this.allCatalogs.filter(c => c.id !== id);
          delete this.catalogReviewStats[id];
          this.updateFolderCounts();
          this.refreshView();
        },
        error: (err) => alert('Silme işlemi başarısız.')
      });
    }
  }

  startAiAnalysis(catalog: Catalog, event: Event) {
    event.stopPropagation();
    
    if(!confirm(`${catalog.name} için AI analizi başlatılacak. Onaylıyor musun?`)) return;

    this.isProcessing = true;
    catalog.status = 'Processing'; 

    this.catalogService.startAiProcess(catalog.id).subscribe({
        next: () => {
            alert('AI Analizi Başlatıldı! Arka planda devam ediyor.');
            this.isProcessing = false;
            this.loadAiJobs();
            // Status backend'den Processing olarak döndü, polling veya refresh gerekebilir ama şimdilik böyle kalsın
        },
        error: (err) => {
            console.error(err);
            alert('Hata: ' + (err.error?.message || err.message));
            this.isProcessing = false;
            catalog.status = 'Error';
        }
    });
  }

  getAiJobStatusClass(status: string): string {
    const s = status?.toLowerCase();
    if (s === 'pending') return 'job-chip pending';
    if (s === 'processing') return 'job-chip processing';
    if (s === 'completed') return 'job-chip completed';
    if (s === 'failed') return 'job-chip failed';
    return 'job-chip';
  }

  getAiJobStatusText(status: string): string {
    const s = status?.toLowerCase();
    if (s === 'pending') return 'Beklemede';
    if (s === 'processing') return 'İşleniyor';
    if (s === 'completed') return 'Tamamlandı';
    if (s === 'failed') return 'Başarısız';
    return status || 'Bilinmiyor';
  }

  getCatalogReviewSnapshot(catalogId: string): CatalogReviewSnapshot | null {
    return this.catalogReviewStats[catalogId] ?? null;
  }

  getCatalogReviewTone(catalogId: string): 'healthy' | 'warning' | 'critical' | 'loading' | 'error' {
    const snapshot = this.catalogReviewStats[catalogId];
    if (!snapshot) return 'loading';
    if (snapshot.loading) return 'loading';
    if (snapshot.error) return 'error';
    if (snapshot.highSeverityIssueCount > 0) return 'critical';
    if (snapshot.issueCount > 0 || snapshot.needsReviewPageCount > 0) return 'warning';
    return 'healthy';
  }

  getCatalogReviewLabel(catalogId: string): string {
    const snapshot = this.catalogReviewStats[catalogId];
    if (!snapshot || snapshot.loading) return 'Kontrol özeti yükleniyor';
    if (snapshot.error) return 'Kontrol özeti alınamadı';
    if (snapshot.pageCount === 0) return 'Henüz sayfa yok';
    if (snapshot.issueCount === 0 && snapshot.needsReviewPageCount === 0) return 'Kontrol kuyruğu temiz';
    if (snapshot.highSeverityIssueCount > 0) return `${snapshot.highSeverityIssueCount} kritik kontrol`;
    return `${snapshot.needsReviewPageCount} sayfa bekliyor`;
  }

  private ensureVisibleCatalogReviewStats() {
    this.visibleCatalogs.slice(0, 24).forEach((catalog) => {
      const existing = this.catalogReviewStats[catalog.id];
      if (existing && (existing.loading || existing.loaded)) {
        return;
      }

      this.catalogReviewStats[catalog.id] = {
        loading: true,
        loaded: false,
        pageCount: 0,
        reviewedPageCount: 0,
        needsReviewPageCount: 0,
        issueCount: 0,
        highSeverityIssueCount: 0,
        lowConfidenceCount: 0
      };

      this.catalogService.getCatalogById(catalog.id).subscribe({
        next: (detail) => {
          this.catalogReviewStats[catalog.id] = this.buildCatalogReviewSnapshot(detail);
        },
        error: () => {
          this.catalogReviewStats[catalog.id] = {
            loading: false,
            loaded: false,
            error: 'Kontrol özeti yüklenemedi.',
            pageCount: 0,
            reviewedPageCount: 0,
            needsReviewPageCount: 0,
            issueCount: 0,
            highSeverityIssueCount: 0,
            lowConfidenceCount: 0
          };
        }
      });
    });
  }

  private buildCatalogReviewSnapshot(catalog: Catalog): CatalogReviewSnapshot {
    const pages = catalog.pages ?? [];
    let reviewedPageCount = 0;
    let needsReviewPageCount = 0;
    let issueCount = 0;
    let highSeverityIssueCount = 0;
    let lowConfidenceCount = 0;

    pages.forEach((page) => {
      const issues = this.buildPageReviewIssues(page, page.items ?? []);
      issueCount += issues.length;
      highSeverityIssueCount += issues.filter((issue) => issue.severity === 'high').length;
      lowConfidenceCount += issues.filter((issue) => issue.type === 'low-confidence').length;

      if ((page.reviewStatus ?? 'NeedsReview') === 'Reviewed' && issues.length === 0) {
        reviewedPageCount += 1;
      } else {
        needsReviewPageCount += 1;
      }
    });

    return {
      loading: false,
      loaded: true,
      pageCount: pages.length,
      reviewedPageCount,
      needsReviewPageCount,
      issueCount,
      highSeverityIssueCount,
      lowConfidenceCount
    };
  }

  private buildPageReviewIssues(page?: CatalogPage, pageItems: CatalogPageItem[] = []): CatalogReviewIssue[] {
    if (!page) return [];

    const hotspots = page.hotspots ?? [];
    const issues: CatalogReviewIssue[] = [];
    const hotspotMap = new Map<string, Hotspot[]>();
    const itemMap = new Map<string, CatalogPageItem[]>();

    hotspots.forEach((hotspot) => {
      const label = this.normalizeRef(hotspot.label);
      if (!label) return;
      const group = hotspotMap.get(label) ?? [];
      group.push(hotspot);
      hotspotMap.set(label, group);
    });

    pageItems.forEach((item) => {
      const refNo = this.normalizeRef(item.refNo);
      if (refNo) {
        const group = itemMap.get(refNo) ?? [];
        group.push(item);
        itemMap.set(refNo, group);
      }

      if (!refNo || !item.partCode?.trim() || !item.partName?.trim()) {
        issues.push({ type: 'incomplete-item', severity: 'medium' });
      }
    });

    itemMap.forEach((group) => {
      if (group.length > 1) {
        group.forEach(() => issues.push({ type: 'duplicate-item', severity: 'medium' }));
      }
    });

    hotspotMap.forEach((group) => {
      if (group.length > 1) {
        group.forEach(() => issues.push({ type: 'duplicate-hotspot', severity: 'medium' }));
      }
    });

    pageItems.forEach((item) => {
      const refNo = this.normalizeRef(item.refNo);
      if (!refNo) return;
      if (hotspotMap.has(refNo)) return;
      issues.push({ type: 'missing-hotspot', severity: 'high' });
    });

    hotspots.forEach((hotspot) => {
      const refNo = this.normalizeRef(hotspot.label);
      if (refNo && itemMap.has(refNo)) return;
      issues.push({ type: 'unlinked-hotspot', severity: 'high' });
    });

    hotspots.forEach((hotspot) => {
      if (!this.isHotspotLowConfidence(hotspot)) return;
      issues.push({ type: 'low-confidence', severity: 'medium' });
    });

    return issues;
  }

  private isHotspotLowConfidence(hotspot: Hotspot): boolean {
    const confidence = hotspot.aiConfidence ?? hotspot.confidence ?? 1;
    return confidence < 0.72;
  }

  private normalizeRef(value?: string | null): string {
    return (value ?? '').trim().toLowerCase();
  }
}

type CatalogReviewIssueType =
  | 'missing-hotspot'
  | 'unlinked-hotspot'
  | 'duplicate-item'
  | 'duplicate-hotspot'
  | 'incomplete-item'
  | 'low-confidence';

interface CatalogReviewIssue {
  type: CatalogReviewIssueType;
  severity: 'high' | 'medium';
}

interface CatalogReviewSnapshot {
  loading: boolean;
  loaded: boolean;
  error?: string;
  pageCount: number;
  reviewedPageCount: number;
  needsReviewPageCount: number;
  issueCount: number;
  highSeverityIssueCount: number;
  lowConfidenceCount: number;
}
