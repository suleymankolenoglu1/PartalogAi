import { Component, OnInit, inject, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CatalogService, Catalog, CatalogPage, CatalogPageItem } from '../../core/services/catalog.service';
import { CartService } from '../../core/services/cart.service';

interface ViewerGroup {
  pageIndex: number;
  pageNumber: number;
  title: string;
  imageUrl: string;
  isTechnicalDrawing: boolean;
}

interface RequestedPartSelection {
  itemId: string | null;
  refNo: string | null;
  partCode: string | null;
}

@Component({
  selector: 'app-public-catalog-viewer',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  templateUrl: './public-catalog-viewer.html',
  styleUrls: ['./public-catalog-viewer.css']
})
export class PublicCatalogViewerComponent implements OnInit {
  
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private catalogService = inject(CatalogService);
  public cartService = inject(CartService); 
  @ViewChild('viewport') private viewportRef?: ElementRef<HTMLDivElement>;
  @ViewChild('technicalImage') private technicalImageRef?: ElementRef<HTMLImageElement>;

  // 🔥 DÜZELTME: Bu değişken artık class property olarak burada!
  // HTML'deki [routerLink] bunu kullanacak.
  catalogId: string | null = null;
  publicToken: string | null = null;
  publicQueryParams: any = {};

  catalog: Catalog | null = null;
  groups: ViewerGroup[] = [];
  
  activeGroupIndex: number = 0;
  activePage: CatalogPage | null = null;
  
  // Kütüphane Verileri (Stoklu + Stoksuz)
  pageItems: CatalogPageItem[] = [];
  filteredItems: CatalogPageItem[] = [];
  searchQuery: string = '';
  
  // Seçim Durumları
  selectedPartLabel: string | null = null;
  selectedItem: CatalogPageItem | null = null;
  selectedProductId: string | null = null;

  isSidebarOpen = true;
  isCartOpen = false; 
  isLoading = true;
  private requestedPageNumber: number | null = null;
  private preferTechnicalPage = false;
  private requestedPart: RequestedPartSelection = { itemId: null, refNo: null, partCode: null };
  private pendingAutoSelect = false;
  private attemptedHotspotRedirect = false;

  // Zoom & Pan
  transform = { x: 0, y: 0, scale: 1 };
  isDragging = false;
  startX = 0;
  startY = 0;

  ngOnInit() {
    // 🔥 DÜZELTME: ID'yi URL'den alıp hemen değişkene atıyoruz.
    this.catalogId = this.route.snapshot.paramMap.get('id');
    const pageIndexStr = this.route.snapshot.paramMap.get('pageIndex');
    const pageNumberQuery = this.route.snapshot.queryParamMap.get('page');
    const parsedPageNumber = pageNumberQuery ? Number.parseInt(pageNumberQuery, 10) : NaN;
    this.requestedPageNumber = Number.isFinite(parsedPageNumber) && parsedPageNumber > 0 ? parsedPageNumber : null;
    this.preferTechnicalPage = this.route.snapshot.queryParamMap.get('preferTech') === '1';
    const itemIdQuery = this.route.snapshot.queryParamMap.get('itemId');
    const refQuery = this.route.snapshot.queryParamMap.get('ref');
    const codeQuery = this.route.snapshot.queryParamMap.get('code');
    this.requestedPart = {
      itemId: itemIdQuery ? itemIdQuery.trim() : null,
      refNo: refQuery ? refQuery.trim() : null,
      partCode: codeQuery ? codeQuery.trim() : null,
    };
    this.pendingAutoSelect = this.hasRequestedPartSelection();

    const tokenParam = this.route.snapshot.queryParamMap.get('token');
    if (!tokenParam) {
      this.isLoading = false;
      console.error('Public token bulunamadı.');
      return;
    }
    this.publicToken = tokenParam;
    this.publicQueryParams = { token: this.publicToken };
    this.cartService.setScope(`public:${this.publicToken}`);
    
    // Eğer ID varsa yüklemeyi başlat
    if (this.catalogId) {
      this.activeGroupIndex = pageIndexStr ? parseInt(pageIndexStr, 10) : 0;
      this.loadCatalog(this.catalogId);
    }
  }

