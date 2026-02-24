import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

// --- INTERFACES ---

export interface DashboardStats {
  totalCatalogs: number;
  totalParts: number;
  totalViews: number;
  pendingCount: number;
  visualEmbeddingCount?: number;  // YENİ: Visual Embedding'li parça sayısı
  recentCatalogs: DashboardCatalogItem[];
}

export interface PublicTokenStatus {
  enabled: boolean;
  version: number;
}

export interface DashboardCatalogItem {
  id: string;
  name: string;
  status: 'Published' | 'Processing' | 'Pending' | 'Draft';
  partCount: number;
  createdDate: string;
}

export interface ShowcaseMedia {
  id: string;
  type: 'image' | 'video';
  url: string;
  title?: string;
  subtitle?: string;
}

// Analiz İstekleri İçin
export interface RectSelection {
  x: number; y: number; w: number; h: number;
}

export interface AnalyzeRequest {
  pageId: string;
  tableRect?: RectSelection;
  imageRect?: RectSelection;
}

export interface MultiPageAnalyzeRequest {
  tablePageId: string;
  tableRect?: RectSelection; 
  imagePageId: string;
  imageRect?: RectSelection; 
}

export interface AnalyzeResponse {
  success?: boolean;
  message: string;
  productCount: number;
  hotspotCount: number;
  tablePageNumber?: number;
  imagePageNumber?: number;
}

// --- ANA INTERFACE'LER ---

export interface CatalogPageItem {
  catalogItemId: string;
  refNo: string;
  partCode: string;
  partName: string;       
  description?: string;   
  isStocked: boolean;     
  productId?: string;     
  price?: number;
  localName?: string;     
}

export interface Folder {
  id: string; 
  name: string;
  userId: string;
  parentId?: string | null; 
  itemCount?: number; 
}

export interface Hotspot {
  id: string;
  pageId: string;
  label: string;
  productId?: string; 
  confidence?: number;
  left: number; top: number; width: number; height: number; 
  partNumber?: string;  
  description?: string; 
}

export interface CatalogPage {
  id: string;
  catalogId: string;
  pageNumber: number;
  imageUrl: string;
  width?: number;
  height?: number;
  hotspots?: Hotspot[];
  aiDescription?: string; 
  items?: CatalogPageItem[]; 
}

export interface Catalog {
  id: string;
  name: string;
  description: string;
  imageUrl: string;
  status: string; 
  createdDate: string;
  partCount?: number;
  pages?: CatalogPage[];
  folderId?: string | null; 
}

@Injectable({
  providedIn: 'root'
})
export class CatalogService {
  private http = inject(HttpClient);
  
  // ⚠️ DİKKAT: Backend HTTPS (7xxx) portunda çalışıyorsa burayı güncellemelisin.
  // Genelde .NET 7xxx portunu kullanır. Eğer 5159 doğruysa dokunma.
  private apiUrl = 'http://localhost:5159/api'; 

  constructor() { }

  // ==========================================
  // 📂 KLASÖR YÖNETİMİ
  // ==========================================
  
  getFolders(): Observable<Folder[]> {
    return this.http.get<Folder[]>(`${this.apiUrl}/folders`);
  }

  createFolder(name: string): Observable<Folder> {
    return this.http.post<Folder>(`${this.apiUrl}/folders`, { name });
  }

