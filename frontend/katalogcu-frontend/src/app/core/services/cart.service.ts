import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { BehaviorSubject, finalize } from 'rxjs';
import { CatalogPageItem } from './catalog.service'; // 🔥 Doğru interface'i buradan alıyoruz
import { environment } from '../../../environments/environment';

export interface CartItem {
  product: CatalogPageItem; 
  quantity: number;
}

@Injectable({
  providedIn: 'root'
})
export class CartService {
  private http = inject(HttpClient);
  private apiUrl = environment.apiUrl;
  private cartKeyBase = 'partalog_cart';
  private cartScope = 'global';
  private pendingOrderKey: string | null = null;

  private get cartKey(): string {
    return `${this.cartKeyBase}_${this.cartScope}`;
  }

  // --- STATE MANAGEMENT (Reactive) ---
  
  // 1. Sepet Listesi
  private _cart = new BehaviorSubject<CartItem[]>([]);
  public cart$ = this._cart.asObservable();

  // 2. Toplam Adet (Async Pipe İçin)
  private _totalCount = new BehaviorSubject<number>(0);
  public totalCount$ = this._totalCount.asObservable();

  // 3. Toplam Tutar (Async Pipe İçin)
  private _totalPrice = new BehaviorSubject<number>(0);
  public totalPrice$ = this._totalPrice.asObservable();

  constructor() {
    this.loadCart();
  }

  setScope(scope: string | null | undefined) {
    const normalized = (scope || 'global').trim().toLowerCase();
    if (!normalized || this.cartScope === normalized) return;
    this.cartScope = normalized;
    this.loadCart();
  }

  private buildCartItemId(product: CatalogPageItem): string {
    const rawCatalogItemId = String(product?.catalogItemId ?? '').trim();
    if (rawCatalogItemId && rawCatalogItemId !== '00000000-0000-0000-0000-000000000000') {
      return rawCatalogItemId;
    }

    const productId = String(product?.productId ?? '').trim();
    if (productId && productId !== '00000000-0000-0000-0000-000000000000') {
      return `product:${productId}`;
    }

    const partCode = String(product?.partCode ?? '').trim().toUpperCase();
    if (partCode) return `code:${partCode}`;

    const refNo = String(product?.refNo ?? '').trim().toUpperCase();
    if (refNo) return `ref:${refNo}`;

    return `tmp:${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  }

  private normalizeProduct(product: CatalogPageItem): CatalogPageItem {
    return {
      ...product,
      catalogItemId: this.buildCartItemId(product),
      partCode: String(product?.partCode ?? ''),
      partName: String(product?.partName ?? ''),
      refNo: String(product?.refNo ?? '')
    };
  }

  // --- SEPET İŞLEMLERİ ---

  addToCart(product: CatalogPageItem) {
    const normalized = this.normalizeProduct(product);
    const currentCart = this._cart.value;
    
    // Ürün zaten var mı? (ID kontrolü)
    const existingItem = currentCart.find(i => i.product.catalogItemId === normalized.catalogItemId);

    if (existingItem) {
      existingItem.quantity += 1;
    } else {
      currentCart.push({ product: normalized, quantity: 1 });
    }

    this.updateState(currentCart);
  }

  removeFromCart(catalogItemId: string) {
    const currentCart = this._cart.value.filter(i => i.product.catalogItemId !== catalogItemId);
    this.updateState(currentCart);
  }

  updateQuantity(catalogItemId: string, quantity: number) {
    const currentCart = this._cart.value;
    const item = currentCart.find(i => i.product.catalogItemId === catalogItemId);

    if (item) {
      if (quantity <= 0) {
        this.removeFromCart(catalogItemId);
        return;
      }
      item.quantity = quantity;
      this.updateState(currentCart);
    }
  }

  clearCart() {
    this.updateState([]);
  }

  // --- SİPARİŞ GÖNDERME ---

  submitOrder(
    customerInfo: { name: string; phone: string; email: string; note?: string },
    options?: {
      publicToken?: string;
      publicSessionToken?: string;
      deliveryAddress?: string;
      deliveryCity?: string;
      deliveryDistrict?: string;
      deliveryNote?: string;
      paymentMethod?: string;
    }
  ) {
    const idempotencyKey = this.pendingOrderKey ?? this.createIdempotencyKey();
    this.pendingOrderKey = idempotencyKey;

    // Backend 'CreateOrderDto' yapısına uygun veri hazırlıyoruz
    const orderData = {
      customerName: customerInfo.name,
      customerPhone: customerInfo.phone,
      customerEmail: customerInfo.email,
      note: customerInfo.note,
      deliveryAddress: options?.deliveryAddress,
      deliveryCity: options?.deliveryCity,
      deliveryDistrict: options?.deliveryDistrict,
      deliveryNote: options?.deliveryNote,
      paymentMethod: options?.paymentMethod,
      publicToken: options?.publicToken,
      idempotencyKey,
      publicSessionToken: options?.publicSessionToken,
      items: this._cart.value.map(i => ({
        // Eğer stokta varsa ProductId, yoksa CatalogItemId veya null (Backend mantığına göre)
        productId: i.product.productId, 
        partCode: i.product.partCode,   
        partName: i.product.partName,
        quantity: i.quantity,
        price: i.product.price || 0
      }))
    };

    const headers = new HttpHeaders({
      'Idempotency-Key': idempotencyKey
    });

    return this.http.post(`${this.apiUrl}/orders`, orderData, { headers }).pipe(
      finalize(() => {
        this.pendingOrderKey = null;
      })
    );
  }

  // --- YARDIMCI METODLAR ---

  // Tüm observable'ları ve LocalStorage'ı günceller
  private updateState(cart: CartItem[]) {
    this._cart.next(cart);
    this.calculateTotals(cart);
    this.saveToStorage(cart);
  }

  private calculateTotals(cart: CartItem[]) {
    const count = cart.reduce((acc, item) => acc + item.quantity, 0);
    const price = cart.reduce((acc, item) => acc + (item.quantity * (item.product.price || 0)), 0);

    this._totalCount.next(count);
    this._totalPrice.next(price);
  }

  // LocalStorage İşlemleri
  private saveToStorage(cart: CartItem[]) {
    localStorage.setItem(this.cartKey, JSON.stringify(cart));
  }

  private loadCart() {
    const saved = localStorage.getItem(this.cartKey);
    if (saved) {
      try {
        const parsed = JSON.parse(saved) as CartItem[];
        const cart = (parsed || []).map(item => ({
          ...item,
          product: this.normalizeProduct(item.product)
        }));
        this._cart.next(cart);
        this.calculateTotals(cart);
      } catch (e) {
        console.error('Sepet verisi bozuk, sıfırlanıyor.', e);
        this.clearCart();
      }
      return;
    }

    this._cart.next([]);
    this.calculateTotals([]);
  }

  private createIdempotencyKey(): string {
    if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
      return crypto.randomUUID();
    }

    return `${Date.now()}-${Math.random().toString(36).slice(2, 12)}`;
  }
}