  // --- 1. KATALOG VE GRUPLARI YÜKLE ---
  loadCatalog(id: string) {
    this.isLoading = true;
    this.catalogService.getCatalogById(id, { publicToken: this.publicToken! }).subscribe({
      next: (data) => {
        this.catalog = data;
        this.prepareGroups();
        
        // İlk grubu veya URL'den gelen sayfayı seç
        if (this.groups.length > 0) {
          const byTechPageNumber = (this.requestedPageNumber !== null && this.preferTechnicalPage)
            ? this.groups.find(g => g.pageNumber === this.requestedPageNumber && g.isTechnicalDrawing)
            : null;
          const byPageNumber = this.requestedPageNumber !== null
            ? this.groups.find(g => g.pageNumber === this.requestedPageNumber)
            : null;
          const byIndex = this.groups.find(g => g.pageIndex === this.activeGroupIndex) || null;
          const targetGroup = byTechPageNumber || byPageNumber || byIndex || this.groups[0];
          this.selectGroup(targetGroup);
        } else {
           this.isLoading = false;
        }
      },
      error: (err) => { console.error(err); this.isLoading = false; }
    });
  }

  prepareGroups() {
    if (!this.catalog?.pages) return;
    this.groups = this.catalog.pages.map((page, index) => ({
      pageIndex: index,
      pageNumber: page.pageNumber,
      title: page.aiDescription || `Sayfa ${page.pageNumber}`,
      imageUrl: page.imageUrl,
      isTechnicalDrawing: page.isTechnicalDrawing === true
    }));
  }

  // --- 2. SAYFA SEÇİMİ VE VERİ YÜKLEME ---
  selectGroup(group: ViewerGroup) {
    // Catalog yüklü değilse işlem yapma (Typescript kontrolü)
    if (!this.catalog) return;
    if (!this.catalog.pages) return;
    
    this.activeGroupIndex = group.pageIndex;
    this.activePage = this.catalog.pages[group.pageIndex];
    
    // UI Sıfırla
    this.resetZoom();
    this.selectedPartLabel = null;
    this.selectedItem = null;
    this.selectedProductId = null;
    this.searchQuery = ''; 
    this.isLoading = true;

    // Backend'den Sayfa İçeriğini Çek
    this.catalogService.getPageItems(this.catalog.id, group.pageNumber.toString(), { publicToken: this.publicToken! }).subscribe({
      next: (items) => {
        this.pageItems = (items || []).map((item) => ({
          ...item,
          catalogItemId: this.buildCartItemId(item)
        }));
        this.filteredItems = [...this.pageItems];
        
        // Hotspotları eşleştir
        this.matchHotspotsLocally();

        if (this.pendingAutoSelect) {
          const matched = this.tryApplyRequestedPartSelection();
          const hotspotOnlySelected = !matched ? this.trySelectRequestedHotspotOnActivePage() : false;
          if (!matched && !hotspotOnlySelected && !this.attemptedHotspotRedirect) {
              const fallbackGroup = this.findHotspotGroupForRequestedPart();
              if (fallbackGroup && fallbackGroup.pageIndex !== this.activeGroupIndex) {
                this.attemptedHotspotRedirect = true;
                this.selectGroup(fallbackGroup);
                return;
              }
          }
          this.pendingAutoSelect = false;
        }

        this.isLoading = false;
      },
      error: (err) => {
        console.error('Sayfa verisi alınamadı', err);
        this.pageItems = [];
        this.filteredItems = [];
        this.isLoading = false;
      }
    });
  }

  // Hotspotları görselleştirmek için basit eşleştirme
  matchHotspotsLocally() {
    if (!this.activePage?.hotspots) return;
    
    this.activePage.hotspots.forEach(spot => {
        const matchedItem = this.pageItems.find(p => p.refNo === spot.label || p.partCode === spot.label);
        
        if (matchedItem) {
            spot.description = matchedItem.partName;
            spot.partNumber = matchedItem.partCode;
            if(matchedItem.isStocked) spot.productId = matchedItem.productId;
        }
    });
  }

  // --- SEPET & SİPARİŞ ---
  addToCart(item: CatalogPageItem) {
    const productToAdd: CatalogPageItem = {
      catalogItemId: this.buildCartItemId(item),
      refNo: item.refNo,
      partCode: item.partCode,
      partName: item.partName,
      description: item.description,
      isStocked: true,
      productId: item.productId,
      price: item.price,
      localName: item.localName
    };

    this.cartService.addToCart(productToAdd);
    this.isCartOpen = true; 
  }

  goCheckout() {
    if (!this.publicToken) return;
    this.router.navigate(['/public-view', this.publicToken, 'checkout']);
  }

