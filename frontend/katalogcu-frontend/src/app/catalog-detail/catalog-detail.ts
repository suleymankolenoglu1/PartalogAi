import { Component, OnInit, inject, ElementRef, ViewChild, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CatalogService, Catalog, CatalogPage, RectSelection, Hotspot, CatalogPageItem, CatalogPageReviewStatus, HotspotLabelReadResult } from '../core/services/catalog.service';
import { ProductService } from '../core/services/product.service';

type ReviewFilter = 'all' | 'missing-hotspot' | 'unlinked-hotspot' | 'duplicate' | 'incomplete' | 'low-confidence';
type ReviewIssueType = 'missing-hotspot' | 'unlinked-hotspot' | 'duplicate-item' | 'duplicate-hotspot' | 'incomplete-item' | 'low-confidence';

interface PageReviewSummary {
  issueCount: number;
  missingHotspotCount: number;
  unlinkedHotspotCount: number;
  duplicateItemCount: number;
  duplicateHotspotCount: number;
  incompleteItemCount: number;
  lowConfidenceCount: number;
}

interface PageReviewIssue {
  key: string;
  type: ReviewIssueType;
  severity: 'high' | 'medium';
  title: string;
  description: string;
  refNo?: string;
  hotspotId?: string;
  catalogItemId?: string;
}

interface HotspotDragState {
  hotspotId: string;
  mode: 'move' | 'resize';
  startClientX: number;
  startClientY: number;
  initialLeft: number;
  initialTop: number;
  initialWidth: number;
  initialHeight: number;
}

interface EditorToast {
  type: 'success' | 'error' | 'info';
  message: string;
}

interface HotspotRefSuggestion {
  refNo: string;
  partName: string;
  partCode: string;
  catalogItemId: string;
  score: number;
  reason: string;
}

interface HotspotOcrState {
  isReading: boolean;
  lastResult: HotspotLabelReadResult | null;
  autoLinkedItemId?: string | null;
}

