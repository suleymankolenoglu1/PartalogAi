import { Component, OnInit, inject, ElementRef, ViewChild, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CatalogService, Catalog, CatalogPage, RectSelection, Hotspot, CatalogPageItem } from '../core/services/catalog.service';
import { ProductService } from '../core/services/product.service';

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
  itemForm = {
    refNo: '',
    partCode: '',
    partName: '',
    description: ''
  };
  
  activePageIndex = 0;
  imageFrame = { left: 0, top: 0, width: 0, height: 0 };

  get activePage(): CatalogPage | undefined {
    return this.catalog?.pages?.[this.activePageIndex];
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

  get isReadyToAnalyze(): boolean {
    return this.selectedTablePage !== null && this.selectedImagePage !== null;
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
    const pageNum = this.activePage.pageNumber.toString();

    this.catalogService.getPageItems(this.catalog.id, pageNum, { strictPage: true }).subscribe({
      next: (items) => {
        this.pageItems = items || [];
        this.isLoading = false;
        
        // Eğer edit moddaysak veya hotspot varsa sırala
        this.pageItems.sort((a, b) => {
             // String ref karşılaştırması (örn: "10" vs "2")
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
        alert(`✅ Analiz Tamamlandı!\n📦 Parça: ${res.productCount}\n🎯 Hotspot: ${res.hotspotCount}`);
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
        alert('Hata: ' + (err.error?.message || 'Analiz başarısız.'));
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
        alert(`Analiz Tamamlandı!\n${res.productCount} parça bulundu.`);
        this.isAiMode = false;
        this.loadCatalogDetail(this.catalog!.id);
      },
      error: (err) => {
        this.isLoading = false;
        this.isAiMode = false;
        alert('Hata: ' + (err.error?.message || 'Hata oluştu.'));
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
    this.loadPageItems();
    this.scheduleImageFrameSync();
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
      },
      error: () => alert('Hotspot eklenemedi!')
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
    });
  }

  selectPart(refNo: string) {
    this.selectedPartRef = this.selectedPartRef === refNo ? null : refNo;
  }

  onHotspotClick(event: Event, hotspot: Hotspot) {
    event.stopPropagation();
    if (this.isEditMode) {
      this.selectHotspotForEdit(hotspot);
      return;
    }

    // RefNo ile listede bul
    if (hotspot.label) {
        this.selectedPartRef = hotspot.label;
        setTimeout(() => {
            // HTML tarafında id="part-REFNO" olmalı
            const element = document.getElementById(`part-${hotspot.label}`);
            if (element) element.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }, 100);
    }
  }

  selectHotspotForEdit(hotspot: Hotspot) {
    this.selectedHotspotId = hotspot.id;
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
      },
      error: () => alert('Hotspot güncellenemedi.')
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
      alert('Ref No, Parça Kodu ve Parça Adı zorunludur.');
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
          this.loadPageItems();
        },
        error: () => alert('Parça satırı güncellenemedi.')
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
        this.loadPageItems();
      },
      error: () => alert('Parça satırı eklenemedi.')
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
        this.loadPageItems();
      },
      error: () => alert('Parça satırı silinemedi.')
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
        alert('Hata oluştu.');
      }
    });
  }

  private scheduleImageFrameSync() {
    setTimeout(() => this.syncImageFrame(), 0);
  }

  private syncImageFrame() {
    const container = this.pageCanvasRef?.nativeElement;
    const image = this.activePageImageRef?.nativeElement;
    if (!container || !image) return;

    const containerRect = container.getBoundingClientRect();
    if (containerRect.width <= 0 || containerRect.height <= 0) return;

    const naturalWidth = image.naturalWidth || image.clientWidth;
    const naturalHeight = image.naturalHeight || image.clientHeight;
    if (!naturalWidth || !naturalHeight) return;

    const containerAspect = containerRect.width / containerRect.height;
    const imageAspect = naturalWidth / naturalHeight;

    let width = 0;
    let height = 0;
    let left = 0;
    let top = 0;

    if (imageAspect > containerAspect) {
      width = containerRect.width;
      height = width / imageAspect;
      left = 0;
      top = (containerRect.height - height) / 2;
    } else {
      height = containerRect.height;
      width = height * imageAspect;
      top = 0;
      left = (containerRect.width - width) / 2;
    }

    this.imageFrame = { left, top, width, height };
  }
}