  // --- ARAMA & FİLTRELEME ---
  onSearch(query: string) {
    this.searchQuery = query;
    if (!query) {
      this.filteredItems = [...this.pageItems];
      return;
    }
    const lowerQuery = query.toLowerCase();
    this.filteredItems = this.pageItems.filter(p => 
      (p.partCode?.toLowerCase().includes(lowerQuery)) || 
      (p.refNo?.includes(lowerQuery)) ||
      (p.partName?.toLowerCase().includes(lowerQuery)) ||
      (p.localName?.toLowerCase().includes(lowerQuery))
    );
  }

  // --- ETKİLEŞİM ---
  onHotspotClick(label: string) {
    this.selectedPartLabel = label;

    // refNo VEYA partCode ile eşleştir
    this.selectedItem = this.pageItems.find(
      p => p.refNo === label || p.partCode === label
    ) || null;

    if (this.selectedItem && this.selectedItem.isStocked) {
      this.selectedProductId = this.selectedItem.productId || null;
    } else {
      this.selectedProductId = null;
    }

    if (this.selectedItem) {
      // Arama filtresini temizle ki highlight görünsün
      if (this.searchQuery) {
        this.searchQuery = '';
        this.filteredItems = [...this.pageItems];
      }

      setTimeout(() => {
        const row = document.getElementById('row-' + this.selectedItem?.catalogItemId);
        if (row) {
          row.scrollIntoView({ behavior: 'smooth', block: 'center' });
          // Kısa bir "flash" efekti için geçici class ekle
          row.classList.add('hotspot-flash');
          setTimeout(() => row.classList.remove('hotspot-flash'), 1200);
        }
      }, 50);
    }
  }

  onItemClick(item: CatalogPageItem) {
    this.selectItemAndHighlight(item, false, true);
  }

  // --- ZOOM & PAN ---
  onWheel(event: WheelEvent) {
    event.preventDefault();
    const direction = event.deltaY > 0 ? -1 : 1;
    let newScale = this.transform.scale + (direction * 0.1);
    this.transform.scale = Math.min(Math.max(0.5, newScale), 5); 
  }

  startDrag(event: MouseEvent) {
    this.isDragging = true;
    this.startX = event.clientX - this.transform.x;
    this.startY = event.clientY - this.transform.y;
  }

  onDrag(event: MouseEvent) {
    if (!this.isDragging) return;
    this.transform.x = event.clientX - this.startX;
    this.transform.y = event.clientY - this.startY;
  }

  endDrag() { this.isDragging = false; }
  resetZoom() { this.transform = { x: 0, y: 0, scale: 1 }; }

  private isEmptyGuid(value: any): boolean {
    const raw = String(value ?? '').trim();
    return raw === '' || raw === '00000000-0000-0000-0000-000000000000';
  }

