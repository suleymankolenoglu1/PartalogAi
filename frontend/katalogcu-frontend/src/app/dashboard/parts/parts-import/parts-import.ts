
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
  private static readonly MAX_SPREADSHEET_BYTES = 25 * 1024 * 1024;

  private productService = inject(ProductService);
  private catalogService = inject(CatalogService);
  private router = inject(Router);

  catalogs: Catalog[] = [];
  selectedCatalogId = '';
  selectedMode: 'catalog' | 'stock' = 'stock';
  stockImportMode: 'update_only' | 'upsert' = 'update_only';

  selectedFile: File | null = null;
  isUploading = false;
  uploadError: string | null = null;
  resultMessage: string | null = null;
  resultSummary: { totalRows: number; updated: number; created: number; skipped: number; mode: string } | null = null;
  skippedRows: Array<{ rowNumber: number; code: string; reason: string }> = [];
  skippedRowsTruncated = false;

  ngOnInit() {
    // Hangi kataloğa yükleneceğini seçmek için katalogları getir
    this.catalogService.getCatalogs().subscribe(data => {
      this.catalogs = data;
    });
  }

  setMode(mode: 'catalog' | 'stock') {
    this.selectedMode = mode;
    this.selectedFile = null;
    this.uploadError = null;
    this.resultMessage = null;
    this.resultSummary = null;
    this.skippedRows = [];
    this.skippedRowsTruncated = false;
  }

  onFileSelected(event: any) {
    this.uploadError = null;
    this.resultMessage = null;
    const file = event.target.files?.[0] ?? null;
    if (!file) {
      this.selectedFile = null;
      return;
    }

    const extension = (file.name.split('.').pop() || '').toLowerCase();
    const allowed = this.selectedMode === 'catalog'
      ? ['xlsx']
      : ['xlsx', 'csv'];

    if (!allowed.includes(extension)) {
      this.selectedFile = null;
      this.uploadError = `Geçersiz dosya tipi. İzin verilen: ${allowed.map(x => '.' + x).join(', ')}.`;
      return;
    }

    if (file.size > PartsImportComponent.MAX_SPREADSHEET_BYTES) {
      this.selectedFile = null;
      this.uploadError = 'Dosya boyutu 25 MB sınırını aşıyor.';
      return;
    }

    this.selectedFile = file;
    this.resultSummary = null;
    this.skippedRows = [];
    this.skippedRowsTruncated = false;
  }

  onUpload() {
    if (!this.selectedFile) {
      this.uploadError = 'Lütfen bir dosya seçin.';
      return;
    }
    this.uploadError = null;
    this.resultMessage = null;

    if (this.selectedMode === 'catalog') {
      if (!this.selectedCatalogId) {
        this.uploadError = 'Lütfen bir katalog seçin.';
        return;
      }

      this.isUploading = true;
      this.productService.importExcel(this.selectedFile, this.selectedCatalogId).subscribe({
        next: (res) => {
          this.resultMessage = res?.message ?? 'Ürün import tamamlandı.';
          this.router.navigate(['/dashboard/parts']);
        },
        error: (err) => {
          console.error(err);
          this.uploadError = this.extractErrorMessage(err, 'Yükleme başarısız! Excel formatını kontrol edin.');
          this.isUploading = false;
        }
      });
      return;
    }

    if (this.stockImportMode === 'upsert' && !this.selectedCatalogId) {
      this.uploadError = 'Upsert modunda yeni ürünler için katalog seçmek zorunlu.';
      return;
    }

    this.isUploading = true;
    this.productService.importStock(this.selectedFile, {
      mode: this.stockImportMode,
      catalogId: this.stockImportMode === 'upsert' ? this.selectedCatalogId : undefined
    }).subscribe({
      next: (res) => {
        this.resultMessage = res?.message ?? null;
        this.resultSummary = res?.summary ?? null;
        this.skippedRows = res?.skippedRows ?? [];
        this.skippedRowsTruncated = !!res?.skippedRowsTruncated;
        this.isUploading = false;
      },
      error: (err) => {
        console.error(err);
        this.uploadError = this.extractErrorMessage(err, 'Stok aktarımı başarısız.');
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

  private extractErrorMessage(err: any, fallback: string): string {
    const payload = err?.error;
    if (typeof payload === 'string' && payload.trim().length > 0) return payload;
    if (payload?.message && typeof payload.message === 'string') return payload.message;
    if (err?.message && typeof err.message === 'string') return err.message;
    return fallback;
  }
}
