import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

// --- INTERFACES ---

export interface DashboardStats {
  totalCatalogs: number;
  totalParts: number;
  totalViews: number;
  viewsLast7Days?: number;
  uniqueViewersLast30Days?: number;
  storefrontVisitsTotal?: number;
  storefrontVisitsToday?: number;
  storefrontVisitsLast7Days?: number;
  storefrontUniqueVisitorsLast30Days?: number;
  embedEventsTotal?: number;
  embedEventsLast7Days?: number;
  embedPartViewedCount?: number;
  embedCartAddCount?: number;
  embedCheckoutStartCount?: number;
  aiUsedThisMonth?: number;
  aiMonthlyLimit?: number | null;
  aiRemainingThisMonth?: number;
  aiEnabled?: boolean;
  aiUnlimited?: boolean;
  pendingCount: number;
  visualEmbeddingCount?: number;  // YENİ: Visual Embedding'li parça sayısı
  recentCatalogs: DashboardCatalogItem[];
  topViewedCatalogs?: DashboardTopViewedCatalogItem[];
}

export interface DashboardTopViewedCatalogItem {
  id: string;
  name: string;
  viewCount: number;
  lastViewedAtUtc: string;
}

export interface PublicTokenStatus {
  enabled: boolean;
  version: number;
}

export interface PublicStorefront {
  businessName: string;
  ownerName?: string;
  email?: string;
  phoneNumber?: string;
  subscriptionPlan?: number;
  aiChatEnabled?: boolean;
  ecommerceEnabled?: boolean;
}

export interface EmbedSettings {
  userId: string;
  allowedOrigins: string[];
  theme: string;
  mode: string;
}

export interface EmbedVerifyOriginResponse {
  allowed: boolean;
  reason: string;
  origin?: string;
  ownerUserId?: string;
  theme?: string;
  mode?: string;
}

export interface EmbedDomainInstruction {
  type: 'dns_txt' | 'file';
  recordName?: string;
  recordType?: string;
  recordValue?: string;
  filePath?: string;
  fileUrl?: string;
  fileContent?: string;
}

export interface EmbedDomainVerification {
  id: string;
  userId: string;
  origin: string;
  domain: string;
  method: 'dns_txt' | 'file';
  status: 'pending' | 'verified' | 'failed';
  challengeToken: string;
  verifiedAt?: string | null;
  lastError?: string | null;
  instructions: EmbedDomainInstruction;
}

export interface PublicFolderSummary {
  id: string;
  name: string;
  itemCount: number;
}

export interface CatalogAiJobSummary {
  total: number;
  pending: number;
  processing: number;
  completed: number;
  failed: number;
}

export interface CatalogAiJobItem {
  jobId: string;
  catalogId: string;
  catalogName: string;
  status: string;
  attemptCount: number;
  maxAttempts: number;
  nextAttemptAt: string;
  lastAttemptAt?: string | null;
  lockedUntil?: string | null;
  lastError?: string | null;
  createdDate: string;
  updatedDate?: string | null;
}

export interface CatalogAiJobsResponse {
  summary: CatalogAiJobSummary;
  jobs: CatalogAiJobItem[];
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

export interface CatalogItemUpsertRequest {
  catalogId: string;
  pageNumber: number;
  refNo: string;
  partCode: string;
  partName: string;
  description?: string;
}

export interface CatalogItemUpdateRequest {
  refNo: string;
  partCode: string;
  partName: string;
  description?: string;
}

export interface HotspotUpdateRequest {
  left: number;
  top: number;
  width: number;
  height: number;
  label?: string;
  productId?: string | null;
}

export interface CatalogPage {
  id: string;
  catalogId: string;
  pageNumber: number;
  imageUrl: string;
  isTechnicalDrawing?: boolean | null;
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
  private apiUrl = environment.apiUrl;

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

  getCatalogAiJobs(take = 50): Observable<CatalogAiJobsResponse> {
    const params = new HttpParams().set('take', take.toString());
    return this.http.get<CatalogAiJobsResponse>(`${this.apiUrl}/catalogs/ai-jobs`, { params });
  }

