import { Component, OnInit, OnDestroy, inject, ElementRef, ViewChild, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CatalogService, Catalog, CatalogPage, CatalogPageItem } from '../../core/services/catalog.service';
import { CartService } from '../../core/services/cart.service';
import { environment } from '../../../environments/environment';

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

type StockFilter = 'all' | 'in' | 'out';
type SortMode = 'ref' | 'name' | 'stock';
type PartsPaneSnapLevel = 0 | 1 | 2;

@Component({
  selector: 'app-public-catalog-viewer',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  templateUrl: './public-catalog-viewer.html',
  styleUrls: ['./public-catalog-viewer.css']
})
export class PublicCatalogViewerComponent implements OnInit, OnDestroy {
  
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private catalogService = inject(CatalogService);
  public cartService = inject(CartService); 
  @ViewChild('viewport') private viewportRef?: ElementRef<HTMLDivElement>;
  @ViewChild('technicalImage') private technicalImageRef?: ElementRef<HTMLImageElement>;
  @ViewChild('partsPane') private partsPaneRef?: ElementRef<HTMLElement>;
  @ViewChild('partsList') private partsListRef?: ElementRef<HTMLDivElement>;

  // 🔥 DÜZELTME: Bu değişken artık class property olarak burada!
  // HTML'deki [routerLink] bunu kullanacak.
  catalogId: string | null = null;
  publicToken: string | null = null;
  publicQueryParams: any = {};
  canUseEcommerce = false;

  catalog: Catalog | null = null;
  groups: ViewerGroup[] = [];
  
  activeGroupIndex: number = 0;
  activePage: CatalogPage | null = null;
  
  // Kütüphane Verileri (Stoklu + Stoksuz)
  pageItems: CatalogPageItem[] = [];
  filteredItems: CatalogPageItem[] = [];
  searchInput: string = '';
  searchQuery: string = '';
  stockFilter: StockFilter = 'all';
  sortMode: SortMode = 'ref';
  
  // Seçim Durumları
  selectedPartLabel: string | null = null;
  selectedItem: CatalogPageItem | null = null;
  selectedProductId: string | null = null;

  isMobileSidebarOpen = false;
  isSidebarCollapsed = false;
  isPageListExpanded = true;
  isMobilePartsExpanded = false;
  isMobilePartsMid = false;
  isCartOpen = false; 
  isLoading = true;
  private requestedPageNumber: number | null = null;
  private preferTechnicalPage = false;
  private requestedPart: RequestedPartSelection = { itemId: null, refNo: null, partCode: null };
  private pendingAutoSelect = false;
  private attemptedHotspotRedirect = false;
  private readonly minSelectionQty = 1;
  private readonly maxSelectionQty = 99;
  private itemSelectionQty: Record<string, number> = {};
  private searchDebounceTimer: ReturnType<typeof setTimeout> | null = null;
  private hotspotTooltipClasses: Record<string, string> = {};
  private touchMode: 'none' | 'pan' | 'pinch' = 'none';
  private touchStartX = 0;
  private touchStartY = 0;
  private touchStartTransformX = 0;
  private touchStartTransformY = 0;
  private swipeCandidate = false;
  private pinchStartDistance = 0;
  private pinchStartScale = 1;
  private partsSheetTouchStartY = 0;
  private partsSheetTouchStartX = 0;
  private partsSheetGestureActive = false;
  private partsSheetStartedInList = false;
  private partsSheetGestureFromHandle = false;
  private partsSheetStartHeightPx = 0;
  private partsSheetStartSnapLevel: PartsPaneSnapLevel = 0;
  private partsSheetCurrentHeightPx: number | null = null;
  private partsSheetLastDeltaY = 0;
  isPartsSheetDragging = false;

  // Zoom & Pan
  transform = { x: 0, y: 0, scale: 1 };
  isDragging = false;
  startX = 0;
  startY = 0;

  get partsPaneHeightPx(): number | null {
    if (!this.isMobileViewport()) return null;
    return this.partsSheetCurrentHeightPx;
  }