  deleteFolder(id: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/folders/${id}`);
  }

  moveCatalog(catalogId: string, targetFolderId: string | null): Observable<any> {
    return this.http.put(`${this.apiUrl}/catalogs/${catalogId}/move`, { folderId: targetFolderId });
  }

  // ==========================================
  // 📚 KATALOG & DASHBOARD
  // ==========================================

  getDashboardStats(): Observable<DashboardStats> {
    return this.http.get<DashboardStats>(`${this.apiUrl}/catalogs/stats`);
  }

  // Admin/Üye Paneli için (Yetki ister)
  getCatalogs(): Observable<Catalog[]> {
    return this.http.get<Catalog[]>(`${this.apiUrl}/catalogs`);
  }

  getPublicCatalogsByToken(token: string): Observable<Catalog[]> {
    const params = new HttpParams().set('token', token);
    return this.http.get<Catalog[]>(`${this.apiUrl}/catalogs/public-by-token`, { params });
  }

  getPublicToken(catalogIds?: string[]): Observable<{ token: string }> {
    let params = new HttpParams();
    if (catalogIds && catalogIds.length > 0) {
      params = params.set('catalogIds', JSON.stringify(catalogIds));
    }
    return this.http.get<{ token: string }>(`${this.apiUrl}/catalogs/public-token`, { params });
  }

  getPublicTokenStatus(): Observable<PublicTokenStatus> {
    return this.http.get<PublicTokenStatus>(`${this.apiUrl}/catalogs/public-token/status`);
  }

  rotatePublicToken(catalogIds?: string[]): Observable<{ token: string; enabled: boolean; version: number }> {
    let params = new HttpParams();
    if (catalogIds && catalogIds.length > 0) {
      params = params.set('catalogIds', JSON.stringify(catalogIds));
    }
    return this.http.post<{ token: string; enabled: boolean; version: number }>(`${this.apiUrl}/catalogs/public-token/rotate`, null, { params });
  }

  revokePublicToken(): Observable<PublicTokenStatus> {
    return this.http.post<PublicTokenStatus>(`${this.apiUrl}/catalogs/public-token/revoke`, {});
  }

  getCatalogById(id: string, options?: { publicToken?: string }): Observable<Catalog> {
    let params = new HttpParams();
    if (options?.publicToken) params = params.set('token', options.publicToken);
    return this.http.get<Catalog>(`${this.apiUrl}/catalogs/${id}`, { params });
  }

  createCatalog(catalogData: any): Observable<Catalog> {
    return this.http.post<Catalog>(`${this.apiUrl}/catalogs`, catalogData);
  }

  deleteCatalog(id: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/catalogs/${id}`);
  }

  publishCatalog(id: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/catalogs/${id}/publish`, {});
  }

  // ==========================================
  // 🧠 AI & ANALİZ & SAYFA İŞLEMLERİ
  // ==========================================

  getPageItems(catalogId: string, pageNumber: string, options?: { publicToken?: string }): Observable<CatalogPageItem[]> {
    let params = new HttpParams();
    if (options?.publicToken) params = params.set('token', options.publicToken);
    return this.http.get<CatalogPageItem[]>(`${this.apiUrl}/catalogs/${catalogId}/pages/${pageNumber}/items`, { params });
  }

  startAiProcess(catalogId: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/catalogs/${catalogId}/start-ai-process`, {});
  }

  clearPageData(catalogId: string, pageId: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/catalogs/${catalogId}/pages/${pageId}/clear`);
  }

  analyzePage(catalogId: string, data: AnalyzeRequest): Observable<AnalyzeResponse> {
    return this.http.post<AnalyzeResponse>(`${this.apiUrl}/catalogs/${catalogId}/analyze`, data);
  }

  analyzeMultiPage(catalogId: string, data: MultiPageAnalyzeRequest): Observable<AnalyzeResponse> {
    return this.http.post<AnalyzeResponse>(`${this.apiUrl}/catalogs/${catalogId}/analyze-multi`, data);
  }

  // ==========================================
  // 🖼️ MEDYA & DOSYA
  // ==========================================

  uploadImage(file: File): Observable<any> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<{ url: string }>(`${this.apiUrl}/files/upload`, formData);
  }

  // ==========================================
  // 🎯 HOTSPOT İŞLEMLERİ
  // ==========================================

  createHotspot(hotspotData: any): Observable<Hotspot> {
    return this.http.post<Hotspot>(`${this.apiUrl}/hotspots`, hotspotData);
  }

  deleteHotspot(id: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/hotspots/${id}`);
  }
}
