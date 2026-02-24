import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms'; 
import { ProductService, Product, StockMovement } from '../../core/services/product.service';
import { CatalogService, Catalog } from '../../core/services/catalog.service';

@Component({
  selector: 'app-parts',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  templateUrl: './parts.html',
  styleUrl: './parts.css'
})
export class PartsComponent implements OnInit {
  private productService = inject(ProductService);
  private catalogService = inject(CatalogService);
  
  // Veriler
  allParts: Product[] = [];       // API'den gelen ham liste (Örn: 500 kayıt)
  filteredParts: Product[] = [];  // Filtrelerden geçmiş liste (Örn: Arama sonucu 150 kayıt)
  catalogs: Catalog[] = [];       // Dropdown verisi
  
  // Durumlar
  isLoading = true;
  searchQuery: string = '';
  selectedCatalogId: string = '';
  selectedStockStatus: string = '';
  stockMovements: StockMovement[] = [];
  isMovementLoading = false;

  isAdjustModalOpen = false;
  selectedPartForAdjust: Product | null = null;
  adjustDelta = 0;
  adjustReason = '';
  isAdjustingStock = false;

  // 👇 SAYFALAMA AYARLARI
  currentPage: number = 1;
  pageSize: number = 40; // Sayfa başı maks kayıt

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.isLoading = true;

    // 1. Katalogları Çek
    this.catalogService.getCatalogs().subscribe({
      next: (data) => this.catalogs = data,
      error: (err) => console.error('Katalog hatası', err)
    });

    // 2. Parçaları Çek
    this.productService.getProducts().subscribe({
      next: (data) => {
        this.allParts = data;
        this.applyFilters(); // Veri gelince filtreyi (ve sayfalamayı) başlat
        this.loadStockMovements();
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Parça hatası', err);
        this.isLoading = false;
      }
    });
  }

  // 🔥 HTML'in Döngüye Sokacağı Veri (Sadece 40 Kayıt)
  get paginatedParts(): Product[] {
    const startIndex = (this.currentPage - 1) * this.pageSize;
    const endIndex = startIndex + this.pageSize;
    // Slice, orijinal diziyi bozmadan aralığı alır
    return this.filteredParts.slice(startIndex, endIndex);
  }

  // Toplam Sayfa Sayısı (HTML'de butonları yönetmek için)
  get totalPages(): number {
    return Math.ceil(this.filteredParts.length / this.pageSize);
  }

  // Sayfa Değiştirme Fonksiyonu
  changePage(page: number) {
    if (page >= 1 && page <= this.totalPages) {
      this.currentPage = page;
      // İstersen sayfa değişince en üste kaydır:
      // window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }

  // 🔥 MERKEZİ FİLTRELEME
  applyFilters() {
    let result = this.allParts;

    // 1. Arama Filtresi
    if (this.searchQuery) {
      const lowerQuery = this.searchQuery.toLowerCase();
      result = result.filter(p => 
        (p.code && p.code.toLowerCase().includes(lowerQuery)) || 
        (p.name && p.name.toLowerCase().includes(lowerQuery)) ||
        (p.oemNo && p.oemNo.toLowerCase().includes(lowerQuery))
      );
    }

    // 2. Katalog Filtresi
    if (this.selectedCatalogId) {
      if (this.selectedCatalogId === 'general') {
         // KatalogId'si null veya boş Guid olanlar
         result = result.filter(p => !p.catalogId || p.catalogId === '00000000-0000-0000-0000-000000000000');
      } else {
         result = result.filter(p => p.catalogId === this.selectedCatalogId);
      }
    }

    // 3. Stok Filtresi
    if (this.selectedStockStatus) {
      if (this.selectedStockStatus === 'out') {
        result = result.filter(p => p.stockQuantity === 0);
      } else if (this.selectedStockStatus === 'low') {
        result = result.filter(p => p.stockQuantity > 0 && p.stockQuantity < 10);
      }
    }

    this.filteredParts = result;
    
    // 👇 ÖNEMLİ: Filtre değiştiğinde (yeni arama yapıldığında) her zaman 1. sayfaya dön
    this.currentPage = 1;
  }

  // Eventler
  onSearch(query: string) {
    this.searchQuery = query;
    this.applyFilters();
  }

  onCatalogFilterChange(catalogId: string) {
    this.selectedCatalogId = catalogId;
    this.applyFilters();
  }

  onStockFilterChange(status: string) {
    this.selectedStockStatus = status;
    this.applyFilters();
  }

  // Silme
  deletePart(part: Product) {
    if(confirm(`"${part.code}" kodlu parçayı silmek istediğinize emin misiniz?`)) {
      this.productService.deleteProduct(part.id!).subscribe({
        next: () => {
          this.allParts = this.allParts.filter(p => p.id !== part.id);
          this.applyFilters(); // Listeyi güncelle (Sayfa düzenini korur)
        },
        error: (err) => {
          console.error(err);
          alert('Silme başarısız.');
        }
      });
    }
  }

  loadStockMovements(productId?: string) {
    this.isMovementLoading = true;
    this.productService.getStockMovements({ productId, limit: 50 }).subscribe({
      next: (rows) => {
        this.stockMovements = rows;
        this.isMovementLoading = false;
      },
      error: (err) => {
        console.error('Stok hareketleri alınamadı', err);
        this.isMovementLoading = false;
      }
    });
  }

  openAdjustModal(part: Product) {
    this.selectedPartForAdjust = part;
    this.adjustDelta = 0;
    this.adjustReason = '';
    this.isAdjustModalOpen = true;
  }

  closeAdjustModal() {
    this.isAdjustModalOpen = false;
    this.selectedPartForAdjust = null;
    this.adjustDelta = 0;
    this.adjustReason = '';
    this.isAdjustingStock = false;
  }

  submitAdjustStock() {
    if (!this.selectedPartForAdjust?.id) return;

    if (!Number.isInteger(this.adjustDelta) || this.adjustDelta === 0) {
      alert('Lütfen 0 dışında tam sayı bir miktar girin.');
      return;
    }

    this.isAdjustingStock = true;
    this.productService.adjustStock(this.selectedPartForAdjust.id, {
      deltaQuantity: this.adjustDelta,
      reason: this.adjustReason?.trim() || undefined
    }).subscribe({
      next: (res) => {
        const newQuantity: number = res?.newQuantity;
        const targetId = this.selectedPartForAdjust?.id;
        if (!targetId) return;

        this.allParts = this.allParts.map(p => p.id === targetId ? { ...p, stockQuantity: newQuantity } : p);
        this.applyFilters();
        this.loadStockMovements(targetId);
        this.closeAdjustModal();
      },
      error: (err) => {
        console.error(err);
        alert(err?.error ?? 'Stok güncellenemedi.');
        this.isAdjustingStock = false;
      }
    });
  }

  // UI Yardımcıları
  getStockPercentage(qty: number): number { return Math.min(qty, 100); }
  getStockColorClass(qty: number): string {
    if (qty === 0) return 'bg-red';
    if (qty < 10) return 'bg-orange';
    return 'bg-green';
  }

  getMovementDeltaLabel(delta: number): string {
    return delta > 0 ? `+${delta}` : `${delta}`;
  }

  getMovementClass(delta: number): string {
    if (delta > 0) return 'delta-positive';
    if (delta < 0) return 'delta-negative';
    return 'delta-neutral';
  }
} 
