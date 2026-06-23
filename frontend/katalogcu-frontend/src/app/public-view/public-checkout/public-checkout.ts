import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CartService } from '../../core/services/cart.service';
import { CustomerService, PublicCustomerOrder, PublicCustomerOrderDetail } from '../../core/services/customer.service';
import { CatalogService, PublicStorefront } from '../../core/services/catalog.service';
import { environment } from '../../../environments/environment';

type AuthTab = 'login' | 'register';
type PaymentMethod = 'KapidaOdeme' | 'HavaleEFT';

interface CheckoutDetails {
  deliveryAddress: string;
  deliveryCity: string;
  deliveryDistrict: string;
  deliveryNote: string;
  paymentMethod: PaymentMethod;
}

@Component({
  selector: 'app-public-checkout',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './public-checkout.html',
  styleUrl: './public-checkout.css'
})
export class PublicCheckoutComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  public cartService = inject(CartService);
  private customerService = inject(CustomerService);
  private catalogService = inject(CatalogService);

  publicToken = this.route.snapshot.paramMap.get('publicToken');
  storefront: PublicStorefront = {
    businessName: 'Katalog Magazasi',
    ecommerceEnabled: true
  };
  authTab: AuthTab = 'login';

  loginForm = { phone: '', email: '', password: '' };
  registerForm = { name: '', phone: '', email: '', password: '', confirmPassword: '' };
  resetForm = { phone: '', email: '', code: '', newPassword: '', confirmPassword: '' };
  guestForm = {
    name: '',
    phone: '',
    email: '',
    note: '',
    deliveryAddress: '',
    deliveryCity: '',
    deliveryDistrict: '',
    deliveryNote: '',
    paymentMethod: 'KapidaOdeme' as PaymentMethod
  };
  memberCheckout: CheckoutDetails = {
    deliveryAddress: '',
    deliveryCity: '',
    deliveryDistrict: '',
    deliveryNote: '',
    paymentMethod: 'KapidaOdeme'
  };

  isLoggingIn = false;
  isRegistering = false;
  isSavingGuest = false;
  isSubmitting = false;
  isLoadingOrders = false;
  isRequestingResetCode = false;
  isConfirmingReset = false;

  showResetPassword = false;

  loginMessage: string | null = null;
  loginError: string | null = null;
  registerMessage: string | null = null;
  registerError: string | null = null;
  resetMessage: string | null = null;
  resetError: string | null = null;
  guestMessage: string | null = null;
  guestError: string | null = null;
  orderMessage: string | null = null;
  orderError: string | null = null;
  orderDetailError: string | null = null;
  isLoadingOrderDetail = false;
  selectedOrderId: string | null = null;
  selectedOrderDetail: PublicCustomerOrderDetail | null = null;

  customerSessionToken: string | null = null;
  loggedCustomer: any | null = null;
  myOrders: PublicCustomerOrder[] = [];

  private getSessionKey(): string {
    return `public_customer_session_${this.publicToken ?? 'anonymous'}`;
  }

  get canUseEcommerce(): boolean {
    return environment.features.enableEcommerce && this.storefront.ecommerceEnabled !== false;
  }

  ngOnInit(): void {
    if (!this.publicToken) return;
    if (!environment.features.enableEcommerce) {
      this.router.navigate(['/p', this.publicToken]);
      return;
    }
    this.cartService.setScope(`public:${this.publicToken}`);
    this.cartService.setPublicToken(this.publicToken);
    this.loadStorefront();
    const stored = localStorage.getItem(this.getSessionKey());
    if (!stored) return;
    this.customerSessionToken = stored;
    this.fetchCurrentCustomer();
  }

  private fetchCurrentCustomer() {
    if (!this.publicToken || !this.customerSessionToken) return;
    this.customerService.getPublicCustomerMe(this.publicToken, this.customerSessionToken).subscribe({
      next: (me) => {
        this.loggedCustomer = me;
        this.loginForm.phone = me?.phone || '';
        this.loginForm.email = me?.email || '';
        this.guestForm.name = me?.name || '';
        this.guestForm.phone = me?.phone || '';
        this.guestForm.email = me?.email || '';
        this.loadMyOrders();
      },
      error: () => {
        this.logout();
      }
    });
  }

  ngOnDestroy(): void {
  }

  private loadStorefront() {
    if (!this.publicToken) return;
    this.catalogService.getPublicStorefront(this.publicToken).subscribe({
      next: (res) => {
        this.storefront = {
          businessName: res.businessName?.trim() || 'Katalog Magazasi',
          ecommerceEnabled: res.ecommerceEnabled ?? true
        };
      },
      error: () => { }
    });
  }

  goBackToPublic() {
    if (!this.publicToken) return;
    this.router.navigate(['/p', this.publicToken]);
  }

  setAuthTab(tab: AuthTab) {
    this.authTab = tab;
    this.showResetPassword = false;
    this.loginMessage = null;
    this.loginError = null;
    this.registerMessage = null;
    this.registerError = null;
    this.resetMessage = null;
    this.resetError = null;
  }

  toggleResetPassword() {
    this.showResetPassword = !this.showResetPassword;
    this.resetMessage = null;
    this.resetError = null;
    this.resetForm = {
      phone: this.loginForm.phone || '',
      email: this.loginForm.email || '',
      code: '',
      newPassword: '',
      confirmPassword: ''
    };
  }

  login() {
    if (!this.publicToken) return;
    if (!this.loginForm.password || (!this.loginForm.phone && !this.loginForm.email)) {
      this.loginError = 'Telefon/e-posta ve şifre zorunlu.';
      this.loginMessage = null;
      return;
    }

    this.isLoggingIn = true;
    this.loginError = null;
    this.loginMessage = null;

    this.customerService.loginPublicCustomer({
      publicToken: this.publicToken,
      phone: this.loginForm.phone || undefined,
      email: this.loginForm.email || undefined,
      password: this.loginForm.password
    }).subscribe({
      next: (res) => {
        this.isLoggingIn = false;
        this.loginForm.password = '';
        this.customerSessionToken = res.sessionToken;
        localStorage.setItem(this.getSessionKey(), res.sessionToken);
        this.loggedCustomer = res.customer;
        this.guestForm.name = res.customer?.name || '';
        this.guestForm.phone = res.customer?.phone || '';
        this.guestForm.email = res.customer?.email || '';
        this.loginMessage = 'Giriş başarılı.';
        this.loadMyOrders();
      },
      error: (err) => {
        this.isLoggingIn = false;
        this.loginError = err?.error?.message || err?.error || 'Giriş yapılamadı.';
      }
    });
  }

  register() {
    if (!this.publicToken) return;
    if (!this.registerForm.name || !this.registerForm.phone || !this.registerForm.password) {
      this.registerError = 'Ad soyad, telefon ve şifre zorunlu.';
      this.registerMessage = null;
      return;
    }
    if (this.registerForm.password.length < 8) {
      this.registerError = 'Şifre en az 8 karakter olmalı.';
      this.registerMessage = null;
      return;
    }
    if (this.registerForm.password !== this.registerForm.confirmPassword) {
      this.registerError = 'Şifre tekrarı eşleşmiyor.';
      this.registerMessage = null;
      return;
    }

    this.isRegistering = true;
    this.registerError = null;
    this.registerMessage = null;

    this.customerService.registerPublicCustomer({
      publicToken: this.publicToken,
      name: this.registerForm.name,
      phone: this.registerForm.phone,
      email: this.registerForm.email || undefined,
      password: this.registerForm.password
    }).subscribe({
      next: (res) => {
        this.isRegistering = false;
        this.registerForm.password = '';
        this.registerForm.confirmPassword = '';
        this.customerSessionToken = res.sessionToken;
        localStorage.setItem(this.getSessionKey(), res.sessionToken);
        this.loggedCustomer = res.customer;
        this.guestForm.name = res.customer?.name || '';
        this.guestForm.phone = res.customer?.phone || '';
        this.guestForm.email = res.customer?.email || '';
        this.registerMessage = 'Hesap tamamlandı ve giriş yapıldı.';
        this.loadMyOrders();
      },
      error: (err) => {
        this.isRegistering = false;
        this.registerError = err?.error?.message || err?.error || 'Hesap tamamlanamadı.';
      }
    });
  }

  requestResetCode() {
    if (!this.publicToken) return;
    if (!this.resetForm.phone && !this.resetForm.email) {
      this.resetError = 'Telefon veya e-posta zorunlu.';
      this.resetMessage = null;
      return;
    }

    this.isRequestingResetCode = true;
    this.resetMessage = null;
    this.resetError = null;

    this.customerService.requestPublicPasswordReset({
      publicToken: this.publicToken,
      phone: this.resetForm.phone || undefined,
      email: this.resetForm.email || undefined
    }).subscribe({
      next: (res) => {
        this.isRequestingResetCode = false;
        const devCodeSuffix = res?.resetCode ? ` (Geliştirme kodu: ${res.resetCode})` : '';
        this.resetMessage = (res?.message || 'Sıfırlama kodu gönderildi.') + devCodeSuffix;
        this.resetError = null;
        if (res?.resetCode) {
          this.resetForm.code = res.resetCode;
        }
      },
      error: (err) => {
        this.isRequestingResetCode = false;
        this.resetError = err?.error?.message || err?.error || 'Sıfırlama kodu alınamadı.';
        this.resetMessage = null;
      }
    });
  }

  confirmResetPassword() {
    if (!this.publicToken) return;
    if (!this.resetForm.code || !this.resetForm.newPassword || !this.resetForm.confirmPassword) {
      this.resetError = 'Kod ve yeni şifre alanları zorunlu.';
      this.resetMessage = null;
      return;
    }
    if (this.resetForm.newPassword.length < 8) {
      this.resetError = 'Yeni şifre en az 8 karakter olmalı.';
      this.resetMessage = null;
      return;
    }
    if (this.resetForm.newPassword !== this.resetForm.confirmPassword) {
      this.resetError = 'Yeni şifre tekrarı eşleşmiyor.';
      this.resetMessage = null;
      return;
    }

    this.isConfirmingReset = true;
    this.resetMessage = null;
    this.resetError = null;

    this.customerService.confirmPublicPasswordReset({
      publicToken: this.publicToken,
      phone: this.resetForm.phone || undefined,
      email: this.resetForm.email || undefined,
      resetCode: this.resetForm.code,
      newPassword: this.resetForm.newPassword
    }).subscribe({
      next: (res) => {
        this.isConfirmingReset = false;
        this.customerSessionToken = res.sessionToken;
        localStorage.setItem(this.getSessionKey(), res.sessionToken);
        this.loggedCustomer = res.customer;
        this.guestForm.name = res.customer?.name || '';
        this.guestForm.phone = res.customer?.phone || '';
        this.guestForm.email = res.customer?.email || '';
        this.loginForm.password = '';
        this.resetForm = { phone: '', email: '', code: '', newPassword: '', confirmPassword: '' };
        this.showResetPassword = false;
        this.resetMessage = 'Şifre güncellendi ve giriş yapıldı.';
        this.resetError = null;
        this.loadMyOrders();
      },
      error: (err) => {
        this.isConfirmingReset = false;
        this.resetError = err?.error?.message || err?.error || 'Şifre güncellenemedi.';
        this.resetMessage = null;
      }
    });
  }

  saveGuestInfo() {
    this.isSavingGuest = false;
    this.guestMessage = null;
    this.guestError = 'Misafir kayıt kapalı. Portal erişimi panelden tanımlanan müşterilerle sınırlıdır.';
  }

  submitOrderAsMember() {
    if (!this.loggedCustomer) return;
    this.submitOrder({
      name: this.loggedCustomer.name,
      phone: this.loggedCustomer.phone,
      email: this.loggedCustomer.email || ''
    }, true, this.memberCheckout);
  }

  submitOrderAsGuest() {
    this.submitOrder({
      name: this.guestForm.name,
      phone: this.guestForm.phone,
      email: this.guestForm.email || '',
      note: this.guestForm.note || undefined
    }, false, {
      deliveryAddress: this.guestForm.deliveryAddress,
      deliveryCity: this.guestForm.deliveryCity,
      deliveryDistrict: this.guestForm.deliveryDistrict,
      deliveryNote: this.guestForm.deliveryNote,
      paymentMethod: this.guestForm.paymentMethod
    });
  }

  private submitOrder(
    customer: { name: string; phone: string; email: string; note?: string },
    asMember: boolean,
    details: CheckoutDetails
  ) {
    if (!this.canUseEcommerce) {
      this.orderError = 'Bu vitrinde e-ticaret özelliği aktif değil.';
      this.orderMessage = null;
      return;
    }
    if (!customer.name || !customer.phone) {
      this.orderError = 'Ad soyad ve telefon zorunlu.';
      this.orderMessage = null;
      return;
    }
    if (!details.deliveryAddress || !details.deliveryCity) {
      this.orderError = 'Teslimat adresi ve şehir zorunlu.';
      this.orderMessage = null;
      return;
    }

    this.isSubmitting = true;
    this.orderError = null;
    this.orderMessage = null;

    this.cartService.submitOrder(customer, {
      deliveryAddress: details.deliveryAddress,
      deliveryCity: details.deliveryCity,
      deliveryDistrict: details.deliveryDistrict || undefined,
      deliveryNote: details.deliveryNote || undefined,
      paymentMethod: details.paymentMethod,
      publicToken: this.publicToken || undefined,
      publicSessionToken: asMember ? (this.customerSessionToken || undefined) : undefined
    }).subscribe({
      next: (res: any) => {
        this.isSubmitting = false;
        this.orderMessage = `Sipariş alındı. Sipariş No: ${res?.orderNumber ?? '-'}`;
        this.cartService.clearCart();
        this.memberCheckout = {
          deliveryAddress: '',
          deliveryCity: '',
          deliveryDistrict: '',
          deliveryNote: '',
          paymentMethod: 'KapidaOdeme'
        };
        this.guestForm.deliveryAddress = '';
        this.guestForm.deliveryCity = '';
        this.guestForm.deliveryDistrict = '';
        this.guestForm.deliveryNote = '';
        this.guestForm.paymentMethod = 'KapidaOdeme';
        if (asMember) this.loadMyOrders();
      },
      error: (err) => {
        this.isSubmitting = false;
        this.orderError = err?.error?.message || err?.error || 'Sipariş gönderilemedi.';
      }
    });
  }

  loadMyOrders() {
    if (!this.publicToken || !this.customerSessionToken) return;
    this.isLoadingOrders = true;
    this.selectedOrderId = null;
    this.selectedOrderDetail = null;
    this.orderDetailError = null;
    this.customerService.getPublicCustomerOrders(this.publicToken, this.customerSessionToken).subscribe({
      next: (orders) => {
        this.myOrders = orders || [];
        this.isLoadingOrders = false;
      },
      error: () => {
        this.myOrders = [];
        this.isLoadingOrders = false;
      }
    });
  }

  toggleOrderDetail(order: PublicCustomerOrder) {
    if (!this.publicToken || !this.customerSessionToken) return;

    if (this.selectedOrderId === order.id) {
      this.selectedOrderId = null;
      this.selectedOrderDetail = null;
      this.orderDetailError = null;
      return;
    }

    this.selectedOrderId = order.id;
    this.selectedOrderDetail = null;
    this.orderDetailError = null;
    this.isLoadingOrderDetail = true;

    this.customerService.getPublicCustomerOrderDetail(this.publicToken, this.customerSessionToken, order.id).subscribe({
      next: (detail) => {
        if (this.selectedOrderId !== order.id) return;
        this.selectedOrderDetail = detail;
        this.isLoadingOrderDetail = false;
      },
      error: (err) => {
        if (this.selectedOrderId !== order.id) return;
        this.selectedOrderDetail = null;
        this.isLoadingOrderDetail = false;
        this.orderDetailError = err?.error?.message || err?.error || 'Sipariş detayı yüklenemedi.';
      }
    });
  }

  getPaymentMethodLabel(method?: string | null): string {
    if (method === 'HavaleEFT') return 'Havale / EFT';
    if (method === 'KapidaOdeme') return 'Kapıda Ödeme';
    return method || 'Belirtilmedi';
  }

  logout() {
    this.customerSessionToken = null;
    this.loggedCustomer = null;
    this.myOrders = [];
    this.selectedOrderId = null;
    this.selectedOrderDetail = null;
    this.orderDetailError = null;
    this.loginForm.password = '';
    this.showResetPassword = false;
    this.resetMessage = null;
    this.resetError = null;
    localStorage.removeItem(this.getSessionKey());
  }

  formatOrderStatus(status: number): string {
    switch (status) {
      case 0: return 'Bekliyor';
      case 1: return 'Hazırlanıyor';
      case 2: return 'Kargoda';
      case 3: return 'Tamamlandı';
      case 9: return 'İptal';
      default: return String(status);
    }
  }

  getStatusSourceLabel(source?: string | null): string {
    const value = (source || '').toLowerCase();
    if (value === 'ordercreated') return 'Sipariş';
    if (value === 'adminupdate') return 'İşletme';
    return source || 'Sistem';
  }
}
