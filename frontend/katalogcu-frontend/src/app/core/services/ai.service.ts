import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment.development';

// 🔥 GÜNCELLENDİ: Backend (ChatController) Response Yapısı
// PublicViewComponent'te kullandığımız 'res.replySuggestion' ve 'res.products' ile eşleşmeli.
export interface AiChatResponse {
  replySuggestion: string; // AI'nın metin cevabı
  products: any[];         // Bulunan parçalar listesi
  debugInfo?: string;      // Varsa debug bilgisi (hangi tool kullanıldı vs.)
}

export interface VisualFeedbackRequest {
  image: File;
  partName?: string;
  partCode?: string;
  machineBrand?: string;
  machineType?: string;
  userId?: string;
  note?: string;
}

export interface VisualFeedbackResponse {
  success: boolean;
  message?: string;
  record?: any;
}

@Injectable({
  providedIn: 'root'
})
export class AiService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/chat/ask`;
  private feedbackUrl = `${environment.apiUrl}/chat/visual-feedback`;

  /**
   * AI'ya mesaj, resim ve sohbet geçmişini gönderir.
   * @param text Kullanıcı mesajı
   * @param image Seçilen resim (opsiyonel)
   * @param history Önceki konuşmalar (bağlam için)
   * @param userId Public view kullanıcı kimliği
   */
  sendMessage(text: string, image: File | null, history: any[] = [], userId?: string): Observable<AiChatResponse> {
    const formData = new FormData();
    
    // 1. Metin (Varsa)
    if (text) formData.append('text', text);
    
    // 2. Resim (Varsa)
    if (image) formData.append('image', image);
    
    // 3. Sohbet Geçmişi (JSON String olarak gönderiyoruz)
    // Backend tarafında [FromForm] string history olarak karşılanıp deserialize edilecek.
    formData.append('history', JSON.stringify(history));

    // ✅ userId ekle
    if (userId) formData.append('userId', userId);

    return this.http.post<AiChatResponse>(this.apiUrl, formData);
  }

  saveVisualFeedback(payload: VisualFeedbackRequest): Observable<VisualFeedbackResponse> {
    const formData = new FormData();
    formData.append('image', payload.image);

    if (payload.partName) formData.append('partName', payload.partName);
    if (payload.partCode) formData.append('partCode', payload.partCode);
    if (payload.machineBrand) formData.append('machineBrand', payload.machineBrand);
    if (payload.machineType) formData.append('machineType', payload.machineType);
    if (payload.userId) formData.append('userId', payload.userId);
    if (payload.note) formData.append('note', payload.note);

    return this.http.post<VisualFeedbackResponse>(this.feedbackUrl, formData);
  }
}