  private buildCartItemId(item: CatalogPageItem): string {
    const catalogItemId = String(item?.catalogItemId ?? '').trim();
    if (!this.isEmptyGuid(catalogItemId)) return catalogItemId;

    const productId = String(item?.productId ?? '').trim();
    if (!this.isEmptyGuid(productId)) return `product:${productId}`;

    const partCode = String(item?.partCode ?? '').trim().toUpperCase();
    if (partCode) return `code:${partCode}`;

    const refNo = String(item?.refNo ?? '').trim().toUpperCase();
    if (refNo) return `ref:${refNo}`;

    return `tmp:${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  }

  private hasRequestedPartSelection(): boolean {
    return !!(this.requestedPart.itemId || this.requestedPart.refNo || this.requestedPart.partCode);
  }

  private normalizeValue(value: string | null | undefined): string {
    return String(value ?? '').trim().toLowerCase();
  }

  private tryApplyRequestedPartSelection(): boolean {
    const targetItemId = this.normalizeValue(this.requestedPart.itemId);
    const targetRef = this.normalizeValue(this.requestedPart.refNo);
    const targetCode = this.normalizeValue(this.requestedPart.partCode);

    if (!targetItemId && !targetRef && !targetCode) return false;

    const matchedItem = this.pageItems.find((item) => {
      const itemId = this.normalizeValue(item.catalogItemId);
      const refNo = this.normalizeValue(item.refNo);
      const code = this.normalizeValue(item.partCode);

      if (targetItemId && itemId === targetItemId) return true;
      if (targetRef && refNo === targetRef) return true;
      if (targetCode && code === targetCode) return true;
      return false;
    });

    if (!matchedItem) return false;
    const hasHotspotOnActivePage = this.activePageHasMatchingHotspot(matchedItem.refNo, matchedItem.partCode);
    this.selectItemAndHighlight(matchedItem, false, hasHotspotOnActivePage);
    return hasHotspotOnActivePage;
  }

  private trySelectRequestedHotspotOnActivePage(): boolean {
    const targetRef = this.normalizeValue(this.requestedPart.refNo);
    const targetCode = this.normalizeValue(this.requestedPart.partCode);
    if (!targetRef && !targetCode) return false;
    if (!this.activePage?.hotspots?.length) return false;

    const hotspot = this.activePage.hotspots.find((spot) => {
      const label = this.normalizeValue(spot.label);
      if (!label) return false;
      return (targetRef && label === targetRef) || (targetCode && label === targetCode);
    });
    if (!hotspot) return false;

    this.selectedPartLabel = hotspot.label;
    this.selectedItem = null;
    this.selectedProductId = null;
    this.focusSelectedHotspotInViewport();
    return true;
  }

  private activePageHasMatchingHotspot(refNo: string | null | undefined, partCode: string | null | undefined): boolean {
    if (!this.activePage?.hotspots?.length) return false;
    const refNorm = this.normalizeValue(refNo);
    const codeNorm = this.normalizeValue(partCode);
    if (!refNorm && !codeNorm) return false;

    return this.activePage.hotspots.some((spot) => {
      const label = this.normalizeValue(spot.label);
      if (!label) return false;
      return (refNorm && label === refNorm) || (codeNorm && label === codeNorm);
    });
  }

  private findHotspotGroupForRequestedPart(): ViewerGroup | null {
    if (!this.catalog?.pages?.length || this.groups.length === 0) return null;

    const targetRef = this.normalizeValue(this.requestedPart.refNo);
    const targetCode = this.normalizeValue(this.requestedPart.partCode);
    if (!targetRef && !targetCode) return null;

    for (const group of this.groups) {
      const page = this.catalog.pages[group.pageIndex];
      const hotspots = page?.hotspots ?? [];
      const hasMatch = hotspots.some((spot) => {
        const label = this.normalizeValue(spot.label);
        if (!label) return false;
        return (targetRef && label === targetRef) || (targetCode && label === targetCode);
      });
      if (hasMatch) return group;
    }

    return null;
  }

  private selectItemAndHighlight(item: CatalogPageItem, scrollRow: boolean, focusTechnicalDrawing = false) {
    this.selectedItem = item;

    const refNo = this.normalizeValue(item.refNo);
    const partCode = this.normalizeValue(item.partCode);
    const matchedHotspotLabel = this.activePage?.hotspots?.find((spot) => {
      const label = this.normalizeValue(spot.label);
      return (refNo && label === refNo) || (partCode && label === partCode);
    })?.label ?? null;

    this.selectedPartLabel = matchedHotspotLabel || item.refNo || item.partCode || null;

    if (item.isStocked) {
      this.selectedProductId = item.productId || null;
    } else {
      this.selectedProductId = null;
    }

    if (focusTechnicalDrawing) {
      this.focusSelectedHotspotInViewport();
    }

    if (!scrollRow) return;
    setTimeout(() => {
      const row = document.getElementById('row-' + item.catalogItemId);
      if (!row) return;
      row.scrollIntoView({ behavior: 'smooth', block: 'center' });
      row.classList.add('hotspot-flash');
      setTimeout(() => row.classList.remove('hotspot-flash'), 1200);
    }, 50);
  }

  private focusSelectedHotspotInViewport() {
    if (!this.activePage?.hotspots?.length) return;
    const label = this.normalizeValue(this.selectedPartLabel);
    if (!label) return;

    const hotspot = this.activePage.hotspots.find((spot) => this.normalizeValue(spot.label) === label);
    if (!hotspot) return;

    // Teknik resimde parçayı merkeze almak için hafif zoom + center offset uygula.
    this.transform = { x: 0, y: 0, scale: 1.25 };
    setTimeout(() => {
      const viewport = this.viewportRef?.nativeElement;
      const image = this.technicalImageRef?.nativeElement;
      if (!viewport || !image) return;

      const viewportRect = viewport.getBoundingClientRect();
      const imageRect = image.getBoundingClientRect();

      const hotspotCenterX = imageRect.left + ((hotspot.left + hotspot.width / 2) / 100) * imageRect.width;
      const hotspotCenterY = imageRect.top + ((hotspot.top + hotspot.height / 2) / 100) * imageRect.height;

      const viewportCenterX = viewportRect.left + viewportRect.width / 2;
      const viewportCenterY = viewportRect.top + viewportRect.height / 2;

      this.transform = {
        ...this.transform,
        x: this.transform.x + (viewportCenterX - hotspotCenterX),
        y: this.transform.y + (viewportCenterY - hotspotCenterY),
      };
    }, 30);
  }
}
