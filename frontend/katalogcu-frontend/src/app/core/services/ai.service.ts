import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
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
  publicToken?: string;
  note?: string;
}

export interface VisualFeedbackResponse {
  success: boolean;
  message?: string;
  record?: any;
}

export interface ChatFeedbackRequest {
  helpful: boolean;
  reason?: string;
  userQuery?: string;
  replySuggestion: string;
  sourceCodes?: string[];
  publicToken?: string;
  messageId?: string;
  conversationId?: string;
}

export interface ChatFeedbackResponse {
  success: boolean;
  message?: string;
  id?: string;
}

@Injectable({
  providedIn: 'root'
})
export class AiService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/chat/ask`;
  private feedbackUrl = `${environment.apiUrl}/chat/visual-feedback`;
  private chatFeedbackUrl = `${environment.apiUrl}/chat/feedback`;

  /**
   * AI'ya mesaj, resim ve sohbet geçmişini gönderir.
   * @param text Kullanıcı mesajı
   * @param image Seçilen resim (opsiyonel)
   * @param history Önceki konuşmalar (bağlam için)
   */
  sendMessage(text: string, image: File | null, history: any[] = [], catalogIds?: string[], publicToken?: string): Observable<AiChatResponse> {
    const formData = new FormData();
    
    // 1. Metin (Varsa)
    if (text) formData.append('text', text);
    
    // 2. Resim (Varsa)
    if (image) formData.append('image', image);
    
    // 3. Sohbet Geçmişi (JSON String olarak gönderiyoruz)
    // Backend tarafında [FromForm] string history olarak karşılanıp deserialize edilecek.
    formData.append('history', JSON.stringify(history));

    if (publicToken) formData.append('publicToken', publicToken);

    // ✅ catalog_ids ekle (arama kapsamını bu kataloglarla sınırla)
    formData.append('catalog_ids', JSON.stringify(catalogIds ?? []));

    return this.http.post<AiChatResponse>(this.apiUrl, formData);
  }

  saveVisualFeedback(payload: VisualFeedbackRequest): Observable<VisualFeedbackResponse> {
    const formData = new FormData();
    formData.append('image', payload.image);

    if (payload.partName) formData.append('partName', payload.partName);
    if (payload.partCode) formData.append('partCode', payload.partCode);
    if (payload.machineBrand) formData.append('machineBrand', payload.machineBrand);
    if (payload.machineType) formData.append('machineType', payload.machineType);
    if (payload.publicToken) formData.append('publicToken', payload.publicToken);
    if (payload.note) formData.append('note', payload.note);

    return this.http.post<VisualFeedbackResponse>(this.feedbackUrl, formData);
  }

  saveChatFeedback(payload: ChatFeedbackRequest): Observable<ChatFeedbackResponse> {
    return this.http.post<ChatFeedbackResponse>(this.chatFeedbackUrl, payload);
  }

  sendMessageStream(
    text: string,
    image: File | null,
    history: any[],
    catalogIds?: string[],
    publicToken?: string
  ): Observable<{type: string; token?: string; sources?: any[]; debug_intent?: any}> {
    return new Observable(observer => {
      const formData = new FormData();
      if (text) formData.append('text', text);
      if (image) formData.append('image', image);
      formData.append('history', JSON.stringify(history));
      if (publicToken) formData.append('publicToken', publicToken);
      formData.append('catalog_ids', JSON.stringify(catalogIds ?? []));

      const token = localStorage.getItem('auth_token') ?? '';
      const headers: Record<string, string> = {};
      if (token) headers['Authorization'] = `Bearer ${token}`;
      fetch(`${environment.apiUrl}/chat/ask-stream`, {
        method: 'POST',
        body: formData,
        headers
      }).then(response => {
        if (!response.body) { observer.error(new Error('SSE bağlantısı kurulamadı.')); return; }
        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        let buffer = '';

        const read = () => {
          reader.read().then(({ done, value }) => {
            if (done) {
              if (buffer.trim().length > 0) {
                const line = buffer.trim();
                if (line.startsWith('data:')) {
                  try {
                    const data = JSON.parse(line.slice(5).trim());
                    observer.next(data);
                  } catch (e) {
                    console.debug('SSE satırı parse edilemedi:', line);
                  }
                }
              }
              observer.complete();
              return;
            }
            buffer += decoder.decode(value, { stream: true });
            const lines = buffer.split('\n');
            buffer = lines.pop() ?? '';
            for (const rawLine of lines) {
              const line = rawLine.trim();
              if (!line.startsWith('data:')) continue;
              try {
                const data = JSON.parse(line.slice(5).trim());
                observer.next(data);
              } catch (e) {
                console.debug('SSE satırı parse edilemedi:', line);
              }
            }
            read();
          }).catch(err => observer.error(err));
        };
        read();
      }).catch(err => observer.error(err));
    });
  }
}