@Component({
  selector: 'app-catalog-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './catalog-detail.html',
  styleUrl: './catalog-detail.css'
})
export class CatalogDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private catalogService = inject(CatalogService);
  private productService = inject(ProductService); // Opsiyonel: Stok işlemleri için kalabilir
  @ViewChild('pageViewport') pageViewportRef?: ElementRef<HTMLDivElement>;
  @ViewChild('pageCanvas') pageCanvasRef?: ElementRef<HTMLDivElement>;
  @ViewChild('activePageImage') activePageImageRef?: ElementRef<HTMLImageElement>;

  catalog: Catalog | null = null;
  
  // 🔥 YENİ: Kütüphane Verileri (Sayfa Bazlı)
  pageItems: CatalogPageItem[] = [];
  
  selectedPartRef: string | null = null; // ID yerine RefNo kullanıyoruz artık

  isLoading = true;
  isEditMode = false; 
  isAiMode = false;   

  // --- ÇOKLU SAYFA ANALİZ STATE ---
  isMultiPageMode = false;
  analysisStep: 'select-table' | 'select-image' | 'ready' = 'select-table';
  selectedTablePage: CatalogPage | null = null;
  selectedImagePage: CatalogPage | null = null;
  
  tableRect: RectSelection = { x: 0, y: 0, w: 100, h: 100 };
  imageRect: RectSelection = { x: 0, y: 0, w: 100, h: 100 };

  // Çizim State
  isDrawing = false;
  drawStartX = 0;
  drawStartY = 0;
  currentRect: RectSelection | null = null;
  activeRectType: 'table' | 'image' | null = null;

  // --- MANUEL EKLEME STATE ---
  tempHotspot: { x: number, y: number } | null = null;
  selectedHotspotId: string | null = null;
  hotspotForm = {
    id: '',
    label: '',
    left: 0,
    top: 0,
    width: 3,
    height: 2,
    productId: null as string | null
  };

  isItemFormVisible = false;
  editingCatalogItemId: string | null = null;
  reviewFilter: ReviewFilter = 'all';
  reviewStatusForm: CatalogPageReviewStatus = 'NeedsReview';
  reviewNotesForm = '';
  isSavingReview = false;
  isRetriggeringHotspots = false;
  hotspotOcrState: HotspotOcrState = { isReading: false, lastResult: null, autoLinkedItemId: null };
  toast: EditorToast | null = null;
  private toastTimer: ReturnType<typeof setTimeout> | null = null;
  showOnlyReviewQueue = false;
  itemForm = {
    refNo: '',
    partCode: '',
    partName: '',
    description: ''
  };
  
  activePageIndex = 0;
  zoomScale = 1;
  stageSize = { width: 0, height: 0 };
  imageFrame = { left: 0, top: 0, width: 0, height: 0 };
  hotspotDragState: HotspotDragState | null = null;

  get activePage(): CatalogPage | undefined {
    return this.catalog?.pages?.[this.activePageIndex];
  }

  get reviewQueuePages(): CatalogPage[] {
    return (this.catalog?.pages ?? []).filter((page) => this.isPageInReviewQueue(page));
  }

  get visiblePages(): CatalogPage[] {
    if (!this.showOnlyReviewQueue) {
      return this.catalog?.pages ?? [];
    }

    return this.reviewQueuePages;
  }

  get hasImageFrame(): boolean {
    return this.imageFrame.width > 1 && this.imageFrame.height > 1;
  }

  get hasHotspots(): boolean {
    return (this.activePage?.hotspots?.length ?? 0) > 0;
  }

  get selectedHotspot(): Hotspot | undefined {
    return this.activePage?.hotspots?.find(h => h.id === this.selectedHotspotId);
  }

  get selectedHotspotSuggestions(): HotspotRefSuggestion[] {
    const hotspot = this.selectedHotspot;
    if (!hotspot) return [];

    const currentLabel = this.normalizeRef(this.hotspotForm.label || hotspot.label);
    const assignedRefs = new Set(
      (this.activePage?.hotspots ?? [])
        .filter((entry) => entry.id !== hotspot.id)
        .map((entry) => this.normalizeRef(entry.label))
        .filter(Boolean)
    );

    return this.pageItems
      .filter((item) => !!item.refNo?.trim())
      .map((item) => {
        const refNo = item.refNo.trim();
        const normalizedRef = this.normalizeRef(refNo);
        const baseScore = this.calculateSuggestionScore(currentLabel, normalizedRef, assignedRefs.has(normalizedRef));
        const reason = this.describeSuggestionReason(currentLabel, normalizedRef, assignedRefs.has(normalizedRef));
        return {
          refNo,
          partName: item.partName ?? '-',
          partCode: item.partCode ?? '-',
          catalogItemId: item.catalogItemId,
          score: baseScore,
          reason
        };
      })
      .filter((item) => item.score > 0)
      .sort((a, b) => b.score - a.score || a.refNo.localeCompare(b.refNo, 'tr', { numeric: true, sensitivity: 'base' }))
      .slice(0, 5);
  }

  get selectedHotspotAutoLinkedItem(): CatalogPageItem | undefined {
    const itemId = this.hotspotOcrState.autoLinkedItemId;
    if (!itemId) return undefined;
    return this.pageItems.find((item) => item.catalogItemId === itemId);
  }

  get isReadyToAnalyze(): boolean {
    return this.selectedTablePage !== null && this.selectedImagePage !== null;
  }

  get activePageReviewSummary(): PageReviewSummary {
    return this.buildPageReviewSummary(this.activePage, this.pageItems);
  }

  get activePageReviewIssues(): PageReviewIssue[] {
    const issues = this.buildPageReviewIssues(this.activePage, this.pageItems)
      .sort((a, b) => {
        if (a.severity !== b.severity) {
          return a.severity === 'high' ? -1 : 1;
        }
        return a.title.localeCompare(b.title, 'tr', { sensitivity: 'base' });
      });
    if (this.reviewFilter === 'all') {
      return issues;
    }

    if (this.reviewFilter === 'duplicate') {
      return issues.filter((issue) => issue.type === 'duplicate-item' || issue.type === 'duplicate-hotspot');
    }

    return issues.filter((issue) => issue.type === this.reviewFilter);
  }

  get reviewHeadline(): string {
    const summary = this.activePageReviewSummary;
    if (summary.issueCount === 0) return 'Bu sayfada açık kontrol işi görünmüyor.';
    if (summary.missingHotspotCount > 0 || summary.unlinkedHotspotCount > 0) {
      return 'Önce teknik resimle tablo eşleşmesini temizle.';
    }
    if (summary.lowConfidenceCount > 0) {
      return 'Düşük güvenli OCR/hotspot kayıtlarını gözden geçir.';
    }
    return 'Bu sayfada manuel düzeltme bekleyen kayıtlar var.';
  }

  get reviewVisibleIssues(): PageReviewIssue[] {
    return this.activePageReviewIssues.slice(0, 6);
  }

  get hiddenIssueCount(): number {
    return Math.max(0, this.activePageReviewIssues.length - this.reviewVisibleIssues.length);
  }

  toggleEditMode() {
    this.isEditMode = !this.isEditMode;
    if (!this.isEditMode) {
      this.tempHotspot = null;
      this.selectedHotspotId = null;
      this.isItemFormVisible = false;
      this.editingCatalogItemId = null;
    }
  }

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadCatalogDetail(id);
    }
  }

  @HostListener('window:resize')
  onWindowResize() {
    this.syncImageFrame();
  }

  @HostListener('window:mousemove', ['$event'])
  onWindowMouseMove(event: MouseEvent) {
    if (!this.hotspotDragState || !this.activePage?.hotspots?.length || !this.hasImageFrame) return;

    const hotspot = this.activePage.hotspots.find((entry) => entry.id === this.hotspotDragState?.hotspotId);
    if (!hotspot) return;

    const deltaX = ((event.clientX - this.hotspotDragState.startClientX) / this.imageFrame.width) * 100;
    const deltaY = ((event.clientY - this.hotspotDragState.startClientY) / this.imageFrame.height) * 100;

    if (this.hotspotDragState.mode === 'move') {
      hotspot.left = this.clamp(this.hotspotDragState.initialLeft + deltaX, 0, 100 - hotspot.width);
      hotspot.top = this.clamp(this.hotspotDragState.initialTop + deltaY, 0, 100 - hotspot.height);
    } else {
      hotspot.width = this.clamp(this.hotspotDragState.initialWidth + deltaX, 1, 100 - hotspot.left);
      hotspot.height = this.clamp(this.hotspotDragState.initialHeight + deltaY, 1, 100 - hotspot.top);
    }

    if (this.selectedHotspotId === hotspot.id) {
      this.hotspotForm = {
        ...this.hotspotForm,
        left: hotspot.left,
        top: hotspot.top,
        width: hotspot.width,
        height: hotspot.height
      };
    }
  }

  @HostListener('window:mouseup')
  onWindowMouseUp() {
    if (!this.hotspotDragState) return;

    const hotspotId = this.hotspotDragState.hotspotId;
    this.hotspotDragState = null;

    if (this.selectedHotspotId === hotspotId) {
      this.saveSelectedHotspot();
    }
  }

  // --- 1. YÜKLEME İŞLEMLERİ ---
  loadCatalogDetail(id: string) {
    this.isLoading = true;
    this.catalogService.getCatalogById(id).subscribe({
      next: (data) => {
        this.catalog = data;
        
        // Sayfaları sırala
        if (this.catalog.pages) {
          this.catalog.pages.sort((a, b) => a.pageNumber - b.pageNumber);
        }

        // İlk sayfanın verilerini çek
        if (this.catalog.pages && this.catalog.pages.length > 0) {
            this.syncReviewFormFromActivePage();
            this.loadPageItems();
            this.scheduleImageFrameSync();
        } else {
            this.isLoading = false;
        }
      },
      error: () => this.isLoading = false
    });
  }

  // 🔥 YENİ: Sayfa değişince o sayfanın kütüphane verilerini çek
  loadPageItems() {
    if (!this.catalog || !this.activePage) return;
    
    this.isLoading = true;
    this.reviewFilter = 'all';
    const pageNum = this.activePage.pageNumber.toString();
    const activeRefs = new Set(
      (this.activePage.hotspots ?? [])
        .map((spot) => this.normalizeRef(spot.label))
        .filter(Boolean)
    );

    this.catalogService.getPageItems(this.catalog.id, pageNum, { strictPage: false }).subscribe({
      next: (items) => {
        this.pageItems = items || [];
        this.isLoading = false;
        
        this.pageItems.sort((a, b) => {
             const aMatched = activeRefs.has(this.normalizeRef(a.refNo)) ? 1 : 0;
             const bMatched = activeRefs.has(this.normalizeRef(b.refNo)) ? 1 : 0;

             if (aMatched !== bMatched) {
               return bMatched - aMatched;
             }

             return (parseInt(a.refNo) || 0) - (parseInt(b.refNo) || 0);
        });
      },
      error: (err) => {
        console.error("Sayfa verisi alınamadı", err);
        this.pageItems = [];
        this.isLoading = false;
      }
    });
  }

  // --- ÇOKLU SAYFA ANALİZ ---
  openMultiPageAnalysis() {
    this.isMultiPageMode = true;
    this.analysisStep = 'select-table';
    this.selectedTablePage = null;
    this.selectedImagePage = null;
    this.tableRect = { x: 0, y: 0, w: 100, h: 100 };
    this.imageRect = { x: 0, y: 0, w: 100, h: 100 };
    this.currentRect = null;
  }

  closeMultiPageAnalysis() {
    this.isMultiPageMode = false;
    this.analysisStep = 'select-table';
    this.selectedTablePage = null;
    this.selectedImagePage = null;
    this.currentRect = null;
  }

  selectTablePage(page: CatalogPage) {
    this.selectedTablePage = page;
    this.tableRect = { x: 0, y: 0, w: 100, h: 100 };
    this.currentRect = null;
  }

  selectImagePage(page: CatalogPage) {
    this.selectedImagePage = page;
    this.imageRect = { x: 0, y: 0, w: 100, h: 100 };
    this.currentRect = null;
  }

  nextAnalysisStep() {
    if (this.analysisStep === 'select-table' && this.selectedTablePage) {
      this.analysisStep = 'select-image';
    } else if (this.analysisStep === 'select-image' && this.selectedImagePage) {
      this.analysisStep = 'ready';
    }
  }

  prevAnalysisStep() {
    if (this.analysisStep === 'select-image') {
      this.analysisStep = 'select-table';
    } else if (this.analysisStep === 'ready') {
      this.analysisStep = 'select-image';
    }
  }

  // --- ÇİZİM İŞLEMLERİ ---
  onDrawStart(event: MouseEvent, type: 'table' | 'image') {
    const container = event.currentTarget as HTMLElement;
    const rect = container.getBoundingClientRect();

    this.isDrawing = true;
    this.activeRectType = type;
    this.drawStartX = ((event.clientX - rect.left) / rect.width) * 100;
    this.drawStartY = ((event.clientY - rect.top) / rect.height) * 100;

    this.currentRect = { x: this.drawStartX, y: this.drawStartY, w: 0, h: 0 };
  }

  onDrawMove(event: MouseEvent) {
    if (!this.isDrawing || !this.currentRect) return;

    const container = event.currentTarget as HTMLElement;
    const rect = container.getBoundingClientRect();
    const currentX = ((event.clientX - rect.left) / rect.width) * 100;
    const currentY = ((event.clientY - rect.top) / rect.height) * 100;

    const x = Math.min(this.drawStartX, currentX);
    const y = Math.min(this.drawStartY, currentY);
    const w = Math.abs(currentX - this.drawStartX);
    const h = Math.abs(currentY - this.drawStartY);

    this.currentRect = { x, y, w, h };
  }

  onDrawEnd() {
    if (!this.isDrawing || !this.currentRect) return;

    if (this.currentRect.w > 2 && this.currentRect.h > 2) {
      if (this.activeRectType === 'table') {
        this.tableRect = { ...this.currentRect };
      } else if (this.activeRectType === 'image') {
        this.imageRect = { ...this.currentRect };
      }
    }
    this.isDrawing = false;
    this.activeRectType = null;
  }

  resetRect(type: 'table' | 'image') {
    if (type === 'table') this.tableRect = { x: 0, y: 0, w: 100, h: 100 };
    else this.imageRect = { x: 0, y: 0, w: 100, h: 100 };
    this.currentRect = null;
  }

  // --- AI ANALİZ ---
  runMultiPageAnalysis() {
    if (!this.catalog || !this.selectedTablePage || !this.selectedImagePage) return;

    if (!confirm('Analiz Başlatılıyor...')) return;

    this.isLoading = true;
    this.isAiMode = true;

    const requestData = {
      tablePageId: this.selectedTablePage.id,
      tableRect: this.tableRect,
      imagePageId: this.selectedImagePage.id,
      imageRect: this.imageRect
    };

    this.catalogService.analyzeMultiPage(this.catalog.id, requestData).subscribe({
      next: (res) => {
        this.showToast('success', `Analiz tamamlandı. ${res.productCount} parça, ${res.hotspotCount} hotspot işlendi.`);
        this.isAiMode = false;
        this.closeMultiPageAnalysis();
        
        // Sonuç sayfasına git
        if (this.catalog?.pages && res.imagePageNumber) {
           const idx = this.catalog.pages.findIndex(p => p.pageNumber === res.imagePageNumber);
           if (idx !== -1) {
             this.activePageIndex = idx;
             this.loadPageItems(); // Yeni verileri çek
           }
        } else {
            this.loadCatalogDetail(this.catalog!.id);
        }
      },
      error: (err) => {
        console.error(err);
        this.isLoading = false;
        this.isAiMode = false;
        this.showToast('error', err.error?.message || 'Analiz başarısız.');
      }
    });
  }

  runFullPageAnalysis() {
    if (!this.catalog || !this.activePage) return;
    if (!confirm(`Bu sayfa için AI analizi başlatılsın mı?`)) return;

    this.isLoading = true;
    this.isAiMode = true;

    this.catalogService.analyzePage(this.catalog.id, { pageId: this.activePage.id }).subscribe({
      next: (res) => {
        this.showToast('success', `Analiz tamamlandı. ${res.productCount} parça bulundu.`);
        this.isAiMode = false;
        this.loadCatalogDetail(this.catalog!.id);
      },
      error: (err) => {
        this.isLoading = false;
        this.isAiMode = false;
        this.showToast('error', err.error?.message || 'Analiz sırasında hata oluştu.');
      }
    });
  }

  redetectHotspots() {
    if (!this.activePage || this.isRetriggeringHotspots) return;
    if (!confirm('Aktif sayfadaki AI hotspotları yeniden taransın mı? Elle eklediğin hotspotlar korunur.')) return;

    this.isRetriggeringHotspots = true;
    this.catalogService.detectHotspots(this.activePage.id).subscribe({
      next: (result) => {
        if (this.activePage) {
          const manualHotspots = (this.activePage.hotspots ?? []).filter((spot) => !this.isHotspotAiDetected(spot));
          this.activePage.hotspots = [...manualHotspots, ...(result.hotspots ?? [])];
        }
        this.selectedHotspotId = null;
        this.tempHotspot = null;
        this.reviewFilter = 'all';
        this.isRetriggeringHotspots = false;
        this.showToast('success', result.message || `${result.detectedCount} hotspot yenilendi.`);
      },
      error: (err) => {
        this.isRetriggeringHotspots = false;
        this.showToast('error', err?.error?.error || 'Hotspot taraması başarısız.');
      }
    });
  }

  // --- NAVİGASYON ---
  selectPage(index: number) {
    if (!this.catalog?.pages || index < 0 || index >= this.catalog.pages.length) return;
    this.activePageIndex = index;
    this.tempHotspot = null;
    this.selectedHotspotId = null;
    this.selectedPartRef = null;
    this.isItemFormVisible = false;
    this.reviewFilter = 'all';
    this.syncReviewFormFromActivePage();
    this.loadPageItems();
    this.scheduleImageFrameSync();
  }

  selectPageById(pageId: string) {
    const index = this.catalog?.pages?.findIndex((page) => page.id === pageId) ?? -1;
    if (index >= 0) {
      this.selectPage(index);
    }
  }

  nextPage() {
    if (this.catalog?.pages && this.activePageIndex < this.catalog.pages.length - 1) {
      this.selectPage(this.activePageIndex + 1);
    }
  }

  prevPage() {
    if (this.activePageIndex > 0) {
      this.selectPage(this.activePageIndex - 1);
    }
  }

  // --- MANUEL HOTSPOT DÜZENLEME ---
  onActiveImageLoad() {
    this.syncImageFrame();
  }

  onImageClick(event: MouseEvent) {
    if (!this.isEditMode || !this.activePage) return;
    if (!this.hasImageFrame) return;

    const container = event.currentTarget as HTMLElement;
    const rect = container.getBoundingClientRect();
    const frameX = event.clientX - rect.left - this.imageFrame.left;
    const frameY = event.clientY - rect.top - this.imageFrame.top;
    if (frameX < 0 || frameY < 0 || frameX > this.imageFrame.width || frameY > this.imageFrame.height) {
      return;
    }

    const x = (frameX / this.imageFrame.width) * 100;
    const y = (frameY / this.imageFrame.height) * 100;

    this.tempHotspot = { x, y };
    this.selectedHotspotId = null;
  }

  // 🔥 GÜNCELLENDİ: Listeden seçip atama (CatalogPageItem kullanır)
  assignItemToHotspot(item: CatalogPageItem) {
    if (!this.isEditMode || !this.activePage) return;

    if (this.selectedHotspotId) {
      this.hotspotForm = {
        ...this.hotspotForm,
        label: item.refNo,
        productId: item.isStocked ? item.productId ?? null : null
      };
      this.saveSelectedHotspot();
      return;
    }

    if (!this.tempHotspot) return;

    const newHotspot = {
      pageId: this.activePage.id,
      productId: item.isStocked ? item.productId : null, // Stoktaysa bağla
      label: item.refNo, // ÖNEMLİ: Eşleşme RefNo üzerinden yapılır
      
      left: this.tempHotspot.x - 1.5,
      top: this.tempHotspot.y - 1,
      width: 3,
      height: 2
    };

    this.catalogService.createHotspot(newHotspot).subscribe({
      next: (createdSpot) => {
        if (!this.activePage!.hotspots) this.activePage!.hotspots = [];
        this.activePage!.hotspots.push(createdSpot);
        this.selectHotspotForEdit(createdSpot);
        this.tempHotspot = null;
        this.showToast('success', `#${createdSpot.label || 'Yeni'} hotspot eklendi.`);
      },
      error: () => this.showToast('error', 'Hotspot eklenemedi.')
    });
  }

  removeHotspot(event: Event, spotId: string) {
    event.stopPropagation();
    if (!confirm('Silmek istiyor musunuz?')) return;
    this.catalogService.deleteHotspot(spotId).subscribe(() => {
      if (this.activePage?.hotspots) {
        this.activePage.hotspots = this.activePage.hotspots.filter(h => h.id !== spotId);
      }
      if (this.selectedHotspotId === spotId) {
        this.selectedHotspotId = null;
      }
      this.showToast('success', 'Hotspot silindi.');
    });
  }

  selectPart(refNo: string) {
    this.selectedPartRef = this.selectedPartRef === refNo ? null : refNo;
  }

  onHotspotClick(event: Event, hotspot: Hotspot) {
    event.stopPropagation();
    if (this.hotspotDragState) return;
    if (this.isEditMode) {
      this.selectHotspotForEdit(hotspot);
      return;
    }

    // RefNo ile listede bul
    if (hotspot.label) {
        this.selectedPartRef = hotspot.label;
        setTimeout(() => {
            const item = this.pageItems.find((entry) => this.normalizeRef(entry.refNo) === this.normalizeRef(hotspot.label));
            if (item) {
              this.scrollItemIntoView(item.catalogItemId);
            }
        }, 100);
    }
  }

  selectHotspotForEdit(hotspot: Hotspot) {
    this.selectedHotspotId = hotspot.id;
    this.hotspotOcrState = { isReading: false, lastResult: null, autoLinkedItemId: null };
    this.hotspotForm = {
      id: hotspot.id,
      label: hotspot.label ?? '',
      left: hotspot.left,
      top: hotspot.top,
      width: hotspot.width,
      height: hotspot.height,
      productId: hotspot.productId ?? null
    };
    this.selectedPartRef = hotspot.label ?? null;
    this.tempHotspot = null;
  }

  applySuggestionToSelectedHotspot(suggestion: HotspotRefSuggestion) {
    if (!this.selectedHotspotId) return;
    const matchedItem = this.pageItems.find((item) => item.catalogItemId === suggestion.catalogItemId);
    this.hotspotForm = {
      ...this.hotspotForm,
      label: suggestion.refNo,
      productId: matchedItem?.isStocked ? matchedItem.productId ?? null : null
    };
    this.saveSelectedHotspot();
    this.scrollItemIntoView(suggestion.catalogItemId);
  }

  clearSelectedHotspotLink() {
    if (!this.selectedHotspotId) return;
    this.hotspotForm = {
      ...this.hotspotForm,
      label: '',
      productId: null
    };
    this.hotspotOcrState = {
      ...this.hotspotOcrState,
      autoLinkedItemId: null
    };
  }

  readSelectedHotspotLabel() {
    if (!this.selectedHotspotId) return;

    this.hotspotOcrState = {
      ...this.hotspotOcrState,
      isReading: true
    };

    this.catalogService.readHotspotLabel(this.selectedHotspotId).subscribe({
      next: (result) => {
        const normalizedLabel = this.normalizeRef(result.label);
        const matchedItem = this.pageItems.find((item) => this.normalizeRef(item.refNo) === normalizedLabel);
        this.hotspotOcrState = {
          isReading: false,
          lastResult: result,
          autoLinkedItemId: matchedItem?.catalogItemId ?? null
        };

        if (!result.success || !result.label?.trim()) {
          this.showToast('info', result.message || 'OCR etiketi okuyamadı.');
          return;
        }

        this.hotspotForm = {
          ...this.hotspotForm,
          label: result.label.trim(),
          productId: matchedItem?.isStocked ? matchedItem.productId ?? null : this.hotspotForm.productId
        };
        this.selectedPartRef = result.label.trim();
        if (matchedItem) {
          this.scrollItemIntoView(matchedItem.catalogItemId);
        }
        this.showToast('success', `OCR etiketi okudu: #${result.label}`);
      },
      error: () => {
        this.hotspotOcrState = {
          isReading: false,
          lastResult: null
        };
        this.showToast('error', 'Hotspot OCR okunamadı.');
      }
    });
  }

  startHotspotMove(event: MouseEvent, hotspot: Hotspot) {
    if (!this.isEditMode) return;
    event.preventDefault();
    event.stopPropagation();
    this.selectHotspotForEdit(hotspot);
    this.hotspotDragState = {
      hotspotId: hotspot.id,
      mode: 'move',
      startClientX: event.clientX,
      startClientY: event.clientY,
      initialLeft: hotspot.left,
      initialTop: hotspot.top,
      initialWidth: hotspot.width,
      initialHeight: hotspot.height
    };
  }

  startHotspotResize(event: MouseEvent, hotspot: Hotspot) {
    if (!this.isEditMode) return;
    event.preventDefault();
    event.stopPropagation();
    this.selectHotspotForEdit(hotspot);
    this.hotspotDragState = {
      hotspotId: hotspot.id,
      mode: 'resize',
      startClientX: event.clientX,
      startClientY: event.clientY,
      initialLeft: hotspot.left,
      initialTop: hotspot.top,
      initialWidth: hotspot.width,
      initialHeight: hotspot.height
    };
  }

  saveSelectedHotspot() {
    if (!this.selectedHotspotId) return;

    const payload = {
      label: this.hotspotForm.label,
      left: this.hotspotForm.left,
      top: this.hotspotForm.top,
      width: this.hotspotForm.width,
      height: this.hotspotForm.height,
      productId: this.hotspotForm.productId
    };

    this.catalogService.updateHotspot(this.selectedHotspotId, payload).subscribe({
      next: (updated) => {
        const hotspot = this.activePage?.hotspots?.find(h => h.id === updated.id);
        if (hotspot) {
          hotspot.label = updated.label;
          hotspot.left = updated.left;
          hotspot.top = updated.top;
          hotspot.width = updated.width;
          hotspot.height = updated.height;
          hotspot.productId = updated.productId;
        }
        this.selectedPartRef = updated.label ?? null;
        this.reviewFilter = 'all';
        this.showToast('success', `#${updated.label || 'Hotspot'} güncellendi.`);
      },
      error: () => this.showToast('error', 'Hotspot güncellenemedi.')
    });
  }

  openAddItemForm() {
    this.isItemFormVisible = true;
    this.editingCatalogItemId = null;
    this.itemForm = { refNo: '', partCode: '', partName: '', description: '' };
  }

  openEditItemForm(item: CatalogPageItem, event?: Event) {
    event?.stopPropagation();
    this.isItemFormVisible = true;
    this.editingCatalogItemId = item.catalogItemId;
    this.itemForm = {
      refNo: item.refNo ?? '',
      partCode: item.partCode ?? '',
      partName: item.partName ?? '',
      description: item.description ?? ''
    };
  }

  closeItemForm() {
    this.isItemFormVisible = false;
    this.editingCatalogItemId = null;
  }

  saveItemForm() {
    if (!this.catalog || !this.activePage) return;
    if (!this.itemForm.refNo.trim() || !this.itemForm.partCode.trim() || !this.itemForm.partName.trim()) {
      this.showToast('error', 'Ref No, Parça Kodu ve Parça Adı zorunludur.');
      return;
    }

    if (this.editingCatalogItemId) {
      this.catalogService.updateCatalogItem(this.editingCatalogItemId, {
        refNo: this.itemForm.refNo.trim(),
        partCode: this.itemForm.partCode.trim(),
        partName: this.itemForm.partName.trim(),
        description: this.itemForm.description.trim()
      }).subscribe({
        next: () => {
          this.closeItemForm();
          this.showToast('success', 'Parça satırı güncellendi.');
          this.loadPageItems();
        },
        error: () => this.showToast('error', 'Parça satırı güncellenemedi.')
      });
      return;
    }

    this.catalogService.createCatalogItem({
      catalogId: this.catalog.id,
      pageNumber: this.activePage.pageNumber,
      refNo: this.itemForm.refNo.trim(),
      partCode: this.itemForm.partCode.trim(),
      partName: this.itemForm.partName.trim(),
      description: this.itemForm.description.trim()
    }).subscribe({
      next: () => {
        this.closeItemForm();
        this.reviewFilter = 'all';
        this.showToast('success', 'Parça satırı eklendi.');
        this.loadPageItems();
      },
      error: () => this.showToast('error', 'Parça satırı eklenemedi.')
    });
  }

  deleteItem(item: CatalogPageItem, event?: Event) {
    event?.stopPropagation();
    if (!confirm(`"${item.refNo}" satırını silmek istiyor musunuz?`)) return;

    this.catalogService.deleteCatalogItem(item.catalogItemId).subscribe({
      next: () => {
        if (this.editingCatalogItemId === item.catalogItemId) {
          this.closeItemForm();
        }
        this.reviewFilter = 'all';
        this.showToast('success', 'Parça satırı silindi.');
        this.loadPageItems();
      },
      error: () => this.showToast('error', 'Parça satırı silinemedi.')
    });
  }

  publishAndOpen() {
    if (!this.catalog) return;
    this.isLoading = true;
    this.catalogService.publishCatalog(this.catalog.id).subscribe({
      next: () => {
        this.isLoading = false;
        this.catalog!.status = 'Published';
        const publicUrl = `/view/${this.catalog!.id}`;
        window.open(publicUrl, '_blank');
      },
      error: (err) => {
        console.error(err);
        this.isLoading = false;
        this.showToast('error', err?.error?.message || 'Yayınlama sırasında hata oluştu.');
      }
    });
  }

  private scheduleImageFrameSync() {
    setTimeout(() => this.syncImageFrame(), 0);
  }

  private syncImageFrame() {
    const viewport = this.pageViewportRef?.nativeElement;
    const image = this.activePageImageRef?.nativeElement;
    if (!viewport || !image) return;

    const viewportRect = viewport.getBoundingClientRect();
    if (viewportRect.width <= 0 || viewportRect.height <= 0) return;

    const naturalWidth = image.naturalWidth || image.clientWidth;
    const naturalHeight = image.naturalHeight || image.clientHeight;
    if (!naturalWidth || !naturalHeight) return;

    const containerAspect = viewportRect.width / viewportRect.height;
    const imageAspect = naturalWidth / naturalHeight;

    let width = 0;
    let height = 0;

    if (imageAspect > containerAspect) {
      width = viewportRect.width;
      height = width / imageAspect;
    } else {
      height = viewportRect.height;
      width = height * imageAspect;
    }

    const scaledWidth = width * this.zoomScale;
    const scaledHeight = height * this.zoomScale;
    this.stageSize = { width: scaledWidth, height: scaledHeight };
    this.imageFrame = { left: 0, top: 0, width: scaledWidth, height: scaledHeight };
  }

  zoomIn() {
    this.zoomScale = Math.min(3, Number((this.zoomScale + 0.2).toFixed(2)));
    this.syncImageFrame();
  }

  zoomOut() {
    this.zoomScale = Math.max(1, Number((this.zoomScale - 0.2).toFixed(2)));
    this.syncImageFrame();
  }

  resetZoom() {
    this.zoomScale = 1;
    this.syncImageFrame();
  }

  getPageReviewSummary(page: CatalogPage): PageReviewSummary {
    const pageItems = page.id === this.activePage?.id ? this.pageItems : page.items ?? [];
    return this.buildPageReviewSummary(page, pageItems);
  }

  setReviewFilter(filter: ReviewFilter) {
    this.reviewFilter = filter;
  }

  focusFirstReviewIssue() {
    const issue = this.activePageReviewIssues[0];
    if (!issue) {
      this.showToast('info', 'Bu sayfada açık issue yok.');
      return;
    }
    this.jumpToIssue(issue);
  }

  markPageReviewed(status: CatalogPageReviewStatus) {
    if (!this.catalog || !this.activePage || this.isSavingReview) return;

    this.isSavingReview = true;
    this.catalogService.updateCatalogPageReview(this.catalog.id, this.activePage.id, {
      reviewStatus: status,
      reviewNotes: this.reviewNotesForm.trim() || undefined
    }).subscribe({
      next: (updated) => {
        if (!this.activePage) return;
        this.activePage.reviewStatus = updated.reviewStatus ?? status;
        this.activePage.reviewNotes = (updated.reviewNotes ?? this.reviewNotesForm.trim()) || null;
        this.activePage.reviewedAt = updated.reviewedAt ?? null;
        this.reviewStatusForm = this.activePage.reviewStatus ?? status;
        this.reviewNotesForm = this.activePage.reviewNotes ?? '';
        this.isSavingReview = false;
        this.showToast('success', status === 'Reviewed' ? 'Sayfa inceleme tamamlandı.' : 'Sayfa tekrar kontrol kuyruğuna alındı.');

        if (status === 'Reviewed' && this.showOnlyReviewQueue && this.activePage && !this.isPageInReviewQueue(this.activePage)) {
          this.jumpToNextReviewPage(true);
        }
      },
      error: () => {
        this.isSavingReview = false;
        this.showToast('error', 'Sayfa inceleme durumu kaydedilemedi.');
      }
    });
  }

  getReviewStatusLabel(page?: CatalogPage): string {
    return (page?.reviewStatus ?? 'NeedsReview') === 'Reviewed'
      ? 'İncelendi'
      : 'Kontrol Gerekli';
  }

  getReviewStatusClass(page?: CatalogPage): string {
    return (page?.reviewStatus ?? 'NeedsReview') === 'Reviewed'
      ? 'reviewed'
      : 'needs-review';
  }

  toggleReviewQueueOnly() {
    this.showOnlyReviewQueue = !this.showOnlyReviewQueue;
    if (this.showOnlyReviewQueue && this.activePage && !this.isPageInReviewQueue(this.activePage)) {
      this.jumpToNextReviewPage(true);
    }
  }

  jumpToNextReviewPage(silent = false) {
    const queue = this.reviewQueuePages;
    if (!queue.length) {
      if (!silent) {
        this.showToast('info', 'Kontrol bekleyen sayfa kalmadı.');
      }
      return;
    }

    const currentPageId = this.activePage?.id;
    const currentIndex = queue.findIndex((page) => page.id === currentPageId);
    const nextPage = currentIndex >= 0 && currentIndex < queue.length - 1
      ? queue[currentIndex + 1]
      : queue[0];

    if (nextPage && nextPage.id !== currentPageId) {
      this.selectPageById(nextPage.id);
    } else if (!silent) {
      this.showToast('info', 'Zaten kuyruktaki son sayfadasın.');
    }
  }

  isHotspotLowConfidence(hotspot: Hotspot): boolean {
    const confidence = hotspot.aiConfidence ?? hotspot.confidence;
    return typeof confidence === 'number' && confidence > 0 && confidence < 0.72;
  }

  isHotspotAiDetected(hotspot: Hotspot): boolean {
    if (typeof hotspot.isAiDetected === 'boolean') {
      return hotspot.isAiDetected;
    }

    const confidence = hotspot.aiConfidence ?? hotspot.confidence;
    return typeof confidence === 'number' && confidence > 0;
  }

  jumpToIssue(issue: PageReviewIssue) {
    if (issue.hotspotId && this.activePage?.hotspots) {
      const hotspot = this.activePage.hotspots.find((entry) => entry.id === issue.hotspotId);
      if (hotspot) {
        this.isEditMode = true;
        this.selectHotspotForEdit(hotspot);
      }
    }

    if (issue.catalogItemId) {
      const item = this.pageItems.find((entry) => entry.catalogItemId === issue.catalogItemId);
      if (item) {
        this.selectedPartRef = item.refNo ?? null;
        this.scrollItemIntoView(item.catalogItemId);
        if (this.isEditMode && issue.type !== 'missing-hotspot') {
          this.openEditItemForm(item);
        }
      }
    }
  }

  private buildPageReviewSummary(page?: CatalogPage, pageItems?: CatalogPageItem[]): PageReviewSummary {
    const issues = this.buildPageReviewIssues(page, pageItems);
    return {
      issueCount: issues.length,
      missingHotspotCount: issues.filter((issue) => issue.type === 'missing-hotspot').length,
      unlinkedHotspotCount: issues.filter((issue) => issue.type === 'unlinked-hotspot').length,
      duplicateItemCount: issues.filter((issue) => issue.type === 'duplicate-item').length,
      duplicateHotspotCount: issues.filter((issue) => issue.type === 'duplicate-hotspot').length,
      incompleteItemCount: issues.filter((issue) => issue.type === 'incomplete-item').length,
      lowConfidenceCount: issues.filter((issue) => issue.type === 'low-confidence').length
    };
  }

  private buildPageReviewIssues(page?: CatalogPage, pageItems: CatalogPageItem[] = []): PageReviewIssue[] {
    if (!page) return [];

    const hotspots = page.hotspots ?? [];
    const issues: PageReviewIssue[] = [];
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
        issues.push({
          key: `incomplete-${item.catalogItemId}`,
          type: 'incomplete-item',
          severity: 'medium',
          title: item.refNo?.trim() ? `#${item.refNo} satırı eksik` : 'Ref no eksik satır',
          description: 'Ref no, parça kodu ve parça adı alanlarını tamamla.',
          refNo: item.refNo,
          catalogItemId: item.catalogItemId
        });
      }
    });

    itemMap.forEach((group, refNo) => {
      if (group.length <= 1) return;
      group.forEach((item) => {
        issues.push({
          key: `duplicate-item-${item.catalogItemId}`,
          type: 'duplicate-item',
          severity: 'medium',
          title: `#${refNo} ref no mükerrer`,
          description: 'Aynı ref no birden fazla satırda görünüyor. Tekilleştir veya düzelt.',
          refNo,
          catalogItemId: item.catalogItemId
        });
      });
    });

    hotspotMap.forEach((group, refNo) => {
      if (group.length <= 1) return;
      group.forEach((hotspot) => {
        issues.push({
          key: `duplicate-hotspot-${hotspot.id}`,
          type: 'duplicate-hotspot',
          severity: 'medium',
          title: `#${refNo} hotspot mükerrer`,
          description: 'Aynı etiketli birden fazla hotspot var. Koordinatları veya etiketi kontrol et.',
          refNo,
          hotspotId: hotspot.id
        });
      });
    });

    pageItems.forEach((item) => {
      const refNo = this.normalizeRef(item.refNo);
      if (!refNo) return;
      if (hotspotMap.has(refNo)) return;
      issues.push({
        key: `missing-hotspot-${item.catalogItemId}`,
        type: 'missing-hotspot',
        severity: 'high',
        title: `#${refNo} için hotspot yok`,
        description: 'Listede satır var ama teknik resimde karşılığı işaretlenmemiş.',
        refNo,
        catalogItemId: item.catalogItemId
      });
    });

    hotspots.forEach((hotspot) => {
      const refNo = this.normalizeRef(hotspot.label);
      if (refNo && itemMap.has(refNo)) return;
      issues.push({
        key: `unlinked-hotspot-${hotspot.id}`,
        type: 'unlinked-hotspot',
        severity: 'high',
        title: refNo ? `#${refNo} hotspotu eşleşmiyor` : 'Etiketsiz hotspot',
        description: 'Bu hotspot için eşleşen parça satırı bulunamadı. Ref no veya satır bağlantısını düzelt.',
        refNo: hotspot.label,
        hotspotId: hotspot.id
      });
    });

    hotspots.forEach((hotspot) => {
      if (!this.isHotspotLowConfidence(hotspot)) return;
      issues.push({
        key: `low-confidence-${hotspot.id}`,
        type: 'low-confidence',
        severity: 'medium',
        title: hotspot.label?.trim()
          ? `#${hotspot.label} hotspot güveni düşük`
          : 'Hotspot güveni düşük',
        description: `AI güven skoru ${Math.round(((hotspot.aiConfidence ?? hotspot.confidence) ?? 0) * 100)}% seviyesinde. Elle kontrol et veya yeniden konumlandır.`,
        refNo: hotspot.label,
        hotspotId: hotspot.id
      });
    });

    return issues;
  }

  private normalizeRef(value?: string | null): string {
    return (value ?? '').trim().toLowerCase();
  }

  private scrollItemIntoView(catalogItemId: string) {
    setTimeout(() => {
      const element = document.getElementById(`part-row-${catalogItemId}`);
      element?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }, 80);
  }

  private clamp(value: number, min: number, max: number): number {
    return Math.min(Math.max(value, min), max);
  }

  private isPageInReviewQueue(page: CatalogPage): boolean {
    const summary = this.getPageReviewSummary(page);
    return (page.reviewStatus ?? 'NeedsReview') !== 'Reviewed' || summary.issueCount > 0;
  }

  private calculateSuggestionScore(currentLabel: string, candidateLabel: string, alreadyAssigned: boolean): number {
    if (!candidateLabel) return 0;

    let score = 0;
    if (!currentLabel) {
      score += alreadyAssigned ? 8 : 18;
    } else if (candidateLabel === currentLabel) {
      score += 120;
    } else {
      if (candidateLabel.includes(currentLabel) || currentLabel.includes(candidateLabel)) {
        score += 70;
      }

      const candidateDigits = candidateLabel.replace(/\D/g, '');
      const currentDigits = currentLabel.replace(/\D/g, '');
      if (candidateDigits && currentDigits) {
        if (candidateDigits === currentDigits) {
          score += 55;
        } else {
          const distance = Math.abs(Number(candidateDigits) - Number(currentDigits));
          if (distance <= 2) score += 34;
          else if (distance <= 5) score += 18;
        }
      }

      const commonPrefix = this.getCommonPrefixLength(candidateLabel, currentLabel);
      score += Math.min(commonPrefix * 8, 24);

      const editDistance = this.getLevenshteinDistance(candidateLabel, currentLabel);
      if (editDistance <= 1) score += 26;
      else if (editDistance === 2) score += 16;
    }

    if (!alreadyAssigned) {
      score += 12;
    }

    return score;
  }

  private describeSuggestionReason(currentLabel: string, candidateLabel: string, alreadyAssigned: boolean): string {
    if (!currentLabel) {
      return alreadyAssigned ? 'Mükerrer olabilir' : 'Henüz atanmadı';
    }

    const candidateDigits = candidateLabel.replace(/\D/g, '');
    const currentDigits = currentLabel.replace(/\D/g, '');

    if (candidateLabel === currentLabel) return 'Aynı ref';
    if (candidateDigits && currentDigits && candidateDigits === currentDigits) return 'Rakamlar aynı';
    if (candidateLabel.includes(currentLabel) || currentLabel.includes(candidateLabel)) return 'Benzer karakter dizisi';
    if (!alreadyAssigned) return 'Boşta uygun aday';
    return 'Yakın eşleşme';
  }

  private getCommonPrefixLength(a: string, b: string): number {
    const limit = Math.min(a.length, b.length);
    let count = 0;
    for (let i = 0; i < limit; i += 1) {
      if (a[i] !== b[i]) break;
      count += 1;
    }
    return count;
  }

  private getLevenshteinDistance(a: string, b: string): number {
    if (a === b) return 0;
    if (!a.length) return b.length;
    if (!b.length) return a.length;

    const matrix = Array.from({ length: b.length + 1 }, (_, row) =>
      Array.from({ length: a.length + 1 }, (_, col) => (row === 0 ? col : col === 0 ? row : 0))
    );

    for (let row = 1; row <= b.length; row += 1) {
      for (let col = 1; col <= a.length; col += 1) {
        const cost = a[col - 1] === b[row - 1] ? 0 : 1;
        matrix[row][col] = Math.min(
          matrix[row - 1][col] + 1,
          matrix[row][col - 1] + 1,
          matrix[row - 1][col - 1] + cost
        );
      }
    }

    return matrix[b.length][a.length];
  }

  private syncReviewFormFromActivePage() {
    this.reviewStatusForm = this.activePage?.reviewStatus ?? 'NeedsReview';
    this.reviewNotesForm = this.activePage?.reviewNotes ?? '';
  }

  private showToast(type: EditorToast['type'], message: string) {
    this.toast = { type, message };
    if (this.toastTimer) {
      clearTimeout(this.toastTimer);
    }
    this.toastTimer = setTimeout(() => {
      this.toast = null;
      this.toastTimer = null;
    }, 2800);
  }
}