  // Admin/Üye Paneli için (Yetki ister)
  getCatalogs(): Observable<Catalog[]> {
    return this.http.get<Catalog[]>(`${this.apiUrl}/catalogs`);
  }

  getPublicCatalogsByToken(token: string): Observable<Catalog[]> {
    const params = new HttpParams().set('token', token);
    return this.http.get<Catalog[]>(`${this.apiUrl}/catalogs/public-by-token`, { params });
  }

  getPublicStorefront(token: string): Observable<PublicStorefront> {
    const params = new HttpParams().set('token', token);
    return this.http.get<PublicStorefront>(`${this.apiUrl}/catalogs/public-storefront`, { params });
  }

  getPublicFoldersByToken(token: string): Observable<PublicFolderSummary[]> {
    const params = new HttpParams().set('token', token);
    return this.http.get<PublicFolderSummary[]>(`${this.apiUrl}/catalogs/public-folders-by-token`, { params });
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

  getEmbedSettings(): Observable<EmbedSettings> {
    return this.http.get<EmbedSettings>(`${this.apiUrl}/embed/settings`);
  }

  updateEmbedSettings(payload: { allowedOrigins: string[]; theme?: string; mode?: string }): Observable<EmbedSettings> {
    return this.http.put<EmbedSettings>(`${this.apiUrl}/embed/settings`, payload);
  }

  verifyEmbedOrigin(publicToken: string, origin: string): Observable<EmbedVerifyOriginResponse> {
    return this.http.post<EmbedVerifyOriginResponse>(`${this.apiUrl}/embed/verify-origin`, {
      publicToken,
      origin
    });
  }

  getEmbedDomainVerifications(): Observable<EmbedDomainVerification[]> {
    return this.http.get<EmbedDomainVerification[]>(`${this.apiUrl}/embed/domains`);
  }

  createEmbedDomainChallenge(payload: { origin: string; method: 'dns_txt' | 'file' }): Observable<EmbedDomainVerification> {
    return this.http.post<EmbedDomainVerification>(`${this.apiUrl}/embed/domains/challenge`, payload);
  }

  verifyEmbedDomainNow(id: string): Observable<EmbedDomainVerification> {
    return this.http.post<EmbedDomainVerification>(`${this.apiUrl}/embed/domains/${id}/verify-now`, {});
  }

  activateEmbedDomain(id: string): Observable<EmbedDomainVerification> {
    return this.http.post<EmbedDomainVerification>(`${this.apiUrl}/embed/domains/${id}/activate`, {});
  }

  deleteEmbedDomainVerification(id: string): Observable<{ success: boolean }> {
    return this.http.delete<{ success: boolean }>(`${this.apiUrl}/embed/domains/${id}`);
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

  getPageItems(catalogId: string, pageNumber: string, options?: { publicToken?: string; strictPage?: boolean }): Observable<CatalogPageItem[]> {
    let params = new HttpParams();
    if (options?.publicToken) params = params.set('token', options.publicToken);
    if (options?.strictPage) params = params.set('strict', 'true');
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

  updateHotspot(id: string, hotspotData: HotspotUpdateRequest): Observable<Hotspot> {
    return this.http.put<Hotspot>(`${this.apiUrl}/hotspots/${id}`, hotspotData);
  }

  deleteHotspot(id: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/hotspots/${id}`);
  }

  createCatalogItem(data: CatalogItemUpsertRequest): Observable<CatalogPageItem> {
    return this.http.post<CatalogPageItem>(`${this.apiUrl}/catalog-items`, data);
  }

  updateCatalogItem(id: string, data: CatalogItemUpdateRequest): Observable<CatalogPageItem> {
    return this.http.put<CatalogPageItem>(`${this.apiUrl}/catalog-items/${id}`, data);
  }

  deleteCatalogItem(id: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/catalog-items/${id}`);
  }
}