  ngOnDestroy(): void {
    if (this.searchDebounceTimer) {
      clearTimeout(this.searchDebounceTimer);
      this.searchDebounceTimer = null;
    }
  }

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
    this.cartService.setPublicToken(this.publicToken);
    this.loadStorefrontFeatures();
    
    // Eğer ID varsa yüklemeyi başlat
    if (this.catalogId) {
      this.activeGroupIndex = pageIndexStr ? parseInt(pageIndexStr, 10) : 0;
      this.loadCatalog(this.catalogId);
    }
  }

  private loadStorefrontFeatures() {
    if (!this.publicToken) return;
    this.catalogService.getPublicStorefront(this.publicToken).subscribe({
      next: (res) => {
        this.canUseEcommerce = environment.features.enableEcommerce && res?.ecommerceEnabled === true;
        if (!this.canUseEcommerce) {
          this.isCartOpen = false;
        }
      },
      error: () => {
        this.canUseEcommerce = false;
        this.isCartOpen = false;
      }
    });
  }

  get showCartSidebar(): boolean {
    return this.canUseEcommerce;
  }

  get targetedActionLabel(): string {
    return 'Sepete';
  }

  showsPrimaryAction(_item: CatalogPageItem): boolean {
    return this.canUseEcommerce;
  }

  isPrimaryActionDisabled(item: CatalogPageItem): boolean {
    return item.canAddToCart === false;
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
      error: (err) => {
        console.error(err);
        this.isLoading = false;
      }
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
    if (this.isMobileViewport()) {
      this.isMobileSidebarOpen = false;
      this.applyPartsPaneSnapLevel(0);
      this.partsSheetCurrentHeightPx = null;
      this.isPartsSheetDragging = false;
    }
    
    // UI Sıfırla
    this.resetZoom();
    this.selectedPartLabel = null;
    this.selectedItem = null;
    this.selectedProductId = null;
    this.searchInput = '';
    this.searchQuery = '';
    this.stockFilter = 'all';
    this.sortMode = 'ref';
    this.hotspotTooltipClasses = {};
    this.isLoading = true;

    // Backend'den Sayfa İçeriğini Çek
    this.catalogService.getPageItems(this.catalog.id, group.pageNumber.toString(), { publicToken: this.publicToken! }).subscribe({
      next: (items) => {
        this.pageItems = (items || []).map((item) => ({
          ...item,
          catalogItemId: this.buildCartItemId(item),
          availabilityPending: false
        }));
        this.applyFiltersAndSort();
        
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
    if (!this.canUseEcommerce) return;
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

  addItemWithQty(item: CatalogPageItem) {
    if (!this.canUseEcommerce) return;

    const qty = this.getSelectionQty(item);
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

    this.cartService.addToCart(productToAdd, qty);
    this.isCartOpen = true;
    this.itemSelectionQty[this.buildCartItemId(item)] = this.minSelectionQty;
  }

  getSelectionQty(item: CatalogPageItem): number {
    const key = this.buildCartItemId(item);
    return this.itemSelectionQty[key] ?? this.minSelectionQty;
  }

  increaseSelectionQty(item: CatalogPageItem) {
    const key = this.buildCartItemId(item);
    const current = this.getSelectionQty(item);
    this.itemSelectionQty[key] = Math.min(current + 1, this.maxSelectionQty);
  }

  decreaseSelectionQty(item: CatalogPageItem) {
    const key = this.buildCartItemId(item);
    const current = this.getSelectionQty(item);
    this.itemSelectionQty[key] = Math.max(current - 1, this.minSelectionQty);
  }

  getStockLabel(item: CatalogPageItem): string {
    if (item?.availabilityPending) return 'Kontrol Ediliyor';
    if (item?.availabilityLabel) return item.availabilityLabel;
    if (item?.stockStatus === 'available_to_order') return 'Siparişe Uygun';
    if (item?.isStocked === true) return 'Stokta Var';
    if (item?.isStocked === false) return 'Stokta Yok';
    return 'Belirsiz';
  }

  getStockBadgeClass(item: CatalogPageItem): string {
    if (item?.stockStatus === 'available_to_order') return 'stock-pill stock-pill-unknown';
    if (item?.isStocked === true) return 'stock-pill stock-pill-ok';
    if (item?.isStocked === false) return 'stock-pill stock-pill-no';
    return 'stock-pill stock-pill-unknown';
  }

  isSelectedFromHotspot(item: CatalogPageItem): boolean {
    const selected = this.normalizeValue(this.selectedPartLabel);
    if (!selected) return false;
    return selected === this.normalizeValue(item.refNo) || selected === this.normalizeValue(item.partCode);
  }

  getHotspotPartName(label: string): string {
    const norm = this.normalizeValue(label);
    if (!norm) return 'Parça';

    const matched = this.pageItems.find((item) => {
      const refNo = this.normalizeValue(item.refNo);
      const code = this.normalizeValue(item.partCode);
      return refNo === norm || code === norm;
    });

    return matched?.localName || matched?.partName || matched?.partCode || 'Parça';
  }

  getHotspotTooltipClass(spot: { top: number; left: number; width: number; height: number; label?: string }, index: number): string {
    const key = this.getHotspotTooltipKey(spot, index);
    const dynamicClass = this.hotspotTooltipClasses[key];
    if (dynamicClass) return dynamicClass;

    return this.getFallbackHotspotTooltipClass(spot);
  }

  updateTooltipPosition(event: Event, spot: { top: number; left: number; width: number; height: number; label?: string }, index: number) {
    const hotspotElement = event.currentTarget as HTMLElement | null;
    const viewportElement = this.viewportRef?.nativeElement;
    if (!hotspotElement || !viewportElement) return;

    const tooltipElement = hotspotElement.querySelector('.hotspot-tooltip') as HTMLElement | null;
    if (!tooltipElement) return;

    const key = this.getHotspotTooltipKey(spot, index);
    this.hotspotTooltipClasses[key] = this.calculateTooltipClass(hotspotElement, tooltipElement, viewportElement);
  }

  private getFallbackHotspotTooltipClass(spot: { top: number; left: number; width: number; height: number }): string {
    const top = Number.isFinite(spot.top) ? spot.top : 0;
    const left = Number.isFinite(spot.left) ? spot.left : 0;
    const width = Number.isFinite(spot.width) ? spot.width : 0;

    const center = left + (width / 2);
    const verticalClass = top < 16 ? 'tooltip-below' : 'tooltip-above';

    if (center < 18) return `${verticalClass} tooltip-left`;
    if (center > 82) return `${verticalClass} tooltip-right`;
    return `${verticalClass} tooltip-center`;
  }

  private calculateTooltipClass(hotspotEl: HTMLElement, tooltipEl: HTMLElement, viewportEl: HTMLElement): string {
    const viewportRect = viewportEl.getBoundingClientRect();
    const hotspotRect = hotspotEl.getBoundingClientRect();
    const tooltipRect = tooltipEl.getBoundingClientRect();

    const tooltipWidth = tooltipRect.width > 0 ? tooltipRect.width : 220;
    const tooltipHeight = tooltipRect.height > 0 ? tooltipRect.height : 56;
    const edgePadding = 10;
    const gap = 8;

    const spaceAbove = hotspotRect.top - viewportRect.top;
    const spaceBelow = viewportRect.bottom - hotspotRect.bottom;
    const verticalClass =
      spaceAbove < (tooltipHeight + gap + edgePadding) && spaceBelow > spaceAbove
        ? 'tooltip-below'
        : 'tooltip-above';

    const hotspotCenterX = hotspotRect.left + (hotspotRect.width / 2);
    const centeredLeft = hotspotCenterX - (tooltipWidth / 2);
    const centeredRight = hotspotCenterX + (tooltipWidth / 2);

    let horizontalClass = 'tooltip-center';
    if (centeredLeft < viewportRect.left + edgePadding) horizontalClass = 'tooltip-left';
    if (centeredRight > viewportRect.right - edgePadding) horizontalClass = 'tooltip-right';

    return `${verticalClass} ${horizontalClass}`;
  }

  private getHotspotTooltipKey(spot: { label?: string }, index: number): string {
    const label = this.normalizeValue(spot.label ?? '');
    return `${this.activeGroupIndex}:${index}:${label}`;
  }

  goCheckout() {
    if (!this.canUseEcommerce) return;
    if (!this.publicToken) return;
    this.router.navigate(['/p', this.publicToken, 'checkout']);
  }

  get currentPagePositionText(): string {
    const pageNo = this.activePage?.pageNumber ?? (this.activeGroupIndex + 1);
    const total = this.groups.length || 0;
    return `${pageNo}/${total}`;
  }

  goToPreviousPage() {
    if (!this.groups.length) return;
    const prevIndex = Math.max(this.activeGroupIndex - 1, 0);
    if (prevIndex === this.activeGroupIndex) return;
    this.selectGroup(this.groups[prevIndex]);
  }

  goToNextPage() {
    if (!this.groups.length) return;
    const nextIndex = Math.min(this.activeGroupIndex + 1, this.groups.length - 1);
    if (nextIndex === this.activeGroupIndex) return;
    this.selectGroup(this.groups[nextIndex]);
  }

  onPartsHandleTouchStart(event: TouchEvent) {
    if (!this.isMobileViewport() || event.touches.length !== 1) return;
    const touchTarget = event.target as HTMLElement | null;
    const startedFromHandle = !!touchTarget?.closest('.parts-handle');
    if (!startedFromHandle && this.shouldIgnorePartsSheetGestureStart(touchTarget)) return;

    this.partsSheetGestureActive = true;
    this.partsSheetGestureFromHandle = startedFromHandle;
    this.partsSheetStartedInList = !!touchTarget?.closest('.parts-list');
    this.partsSheetTouchStartX = event.touches[0].clientX;
    this.partsSheetTouchStartY = event.touches[0].clientY;
    this.partsSheetLastDeltaY = 0;
    this.partsSheetStartSnapLevel = this.getCurrentPartsPaneSnapLevel();
    this.partsSheetStartHeightPx = this.getCurrentPartsPaneHeightPx();
    this.partsSheetCurrentHeightPx = this.partsSheetStartHeightPx;
    this.isPartsSheetDragging = false;
  }

  onPartsHandleTouchMove(event: TouchEvent) {
    if (!this.isMobileViewport() || !this.partsSheetGestureActive || event.touches.length !== 1) return;

    const currentX = event.touches[0].clientX;
    const currentY = event.touches[0].clientY;
    const deltaX = currentX - this.partsSheetTouchStartX;
    const deltaY = currentY - this.partsSheetTouchStartY;
    this.partsSheetLastDeltaY = deltaY;

    if (Math.abs(deltaY) <= Math.abs(deltaX)) return;

    const wantsExpand = deltaY < 0;
    const wantsCollapse = deltaY > 0;
    const currentSnapLevel = this.getCurrentPartsPaneSnapLevel();
    if (this.partsSheetStartedInList && !this.partsSheetGestureFromHandle && currentSnapLevel > 0 && wantsExpand) {
      return;
    }

    const canCollapse = this.partsSheetGestureFromHandle || !this.partsSheetStartedInList || this.isPartsListAtTop();
    if (wantsCollapse && !canCollapse) return;

    const minHeight = this.getPartsPaneCollapsedHeightPx();
    const maxHeight = this.getPartsPaneExpandedHeightPx();
    const nextHeight = this.clamp(this.partsSheetStartHeightPx - deltaY, minHeight, maxHeight);

    this.isPartsSheetDragging = true;
    this.partsSheetCurrentHeightPx = nextHeight;
    event.preventDefault();
  }

  onPartsHandleTouchEnd(event: TouchEvent) {
    if (!this.isMobileViewport() || !this.partsSheetGestureActive || !this.partsSheetTouchStartY) return;
    const endY = event.changedTouches[0]?.clientY ?? this.partsSheetTouchStartY;
    const deltaY = endY - this.partsSheetTouchStartY;
    const canCollapse = this.partsSheetGestureFromHandle || !this.partsSheetStartedInList || this.isPartsListAtTop();
    const swipeThreshold = 45;

    if (this.isPartsSheetDragging && this.partsSheetCurrentHeightPx !== null) {
      const strongSwipe = Math.abs(this.partsSheetLastDeltaY) > 36 || Math.abs(deltaY) > swipeThreshold;
      const currentLevel = this.partsSheetStartSnapLevel;
      let targetLevel: PartsPaneSnapLevel;

      if (strongSwipe) {
        if (this.partsSheetLastDeltaY < 0) {
          targetLevel = this.clampSnapLevel((currentLevel + 1) as PartsPaneSnapLevel);
        } else if (canCollapse) {
          targetLevel = this.clampSnapLevel((currentLevel - 1) as PartsPaneSnapLevel);
        } else {
          targetLevel = currentLevel;
        }
      } else {
        targetLevel = this.getNearestPartsPaneSnapLevel(this.partsSheetCurrentHeightPx, canCollapse);
      }
      this.snapPartsPane(targetLevel);
    } else {
      const currentLevel = this.getCurrentPartsPaneSnapLevel();
      let targetLevel: PartsPaneSnapLevel = currentLevel;
      if (deltaY < -swipeThreshold) {
        targetLevel = this.clampSnapLevel((currentLevel + 1) as PartsPaneSnapLevel);
      } else if (deltaY > swipeThreshold && canCollapse) {
        targetLevel = this.clampSnapLevel((currentLevel - 1) as PartsPaneSnapLevel);
      }

      if (targetLevel !== currentLevel) {
        this.snapPartsPane(targetLevel);
      } else {
        this.partsSheetCurrentHeightPx = null;
        this.isPartsSheetDragging = false;
      }
    }
    this.resetPartsSheetGestureState();
  }

  onPartsHandleTouchCancel() {
    this.partsSheetCurrentHeightPx = null;
    this.isPartsSheetDragging = false;
    this.resetPartsSheetGestureState();
  }

  // --- ARAMA & FİLTRELEME ---
  onSearchInputChange(query: string) {
    this.searchInput = query;
    if (this.searchDebounceTimer) {
      clearTimeout(this.searchDebounceTimer);
    }
    this.searchDebounceTimer = setTimeout(() => {
      this.searchQuery = this.searchInput.trim();
      this.applyFiltersAndSort();
    }, 200);
  }

  clearSearch() {
    this.searchInput = '';
    this.searchQuery = '';
    if (this.searchDebounceTimer) {
      clearTimeout(this.searchDebounceTimer);
      this.searchDebounceTimer = null;
    }
    this.applyFiltersAndSort();
  }

  setStockFilter(filter: StockFilter) {
    this.stockFilter = filter;
    this.applyFiltersAndSort();
  }

  setSortMode(mode: SortMode) {
    this.sortMode = mode;
    this.applyFiltersAndSort();
  }

  private applyFiltersAndSort() {
    const query = this.searchQuery.toLowerCase();
    let items = [...this.pageItems];

    if (query) {
      items = items.filter((p) =>
        (p.partCode?.toLowerCase().includes(query)) ||
        (p.refNo?.toLowerCase().includes(query)) ||
        (p.partName?.toLowerCase().includes(query)) ||
        (p.localName?.toLowerCase().includes(query))
      );
    }

    if (this.stockFilter === 'in') {
      items = items.filter((p) => p.isStocked === true);
    } else if (this.stockFilter === 'out') {
      items = items.filter((p) => p.isStocked === false);
    }

    const stockRank = (item: CatalogPageItem): number => {
      if (item.isStocked === true) return 0;
      if (item.isStocked === false) return 2;
      return 1;
    };

    items.sort((a, b) => {
      if (this.sortMode === 'name') {
        return (a.localName || a.partName || '').localeCompare((b.localName || b.partName || ''), 'tr', { sensitivity: 'base' });
      }
      if (this.sortMode === 'stock') {
        const rankDiff = stockRank(a) - stockRank(b);
        if (rankDiff !== 0) return rankDiff;
      }
      const refA = Number.parseInt(String(a.refNo || ''), 10);
      const refB = Number.parseInt(String(b.refNo || ''), 10);
      if (Number.isFinite(refA) && Number.isFinite(refB)) return refA - refB;
      return String(a.refNo || '').localeCompare(String(b.refNo || ''), 'tr', { numeric: true, sensitivity: 'base' });
    });

    this.filteredItems = items;
  }

  get searchResultText(): string {
    return `${this.filteredItems.length} sonuç bulundu`;
  }

  highlightMatch(value: string | null | undefined): string {
    const input = String(value || '');
    if (!this.searchQuery.trim()) return this.escapeHtml(input);

    const escaped = this.escapeHtml(input);
    const terms = this.searchQuery
      .trim()
      .split(/\s+/)
      .filter((term) => term.length > 0)
      .map((term) => this.escapeRegExp(this.escapeHtml(term)));

    if (terms.length === 0) return escaped;
    const pattern = new RegExp(`(${terms.join('|')})`, 'gi');
    return escaped.replace(pattern, '<mark class="search-mark">$1</mark>');
  }

  askAiFromSearch() {
    if (!this.publicToken) return;
    this.router.navigate(['/p', this.publicToken], {
      queryParams: this.searchInput.trim() ? { q: this.searchInput.trim() } : undefined
    });
  }

  private escapeHtml(value: string): string {
    return value
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  private escapeRegExp(value: string): string {
    return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  }

  // --- ETKİLEŞİM ---
  onHotspotClick(label: string, event?: Event, spotIndex?: number) {
    this.selectedPartLabel = label;
    if (event && Number.isInteger(spotIndex)) {
      const spot = this.activePage?.hotspots?.[spotIndex as number];
      if (spot) this.updateTooltipPosition(event, spot, spotIndex as number);
    }

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
        this.clearSearch();
      }

      setTimeout(() => {
        const row = document.getElementById('row-' + this.selectedItem?.catalogItemId);
        if (row) {
          row.scrollIntoView({ behavior: 'smooth', block: 'center' });
          row.classList.add('row-flash-run');
          setTimeout(() => row.classList.remove('row-flash-run'), 1000);
        }
      }, 50);
    }

    this.recalculateSelectedTooltip();
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

  onTouchStart(event: TouchEvent) {
    if (!this.isMobileViewport()) return;

    if (event.touches.length === 2) {
      this.touchMode = 'pinch';
      this.pinchStartDistance = this.getDistance(event.touches[0], event.touches[1]);
      this.pinchStartScale = this.transform.scale;
      this.swipeCandidate = false;
      return;
    }

    if (event.touches.length === 1) {
      const t = event.touches[0];
      this.touchMode = 'pan';
      this.touchStartX = t.clientX;
      this.touchStartY = t.clientY;
      this.touchStartTransformX = this.transform.x;
      this.touchStartTransformY = this.transform.y;
      this.swipeCandidate = this.transform.scale <= 1.05;
    }
  }

  onTouchMove(event: TouchEvent) {
    if (!this.isMobileViewport()) return;

    if (this.touchMode === 'pinch' && event.touches.length === 2) {
      event.preventDefault();
      const distance = this.getDistance(event.touches[0], event.touches[1]);
      if (this.pinchStartDistance <= 0) return;
      const nextScale = this.pinchStartScale * (distance / this.pinchStartDistance);
      this.transform.scale = Math.min(Math.max(0.5, nextScale), 5);
      return;
    }

    if (this.touchMode === 'pan' && event.touches.length === 1) {
      event.preventDefault();
      const t = event.touches[0];
      const dx = t.clientX - this.touchStartX;
      const dy = t.clientY - this.touchStartY;

      if (this.transform.scale > 1.05) {
        this.transform.x = this.touchStartTransformX + dx;
        this.transform.y = this.touchStartTransformY + dy;
        this.swipeCandidate = false;
        return;
      }

      if (Math.abs(dy) > 18 && Math.abs(dy) > Math.abs(dx)) {
        this.swipeCandidate = false;
      }
    }
  }

  onTouchEnd(event: TouchEvent) {
    if (!this.isMobileViewport()) return;

    if (this.touchMode === 'pan' && this.swipeCandidate && this.transform.scale <= 1.05) {
      const changed = event.changedTouches[0];
      if (changed) {
        const dx = changed.clientX - this.touchStartX;
        const dy = changed.clientY - this.touchStartY;
        if (Math.abs(dx) >= 60 && Math.abs(dy) <= 50) {
          if (dx < 0) this.goToNextPage();
          else this.goToPreviousPage();
        }
      }
    }

    this.touchMode = 'none';
    this.swipeCandidate = false;
  }

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
    this.recalculateSelectedTooltip();
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

    this.recalculateSelectedTooltip();

    if (!scrollRow) return;
    setTimeout(() => {
      const row = document.getElementById('row-' + item.catalogItemId);
      if (!row) return;
      row.scrollIntoView({ behavior: 'smooth', block: 'center' });
      row.classList.add('row-flash-run');
      setTimeout(() => row.classList.remove('row-flash-run'), 1000);
    }, 50);
  }

  private focusSelectedHotspotInViewport() {
    if (!this.activePage?.hotspots?.length) return;
    const label = this.normalizeValue(this.selectedPartLabel);
    if (!label) return;

    const hotspot = this.activePage.hotspots.find((spot) => this.normalizeValue(spot.label) === label);
    if (!hotspot) return;

    // Teknik resimde parçayı merkeze almak için hafif zoom + center offset uygula.
    this.transform = { x: 0, y: 0, scale: 1.3 };
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
      this.recalculateSelectedTooltip();
    }, 30);
  }

  @HostListener('window:resize')
  onWindowResize() {
    this.recalculateSelectedTooltip();
    if (this.isMobileViewport() && this.partsSheetCurrentHeightPx !== null) {
      this.partsSheetCurrentHeightPx = this.getPartsPaneHeightForSnap(this.getCurrentPartsPaneSnapLevel());
    }
  }

  private recalculateSelectedTooltip() {
    setTimeout(() => {
      const viewportElement = this.viewportRef?.nativeElement;
      const selectedLabel = this.normalizeValue(this.selectedPartLabel);
      if (!viewportElement || !selectedLabel || !this.activePage?.hotspots?.length) return;

      const spotIndex = this.activePage.hotspots.findIndex((spot) => this.normalizeValue(spot.label) === selectedLabel);
      if (spotIndex < 0) return;

      const hotspotElements = Array.from(viewportElement.querySelectorAll('.hotspot'));
      const hotspotElement = hotspotElements[spotIndex] as HTMLElement | undefined;
      const tooltipElement = hotspotElement?.querySelector('.hotspot-tooltip') as HTMLElement | null | undefined;
      if (!hotspotElement || !tooltipElement) return;

      const key = this.getHotspotTooltipKey(this.activePage.hotspots[spotIndex], spotIndex);
      this.hotspotTooltipClasses[key] = this.calculateTooltipClass(hotspotElement, tooltipElement, viewportElement);
    }, 0);
  }

  toggleMobileSidebar() {
    this.applyPartsPaneSnapLevel(0);
    this.partsSheetCurrentHeightPx = null;
    this.isPartsSheetDragging = false;
    this.isMobileSidebarOpen = !this.isMobileSidebarOpen;
  }

  closeMobileSidebar() {
    this.isMobileSidebarOpen = false;
  }

  toggleSidebarCollapsed() {
    this.isSidebarCollapsed = !this.isSidebarCollapsed;
  }

  togglePageListExpanded() {
    this.isPageListExpanded = !this.isPageListExpanded;
  }

  private isMobileViewport(): boolean {
    if (typeof window === 'undefined') return false;
    return window.matchMedia('(max-width: 768px)').matches;
  }

  private shouldIgnorePartsSheetGestureStart(target: HTMLElement | null): boolean {
    if (!target) return false;
    return !!target.closest('input, textarea, select, button, a, [contenteditable="true"]');
  }

  private isPartsListAtTop(): boolean {
    const listElement = this.partsListRef?.nativeElement;
    if (!listElement) return true;
    return listElement.scrollTop <= 0;
  }

  private getCurrentPartsPaneHeightPx(): number {
    const paneElement = this.partsPaneRef?.nativeElement;
    if (paneElement) {
      const measured = paneElement.getBoundingClientRect().height;
      if (measured > 0) return measured;
    }

    return this.getPartsPaneHeightForSnap(this.getCurrentPartsPaneSnapLevel());
  }

  private getPartsPaneCollapsedHeightPx(): number {
    const viewportHeight = this.getViewportHeightPx();
    return Math.max(260, Math.round(viewportHeight * 0.58));
  }

  private getPartsPaneExpandedHeightPx(): number {
    const viewportHeight = this.getViewportHeightPx();
    return Math.max(320, viewportHeight - 60);
  }

  private getPartsPaneMidHeightPx(): number {
    const viewportHeight = this.getViewportHeightPx();
    return Math.max(300, Math.round(viewportHeight * 0.78));
  }

  private getPartsPaneHeightForSnap(level: PartsPaneSnapLevel): number {
    if (level === 2) return this.getPartsPaneExpandedHeightPx();
    if (level === 1) return this.getPartsPaneMidHeightPx();
    return this.getPartsPaneCollapsedHeightPx();
  }

  private getViewportHeightPx(): number {
    if (typeof window === 'undefined') return 800;
    return window.innerHeight || document.documentElement.clientHeight || 800;
  }

  private clamp(value: number, min: number, max: number): number {
    return Math.min(Math.max(value, min), max);
  }

  private snapPartsPane(level: PartsPaneSnapLevel) {
    this.applyPartsPaneSnapLevel(level);
    this.isPartsSheetDragging = false;
    this.partsSheetCurrentHeightPx = this.getPartsPaneHeightForSnap(level);

    setTimeout(() => {
      if (!this.isPartsSheetDragging) {
        this.partsSheetCurrentHeightPx = null;
      }
    }, 220);
  }

  private resetPartsSheetGestureState() {
    this.partsSheetTouchStartY = 0;
    this.partsSheetTouchStartX = 0;
    this.partsSheetGestureActive = false;
    this.partsSheetStartedInList = false;
    this.partsSheetGestureFromHandle = false;
    this.partsSheetStartHeightPx = 0;
    this.partsSheetStartSnapLevel = 0;
    this.partsSheetLastDeltaY = 0;
  }

  private getCurrentPartsPaneSnapLevel(): PartsPaneSnapLevel {
    if (this.isMobilePartsExpanded) return 2;
    if (this.isMobilePartsMid) return 1;
    return 0;
  }

  private applyPartsPaneSnapLevel(level: PartsPaneSnapLevel) {
    this.isMobilePartsExpanded = level === 2;
    this.isMobilePartsMid = level === 1;
  }

  private getNearestPartsPaneSnapLevel(height: number, canCollapse: boolean): PartsPaneSnapLevel {
    const candidates: PartsPaneSnapLevel[] = canCollapse ? [0, 1, 2] : [1, 2];
    let best = candidates[0];
    let bestDistance = Number.POSITIVE_INFINITY;

    for (const level of candidates) {
      const distance = Math.abs(this.getPartsPaneHeightForSnap(level) - height);
      if (distance < bestDistance) {
        best = level;
        bestDistance = distance;
      }
    }

    return best;
  }

  private clampSnapLevel(level: number): PartsPaneSnapLevel {
    if (level <= 0) return 0;
    if (level >= 2) return 2;
    return 1;
  }

  private getDistance(t1: Touch, t2: Touch): number {
    const dx = t1.clientX - t2.clientX;
    const dy = t1.clientY - t2.clientY;
    return Math.hypot(dx, dy);
  }
}
