import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { CatalogService, Catalog } from '../core/services/catalog.service';
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
  isSubmitting = false;

  // 🔥 AI Asistan Durumu (HTML'deki yapıyla %100 uyumlu)
  aiState = {
    isActive: false, 
    isLoading: false, 
    response: null as null | AiResponse
  };

  // ✨ Sohbet Geçmişi
  chatHistory: any[] = []; 

  selectedImage: File | null = null;
  selectedImagePreview: string | null = null;
  feedbackMachineBrand = '';
  feedbackMachineType = '';
  feedbackNote = '';
  feedbackMessage: string | null = null;
  feedbackError: string | null = null;
  savingFeedbackCode: string | null = null;
  savedFeedbackKeys = new Set<string>();

  // --- Müşteri Form Modeli ---
  customerForm = { name: '', phone: '', email: '', note: '' };

  // --- Veri Havuzu ---
  visibleCatalogs: Catalog[] = [];
  userId: string | null = null;

  ngOnInit() {
    this.userId = this.route.snapshot.paramMap.get('userId');
    if (!this.userId) {
      console.error('UserId bulunamadı.');
      this.isLoading = false;
      return;
    }

    this.loadPublicData(this.userId);
  }

  loadPublicData(userId: string) {
    this.isLoading = true;

    this.catalogService.getPublicCatalogsByUser(userId).subscribe({
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
            this.isLoading = false; 
        }
    });
  }

  // --- 🔥 GERÇEK AI ENTEGRASYONU ---

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
    if (!this.searchText) {
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

    this.chatHistory.push({ role: 'user', text: this.searchText || '(Resim Gönderildi)' });

    this.aiService.sendMessage(
      this.searchText, 
      this.selectedImage, 
      this.chatHistory, 
      this.userId || undefined
    ).subscribe({
      next: (res: any) => { 
        this.aiState.isLoading = false;
        
        this.aiState.response = {
          replySuggestion: res.replySuggestion || "Sonuçlar aşağıdadır:", 
          products: (res.products || []).map((part: any) => ({
            id: part.id,
            code: part.code,
            name: part.name,
            description: part.description, 
            catalogId: part.catalogId, 
            pageNumber: part.pageNumber || '1',
            model: part.model,
            price: part.price,
            stockStatus: part.stockStatus || 'Stokta Yok', 
            imageUrl: part.imageUrl,
            visualMatch: part.visualMatch ?? false,
            visualImageUrl: part.visualImageUrl ?? null,
            visualSimilarity: part.visualSimilarity ?? null,
          })),
          compareGroups: (res.compareGroups || []).map((group: any) => ({
            query: group.query,
            results: (group.results || []).map((part: any) => ({
              id: part.id,
              code: part.code,
              name: part.name,
              description: part.description, 
              catalogId: part.catalogId, 
              pageNumber: part.pageNumber || '1',
              model: part.model,
              price: part.price,
              stockStatus: part.stockStatus || 'Stokta Yok', 
              imageUrl: part.imageUrl,
              visualMatch: part.visualMatch ?? false,
              visualImageUrl: part.visualImageUrl ?? null,
              visualSimilarity: part.visualSimilarity ?? null,
            }))
          })),
          debugInfo: res.debugInfo
        };

        this.chatHistory.push({ role: 'assistant', text: res.replySuggestion });
        this.feedbackMessage = null;
        this.feedbackError = null;
      },
      error: (err) => {
        this.aiState.isLoading = false;
        console.error('AI Bağlantı Hatası:', err);
        
        this.aiState.response = {
          replySuggestion: "⚠️ Üzgünüm, şu an teknik bir sorun yaşıyorum. Lütfen daha sonra tekrar deneyin.",
          products: []
        };
      }
    });
  }

  // --- KLASİK İŞLEMLER ---

  submitOrder() {
    if (!this.customerForm.name || !this.customerForm.phone) {
      alert('Lütfen Ad Soyad ve Telefon alanlarını doldurunuz.');
      return;
    }
    
    this.isSubmitting = true;
    
    this.cartService.submitOrder(this.customerForm).subscribe({
      next: (res: any) => {
        alert(`Siparişiniz başarıyla alındı! \nSipariş No: ${res.orderNumber}`);
        this.cartService.clearCart();
        this.isCartOpen = false;
        this.isSubmitting = false;
        this.customerForm = { name: '', phone: '', email: '', note: '' };
      },
      error: (err) => {
        console.error('Sipariş hatası:', err);
        alert('Sipariş oluşturulurken bir hata oluştu.');
        this.isSubmitting = false;
      }
    });
  }

  openCatalog(catalogId: string) {
    this.router.navigate(['/view', catalogId]); 
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
      userId: this.userId || undefined,
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
}
