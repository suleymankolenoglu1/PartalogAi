import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { CatalogService, Catalog, CatalogPageItem, PublicStorefront } from '../core/services/catalog.service';
import { CartService } from '../core/services/cart.service';
import { AiService } from '../core/services/ai.service'; 

// 🔥 Yanıt Tipi Tanımı (HTML ile uyumlu olması için)
interface AiResponse {
  replySuggestion: string; // Eskiden 'text' idi
  products: any[];         // Eskiden 'suggestedParts' idi
  debugInfo?: string;      // Yeni eklendi

  // ✅ Compare için yan yana gruplar
  compareGroups?: CompareGroup[];
}

interface CompareGroup {
  query: string;
  results: any[];
}

// ✨ Sohbet Mesaj Tipi
interface ChatMessage {
  role: 'user' | 'assistant';
  text: string;
  timestamp: string;
  products?: any[];
  compareGroups?: CompareGroup[];
  isStreaming?: boolean;
  feedback?: 'up' | 'down';
  feedbackSubmitted?: boolean;
}

@Component({
  selector: 'app-public-view',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './public-view.html',
  styleUrls: ['./public-view.css']
})
export class PublicViewComponent implements OnInit {
  private catalogService = inject(CatalogService);
  public cartService = inject(CartService); 
  private aiService = inject(AiService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  // --- UI Durum Yönetimi ---
  searchText: string = '';
  isLoading = true;
  isCartOpen = false;

  // 🔥 AI Asistan Durumu (HTML'deki yapıyla %100 uyumlu)
  aiState = {
    isActive: false, 
    isLoading: false, 
    response: null as null | AiResponse
  };

  // ✨ Sohbet Geçmişi
  chatHistory: { role: string; text: string }[] = []; 
  messages: ChatMessage[] = [];

  selectedImage: File | null = null;
  selectedImagePreview: string | null = null;
  feedbackMachineBrand = '';
  feedbackMachineType = '';
  feedbackNote = '';
  feedbackMessage: string | null = null;
  feedbackError: string | null = null;
  chatFeedbackMessage: string | null = null;
  chatFeedbackError: string | null = null;
  chatFeedbackReason = '';
  isSendingChatFeedback = false;
  latestAssistantMessage: ChatMessage | null = null;
  savingFeedbackCode: string | null = null;
  savedFeedbackKeys = new Set<string>();

  // --- Veri Havuzu ---
  visibleCatalogs: Catalog[] = [];
  publicToken: string | null = null;
  publicLoadError: string | null = null;
  storefront: PublicStorefront = {
    businessName: 'Katalog Magazasi'
  };

  private getHistoryStorageKey(): string {
    return `chat_history_partalog_${this.publicToken ?? 'anonymous'}`;
  }

  get storefrontInitial(): string {
    const source = this.storefront.businessName || 'K';
    return source.trim().charAt(0).toUpperCase() || 'K';
  }

  ngOnInit() {
    this.publicToken = this.route.snapshot.paramMap.get('publicToken');
    if (!this.publicToken) {
      console.error('Public token bulunamadı.');
      this.publicLoadError = 'Public link eksik veya hatalı.';
      this.isLoading = false;
      return;
    }
    this.cartService.setScope(`public:${this.publicToken}`);

    // Load chat history from localStorage
    const saved = localStorage.getItem(this.getHistoryStorageKey());
    if (saved) {
      try { 
        const parsed = JSON.parse(saved);
        this.messages = parsed;
        this.chatHistory = parsed.map((m: ChatMessage) => ({ role: m.role, text: m.text }));
        this.updateLatestAssistantMessage();
        if (this.messages.length > 0) {
          this.aiState.isActive = true;
          const lastAi = [...this.messages].reverse().find(m => m.role === 'assistant');
          if (lastAi) {
            this.aiState.response = {
              replySuggestion: lastAi.text,
              products: lastAi.products || [],
              compareGroups: lastAi.compareGroups || [],
            };
          }
        }
      } catch (e) { console.warn('chat history parse error:', e); }
    }

    this.loadPublicData();
    this.loadStorefront();
  }

  loadStorefront() {
    if (!this.publicToken) return;

    this.catalogService.getPublicStorefront(this.publicToken).subscribe({
      next: (res) => {
        this.storefront = {
          businessName: res.businessName?.trim() || 'Katalog Magazasi',
          ownerName: res.ownerName?.trim() || undefined,
          email: res.email,
          phoneNumber: res.phoneNumber
        };
      },
      error: (err) => {
        console.warn('Public storefront yüklenemedi:', err);
      }
    });
  }

  loadPublicData() {
    this.isLoading = true;
    this.publicLoadError = null;

    this.catalogService.getPublicCatalogsByToken(this.publicToken!).subscribe({
        next: (catalogs) => {
            this.visibleCatalogs = catalogs; 
            
            // Kapak resmi kontrolü
            this.visibleCatalogs.forEach(c => {
                if (!c.imageUrl && c.pages && c.pages.length > 0) {
                    c.imageUrl = c.pages[0].imageUrl;
                }
            });

            this.isLoading = false;
            console.log('Public Kataloglar:', this.visibleCatalogs);
        },
        error: (err) => { 
            console.error('Public Katalog Hatası:', err); 
            this.visibleCatalogs = [];
            const backendMsg =
              typeof err?.error === 'string'
                ? err.error
                : (err?.error?.message ?? null);
            this.publicLoadError = backendMsg || 'Public link geçersiz, iptal edilmiş veya süresi dolmuş olabilir.';
            this.isLoading = false; 
        }
    });
  }

  // --- 🔥 GERÇEK AI ENTEGRASYONU ---

  // 0. Sohbet Geçmişini Kaydet
  private saveHistory() {
    localStorage.setItem(this.getHistoryStorageKey(), JSON.stringify(this.messages));
  }

  // 0b. Sohbet Geçmişini Temizle
  clearHistory() {
    this.messages = [];
    this.chatHistory = [];
    localStorage.removeItem(this.getHistoryStorageKey());
    this.aiState.isActive = false;
    this.aiState.response = null;
    this.latestAssistantMessage = null;
    this.chatFeedbackReason = '';
    this.chatFeedbackMessage = null;
    this.chatFeedbackError = null;
    this.clearImage();
    this.searchText = '';
  }

  // 1. Dosya Seçimi
  onFileSelected(event: any) {
    const file = event.target.files[0];
    if (file) {
      this.selectedImage = file;
      
      const reader = new FileReader();
      reader.onload = (e: any) => this.selectedImagePreview = e.target.result;
      reader.readAsDataURL(file);

      this.aiState.isActive = true;
    }
  }

  // 2. Görseli Temizle
  clearImage() {
    this.selectedImage = null;
    this.selectedImagePreview = null;
    this.feedbackMachineBrand = '';
    this.feedbackMachineType = '';
    this.feedbackNote = '';
    this.feedbackMessage = null;
    this.feedbackError = null;
    this.savingFeedbackCode = null;
    this.savedFeedbackKeys.clear();
    if (!this.searchText && this.messages.length === 0) {
        this.aiState.isActive = false;
        this.aiState.response = null; // Ekranı temizle
    }
  }

  // 3. Normal Arama (Input değiştiğinde)
  onSearchInput() {
    if (!this.searchText && !this.selectedImage) {
        this.aiState.isActive = false;
        // Arama temizlenirse normal kataloğa dön
    }
  }

  // 4. 🔥 AI ARAMASINI BAŞLAT
  startAiSearch() {
    if (!this.searchText && !this.selectedImage) return;

    this.aiState.isActive = true;
    this.aiState.isLoading = true;
    this.aiState.response = null;

    const userText = this.searchText || '(Resim Gönderildi)';
    const userMsg: ChatMessage = { role: 'user', text: userText, timestamp: new Date().toISOString() };
    this.messages.push(userMsg);
    this.chatHistory.push({ role: 'user', text: userText });
    this.saveHistory();

    // Streaming asistan mesajı placeholder ekle
    const streamingMsg: ChatMessage = {
      role: 'assistant',
      text: '',
      timestamp: new Date().toISOString(),
      products: [],
      isStreaming: true,
    };
    this.messages.push(streamingMsg);

    let streamingText = '';

    this.aiService.sendMessageStream(
      this.searchText,
      this.selectedImage,
      this.chatHistory,
      this.visibleCatalogs.map(c => c.id),
      this.publicToken || undefined
    ).subscribe({
      next: (event) => {
        if (event.type === 'sources') {
          const mappedProducts = (event.sources || []).map((part: any) => ({
            id: part.id,
            catalogItemId: this.buildAiCartItemId(part),
            code: part.code,
            refNo: part.refNo ?? part.ref_no,
            name: part.name,
            brand: part.brand,
            description: part.description,
            catalogId: part.catalogId,
            pageNumber: part.pageNumber || '1',
            model: part.model,
            price: part.price,
            productId: this.isEmptyGuid(part.productId ?? part.product_id) ? null : (part.productId ?? part.product_id),
            stockStatus: part.stockStatus || 'Stokta Yok',
            imageUrl: part.imageUrl,
            query: part.query,
            similarity: typeof part.similarity === 'number' ? part.similarity : null,
            visualMatch: part.visualMatch ?? false,
            visualImageUrl: part.visualImageUrl ?? null,
            visualSimilarity: part.visualSimilarity ?? null,
            fallback: part.fallback ?? false,
            fallbackReason: part.fallbackReason ?? part.fallback_reason ?? null,
          }));
          streamingMsg.products = mappedProducts;
          this.aiState.isLoading = false;
          this.aiState.response = {
            replySuggestion: '',
            products: mappedProducts,
          };
          this.messages = [...this.messages];
        } else if (event.type === 'token') {
          streamingText += event.token;
          streamingMsg.text = streamingText;
          this.messages = [...this.messages];
          if (this.aiState.response) {
            this.aiState.response = { ...this.aiState.response, replySuggestion: streamingText };
          }
        } else if (event.type === 'done') {
          streamingMsg.isStreaming = false;
          this.aiState.isLoading = false;
          this.aiState.response = {
            replySuggestion: streamingText,
            products: streamingMsg.products || [],
          };
          this.messages = [...this.messages];
          this.chatHistory.push({ role: 'assistant', text: streamingText });
          this.saveHistory();
          this.feedbackMessage = null;
          this.feedbackError = null;
          this.chatFeedbackReason = '';
          this.chatFeedbackMessage = null;
          this.chatFeedbackError = null;
          this.updateLatestAssistantMessage();
        }
      },
      error: (err) => {
        console.error('AI Stream Hatası:', err);
        this.aiState.isLoading = false;
        streamingMsg.text = '⚠️ Bağlantı hatası, lütfen tekrar deneyin.';
        streamingMsg.isStreaming = false;
        this.messages = [...this.messages];
      }
    });
  }

  private updateLatestAssistantMessage() {
    this.latestAssistantMessage =
      [...this.messages].reverse().find(m => m.role === 'assistant' && !m.isStreaming) ?? null;
  }

  private findUserQueryBefore(target: ChatMessage): string {
    const idx = this.messages.indexOf(target);
    if (idx <= 0) return this.searchText || '';
    for (let i = idx - 1; i >= 0; i--) {
      const msg = this.messages[i];
      if (msg.role === 'user' && msg.text) return msg.text;
    }
    return this.searchText || '';
  }

  sendChatFeedback(helpful: boolean) {
    if (this.isSendingChatFeedback) return;
    if (!this.latestAssistantMessage) return;
    if (this.latestAssistantMessage.feedbackSubmitted) return;

    const target = this.latestAssistantMessage;
    this.isSendingChatFeedback = true;
    this.chatFeedbackMessage = null;
    this.chatFeedbackError = null;

    const sourceCodes = (target.products || [])
      .map((p: any) => (p?.code ? String(p.code) : ''))
      .filter((x: string) => x.length > 0);

    this.aiService.saveChatFeedback({
      helpful,
      reason: this.chatFeedbackReason || undefined,
      userQuery: this.findUserQueryBefore(target),
      replySuggestion: target.text || '',
      sourceCodes,
      publicToken: this.publicToken || undefined,
      messageId: target.timestamp,
      conversationId: this.getHistoryStorageKey()
    }).subscribe({
      next: (res) => {
        this.isSendingChatFeedback = false;
        if (res?.success) {
          target.feedback = helpful ? 'up' : 'down';
          target.feedbackSubmitted = true;
          this.chatFeedbackMessage = helpful
            ? 'Teşekkürler, bu yanıtı faydalı olarak kaydettim.'
            : 'Geri bildirim alındı. Sonraki yanıtları iyileştirmek için kullanacağım.';
          this.chatFeedbackError = null;
          this.saveHistory();
        } else {
          this.chatFeedbackError = res?.message || 'Geri bildirim kaydedilemedi.';
          this.chatFeedbackMessage = null;
        }
      },
      error: (err) => {
        console.error('Chat feedback error:', err);
        this.isSendingChatFeedback = false;
        this.chatFeedbackError = 'Geri bildirim kaydedilirken hata oluştu.';
        this.chatFeedbackMessage = null;
      }
    });
  }

  openCatalog(catalogId: string) {
    this.router.navigate(['/view', catalogId], { queryParams: { token: this.publicToken } }); 
  }

  goCheckout() {
    if (!this.publicToken) return;
    this.router.navigate(['/public-view', this.publicToken, 'checkout']);
  }

  private isEmptyGuid(value: any): boolean {
    const raw = String(value ?? '').trim();
    return raw === '' || raw === '00000000-0000-0000-0000-000000000000';
  }

  private buildAiCartItemId(part: any): string {
    const catalogItemId = String(part?.catalogItemId ?? '').trim();
    if (!this.isEmptyGuid(catalogItemId)) return catalogItemId;

    const productId = String(part?.productId ?? part?.product_id ?? '').trim();
    if (!this.isEmptyGuid(productId)) return `product:${productId}`;

    const code = String(part?.code ?? '').trim().toUpperCase();
    if (code) return `code:${code}`;

    const refNo = String(part?.refNo ?? part?.ref_no ?? '').trim().toUpperCase();
    if (refNo) return `ref:${refNo}`;

    return `tmp:${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  }

  addAiPartToCart(part: any) {
    const item: CatalogPageItem = {
      catalogItemId: this.buildAiCartItemId(part),
      refNo: String(part?.refNo || part?.ref_no || ''),
      partCode: String(part?.code || ''),
      partName: String(part?.name || ''),
      description: part?.description || '',
      isStocked: true,
      productId: part?.productId || undefined,
      price: part?.price || undefined,
      localName: part?.name || undefined
    };

    this.cartService.addToCart(item);
    this.isCartOpen = true;
  }

  getPartFeedbackKey(part: any): string {
    return `${part?.code || ''}_${part?.id || ''}`;
  }

  isFeedbackSaved(part: any): boolean {
    return this.savedFeedbackKeys.has(this.getPartFeedbackKey(part));
  }

  savePartFeedback(part: any) {
    if (!this.selectedImage) {
      this.feedbackError = 'Önce parça fotoğrafı yükleyin.';
      this.feedbackMessage = null;
      return;
    }

    const feedbackKey = this.getPartFeedbackKey(part);
    this.savingFeedbackCode = feedbackKey;
    this.feedbackMessage = null;
    this.feedbackError = null;

    this.aiService.saveVisualFeedback({
      image: this.selectedImage,
      partName: part?.name,
      partCode: part?.code,
      machineBrand: this.feedbackMachineBrand || undefined,
      machineType: this.feedbackMachineType || part?.model || undefined,
      publicToken: this.publicToken || undefined,
      note: this.feedbackNote || this.searchText || undefined
    }).subscribe({
      next: (res) => {
        this.savingFeedbackCode = null;
        if (res?.success) {
          this.savedFeedbackKeys.add(feedbackKey);
          this.feedbackMessage = `${part?.code || part?.name || 'Parça'} için görsel doğrulama kaydedildi.`;
          this.feedbackError = null;
        } else {
          this.feedbackError = res?.message || 'Görsel geri bildirim kaydedilemedi.';
          this.feedbackMessage = null;
        }
      },
      error: (err) => {
        console.error('Visual feedback error:', err);
        this.savingFeedbackCode = null;
        this.feedbackError = 'Görsel geri bildirim kaydedilirken hata oluştu.';
        this.feedbackMessage = null;
      }
    });
  }

  formatFallbackReason(reason: string | null | undefined): string {
    switch (reason) {
      case 'brand_removed':
        return 'Marka filtresi kaldırıldı';
      case 'machine_group_removed':
        return 'Makine grubu filtresi kaldırıldı';
      case 'all_filters_removed':
        return 'Tüm filtreler kaldırıldı';
      default:
        return reason || 'Fallback';
    }
  }
}
