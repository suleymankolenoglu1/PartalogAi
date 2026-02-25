import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Observable } from 'rxjs';

// 🔥 GÜNCELLENDİ: Backend'den gelen yeni alanlar eklendi
export interface Product {
  id?: string;
  code: string;          // Parça Kodu
  name: string;          // Parça Adı
  oemNo?: string;        // ✨ YENİ: OEM Numarası
  category?: string;     // Kategori (Motor, Fren vb.)
  price: number;
  stockQuantity: number;
  imageUrl?: string;     // ✨ YENİ: Parça Görseli
  description?: string;
  
  // İlişkisel Veriler
  catalogName?: string;  // ✨ YENİ: Tabloda "Hangi Katalog" sütunu için
  catalogId?: string;
  pageNumber?: string;
  refNo?: number;
}

export interface StockImportOptions {
  catalogId?: string;
  mode?: 'update_only' | 'upsert';
}

export interface StockMovement {
  id: string;
  productId: string;
  productCode: string;
  productName: string;
  previousQuantity: number;
  deltaQuantity: number;
  newQuantity: number;
  movementType: string;
  reason: string;
  source?: string;
  actorName?: string;
  referenceId?: string;
  createdDate: string;
}

@Injectable({
  providedIn: 'root'
})
export class ProductService {
  private http = inject(HttpClient);
  private apiUrl = environment.apiUrl;

  // 1. Tüm Parçaları Getir (Admin Envanter Sayfası İçin)
  getProducts(): Observable<Product[]> {
    return this.http.get<Product[]>(`${this.apiUrl}/products`);
  }

  // 2. Belirli Bir Kataloğa Ait Parçaları Getir (Vitrin / PublicView İçin)
  getProductsByCatalog(catalogId: string, options?: { publicToken?: string }): Observable<Product[]> {
    let params = new HttpParams();
    if (options?.publicToken) params = params.set('token', options.publicToken);
    return this.http.get<Product[]>(`${this.apiUrl}/products/catalog/${catalogId}`, { params });
  }

  // 3. Yeni Parça Ekle
  // Partial<Product> kullanarak ID gibi zorunlu olmayan alanları es geçebiliyoruz
  createProduct(product: Partial<Product>): Observable<Product> {
    return this.http.post<Product>(`${this.apiUrl}/products`, product);
  }

  // 4. Parça Sil
  deleteProduct(id: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/products/${id}`);
  }

  // 5. Excel Import
  importExcel(file: File, catalogId: string): Observable<any> {
    const formData = new FormData();
    formData.append('file', file);
    
    // Eğer genel stok yüklemesi yapılıyorsa catalogId boş olabilir
    if (catalogId) {
      formData.append('catalogId', catalogId);
    }

    return this.http.post(`${this.apiUrl}/products/import`, formData);
  }

  importStock(file: File, options?: StockImportOptions): Observable<any> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('mode', options?.mode ?? 'update_only');

    if (options?.catalogId) {
      formData.append('catalogId', options.catalogId);
    }

    return this.http.post(`${this.apiUrl}/products/import-stock`, formData);
  }

  adjustStock(productId: string, payload: { deltaQuantity: number; reason?: string }): Observable<any> {
    return this.http.post(`${this.apiUrl}/products/${productId}/adjust-stock`, payload);
  }

  getStockMovements(options?: { productId?: string; limit?: number }): Observable<StockMovement[]> {
    let params = new HttpParams();
    if (options?.productId) params = params.set('productId', options.productId);
    if (options?.limit) params = params.set('limit', options.limit.toString());
    return this.http.get<StockMovement[]>(`${this.apiUrl}/products/stock-movements`, { params });
  }
}
