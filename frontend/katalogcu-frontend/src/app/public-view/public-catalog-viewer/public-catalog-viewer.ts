import { Component, OnInit, inject } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CatalogService, Catalog, CatalogPage, CatalogPageItem } from '../../core/services/catalog.service';
import { CartService } from '../../core/services/cart.service';

interface ViewerGroup {
  pageIndex: number;
  pageNumber: number;
  title: string;
  imageUrl: string;
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
  private location = inject(Location);
  private catalogService = inject(CatalogService);
  public cartService = inject(CartService); 

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

  // Zoom & Pan
  transform = { x: 0, y: 0, scale: 1 };
  isDragging = false;
  startX = 0;
  startY = 0;

  ngOnInit() {
    // 🔥 DÜZELTME: ID'yi URL'den alıp hemen değişkene atıyoruz.
    this.catalogId = this.route.snapshot.paramMap.get('id');
    const pageIndexStr = this.route.snapshot.paramMap.get('pageIndex');

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
          const targetGroup = this.groups.find(g => g.pageIndex === this.activeGroupIndex) || this.groups[0];
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
      imageUrl: page.imageUrl
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
        this.isLoading = false;
        
        // Hotspotları eşleştir
        this.matchHotspotsLocally();
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
    this.selectedItem = item;
    this.selectedPartLabel = item.refNo;
    
    // HTML highlight güncelle
    if (item.isStocked) {
        this.selectedProductId = item.productId || null;
    } else {
        this.selectedProductId = null;
    }
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
}
