
import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ProductService } from '../../../core/services/product.service';
import { CatalogService, Catalog } from '../../../core/services/catalog.service';

@Component({
  selector: 'app-parts-import',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  templateUrl: './parts-import.html',
  styleUrl: './parts-import.css'
})
export class PartsImportComponent implements OnInit {
  private productService = inject(ProductService);
  private catalogService = inject(CatalogService);
  private router = inject(Router);

  catalogs: Catalog[] = [];
  selectedCatalogId = '';
  selectedMode: 'catalog' | 'stock' = 'stock';
  stockImportMode: 'update_only' | 'upsert' = 'update_only';

  selectedFile: File | null = null;
  isUploading = false;
  resultSummary: { totalRows: number; updated: number; created: number; skipped: number; mode: string } | null = null;
  skippedRows: Array<{ rowNumber: number; code: string; reason: string }> = [];

  ngOnInit() {
    // Hangi kataloğa yükleneceğini seçmek için katalogları getir
    this.catalogService.getCatalogs().subscribe(data => {
      this.catalogs = data;
    });
  }

  setMode(mode: 'catalog' | 'stock') {
    this.selectedMode = mode;
    this.selectedFile = null;
    this.resultSummary = null;
    this.skippedRows = [];
  }

  onFileSelected(event: any) {
    this.selectedFile = event.target.files[0];
    this.resultSummary = null;
    this.skippedRows = [];
  }

  onUpload() {
    if (!this.selectedFile) {
      alert('Lütfen bir dosya seçin.');
      return;
    }

    if (this.selectedMode === 'catalog') {
      if (!this.selectedCatalogId) {
        alert('Lütfen bir katalog seçin.');
        return;
      }

      this.isUploading = true;
      this.productService.importExcel(this.selectedFile, this.selectedCatalogId).subscribe({
        next: (res) => {
          alert(res.message);
          this.router.navigate(['/dashboard/parts']);
        },
        error: (err) => {
          console.error(err);
          alert('Yükleme başarısız! Excel formatını kontrol edin.');
          this.isUploading = false;
        }
      });
      return;
    }

    if (this.stockImportMode === 'upsert' && !this.selectedCatalogId) {
      alert('Upsert modunda yeni ürünler için katalog seçmek zorunlu.');
      return;
    }

    this.isUploading = true;
    this.productService.importStock(this.selectedFile, {
      mode: this.stockImportMode,
      catalogId: this.stockImportMode === 'upsert' ? this.selectedCatalogId : undefined
    }).subscribe({
      next: (res) => {
        this.resultSummary = res?.summary ?? null;
        this.skippedRows = res?.skippedRows ?? [];
        this.isUploading = false;
      },
      error: (err) => {
        console.error(err);
        alert(err?.error ?? 'Stok aktarımı başarısız.');
        this.isUploading = false;
      }
    });
  }

  downloadStockTemplate() {
    const content = [
      'code,stockQuantity,price,name,category,description',
      '160000,25,0,,,',
      '120016,8,15.5,,,'
    ].join('\\n');

    const blob = new Blob([content], { type: 'text/csv;charset=utf-8;' });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'stock-import-template.csv';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
  }
}
