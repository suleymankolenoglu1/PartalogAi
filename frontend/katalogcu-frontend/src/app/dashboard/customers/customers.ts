import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Customer, CustomerService, UpsertPortalCustomerRequest } from '../../core/services/customer.service';
import { CatalogService, PublicTokenStatus } from '../../core/services/catalog.service';
import { DomainContextService } from '../../core/services/domain-context.service';

@Component({
  selector: 'app-customers',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './customers.html',
  styleUrl: './customers.css'
})
export class CustomersComponent implements OnInit {
  private customerService = inject(CustomerService);
  private catalogService = inject(CatalogService);
  private domainContext = inject(DomainContextService);

  isLoading = true;
  errorMsg: string | null = null;
  customers: Customer[] = [];
  filteredCustomers: Customer[] = [];
  searchQuery = '';
  statusFilter: 'all' | 'active' | 'pending' | 'inactive' = 'all';
  isFormOpen = false;
  isSaving = false;
  actionCustomerId: string | null = null;
  successMsg: string | null = null;
  editingCustomer: Customer | null = null;
  form = this.createEmptyForm();
  publicTokenStatus: PublicTokenStatus | null = null;
  publicToken: string | null = null;
  publicActionLoading = false;

  get portalUrl(): string {
    return this.publicToken ? this.domainContext.portalUrl(`/p/${this.publicToken}`) : '';
  }

  get activeCustomerCount(): number {
    return this.customers.filter(c => this.getPortalState(c) === 'active').length;
  }

  get pendingCustomerCount(): number {
    return this.customers.filter(c => this.getPortalState(c) === 'pending').length;
  }

  get inactiveCustomerCount(): number {
    return this.customers.filter(c => this.getPortalState(c) === 'inactive').length;
  }

  get invitableCustomerCount(): number {
    return this.customers.filter(c => this.canCopyInvite(c)).length;
  }

  ngOnInit() {
    this.loadCustomers();
    this.loadPublicLinkState();
  }

  loadCustomers() {
    this.isLoading = true;
    this.errorMsg = null;

    this.customerService.getCustomers().subscribe({
      next: (rows) => {
        this.customers = rows || [];
        this.applyFilters();
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Müşteri listesi alınamadı:', err);
        this.errorMsg = 'Müşteri verisi yüklenemedi.';
        this.customers = [];
        this.filteredCustomers = [];
        this.isLoading = false;
      }
    });
  }

  openCreateForm() {
    this.editingCustomer = null;
    this.form = this.createEmptyForm();
    this.isFormOpen = true;
    this.successMsg = null;
    this.errorMsg = null;
  }

  openEditForm(customer: Customer) {
    this.editingCustomer = customer;
    this.form = {
      name: customer.name || '',
      phone: customer.phone || '',
      email: customer.email || '',
      companyName: customer.company || '',
      note: customer.note || '',
      initialPassword: '',
      isActive: customer.status === 'active'
    };
    this.isFormOpen = true;
    this.successMsg = null;
    this.errorMsg = null;
  }

  closeForm() {
    if (this.isSaving) return;
    this.isFormOpen = false;
    this.editingCustomer = null;
    this.form = this.createEmptyForm();
  }

  savePortalCustomer() {
    if (this.isSaving) return;
    if (!this.form.name.trim() || !this.form.phone.trim()) {
      this.errorMsg = 'Ad soyad ve telefon zorunlu.';
      this.successMsg = null;
      return;
    }
    if (this.form.initialPassword && this.form.initialPassword.length < 8) {
      this.errorMsg = 'İlk şifre en az 8 karakter olmalı.';
      this.successMsg = null;
      return;
    }

    const payload: UpsertPortalCustomerRequest = {
      name: this.form.name.trim(),
      phone: this.form.phone.trim(),
      email: this.form.email.trim() || undefined,
      companyName: this.form.companyName.trim() || undefined,
      note: this.form.note.trim() || undefined,
      initialPassword: this.form.initialPassword || undefined,
      isActive: this.form.isActive
    };

    this.isSaving = true;
    this.errorMsg = null;
    this.successMsg = null;
    const request$ = this.editingCustomer
      ? this.customerService.updatePortalCustomer(this.editingCustomer.id, payload)
      : this.customerService.createPortalCustomer(payload);

    request$.subscribe({
      next: (saved) => {
        this.isSaving = false;
        const idx = this.customers.findIndex(x => x.id === saved.id);
        if (idx >= 0) {
          this.customers[idx] = saved;
          this.customers = [...this.customers];
        } else {
          this.customers = [saved, ...this.customers];
        }
        this.applyFilters();
        if (this.editingCustomer) {
          this.successMsg = 'Portal kullanıcısı güncellendi.';
        } else if (this.publicToken) {
          this.successMsg = 'Portal kullanıcısı oluşturuldu. Davet mesajını satırdan kopyalayabilirsiniz.';
        } else {
          this.successMsg = 'Portal kullanıcısı oluşturuldu. Davet göndermek için önce portal linki üretin.';
        }
        this.closeForm();
      },
      error: (err) => {
        this.isSaving = false;
        this.errorMsg = err?.error?.message || err?.error || 'Portal kullanıcısı kaydedilemedi.';
      }
    });
  }

  toggleAccess(customer: Customer) {
    if (this.actionCustomerId) return;
    const nextActive = customer.status !== 'active';
    this.actionCustomerId = customer.id;
    this.errorMsg = null;
    this.successMsg = null;

    this.customerService.setPortalCustomerAccess(customer.id, nextActive).subscribe({
      next: (updated) => {
        this.actionCustomerId = null;
        this.customers = this.customers.map(x => x.id === updated.id ? updated : x);
        this.applyFilters();
        this.successMsg = nextActive
          ? 'Portal erişimi aktif edildi.'
          : 'Portal erişimi pasifleştirildi ve açık oturum kapatıldı.';
      },
      error: (err) => {
        this.actionCustomerId = null;
        this.errorMsg = err?.error?.message || err?.error || 'Portal erişimi güncellenemedi.';
      }
    });
  }

  loadPublicLinkState() {
    this.catalogService.getPublicTokenStatus().subscribe({
      next: (status) => {
        this.publicTokenStatus = status;
        if (!status.enabled) {
          this.publicToken = null;
          return;
        }

        this.catalogService.getPublicToken().subscribe({
          next: (res) => {
            this.publicToken = res.token;
          },
          error: () => {
            this.publicToken = null;
          }
        });
      },
      error: () => {
        this.publicTokenStatus = null;
        this.publicToken = null;
      }
    });
  }

  generatePortalLink() {
    if (this.publicActionLoading) return;
    this.publicActionLoading = true;
    this.errorMsg = null;
    this.successMsg = null;

    const request$ = this.publicTokenStatus?.enabled === false
      ? this.catalogService.rotatePublicToken()
      : this.catalogService.getPublicToken();

    request$.subscribe({
      next: (res: any) => {
        this.publicToken = res.token;
        this.publicTokenStatus = {
          enabled: res.enabled ?? true,
          version: res.version ?? this.publicTokenStatus?.version ?? 1
        };
        this.publicActionLoading = false;
        this.successMsg = 'Portal davet linki hazır.';
      },
      error: () => {
        this.publicActionLoading = false;
        this.errorMsg = 'Portal davet linki üretilemedi.';
      }
    });
  }

  rotatePortalLink() {
    if (this.publicActionLoading) return;
    this.publicActionLoading = true;
    this.errorMsg = null;
    this.successMsg = null;

    this.catalogService.rotatePublicToken().subscribe({
      next: (res) => {
        this.publicToken = res.token;
        this.publicTokenStatus = { enabled: res.enabled, version: res.version };
        this.publicActionLoading = false;
        this.successMsg = 'Portal davet linki yenilendi. Eski linkler iptal edildi.';
      },
      error: () => {
        this.publicActionLoading = false;
        this.errorMsg = 'Portal davet linki yenilenemedi.';
      }
    });
  }

  async copyPortalLink() {
    if (!this.publicToken) return;

    try {
      await navigator.clipboard.writeText(this.portalUrl);
      this.successMsg = 'Portal davet linki panoya kopyalandı.';
      this.errorMsg = null;
    } catch {
      this.successMsg = null;
      this.errorMsg = 'Link kopyalanamadı.';
    }
  }

  async copyInviteMessage(customer: Customer) {
    if (!this.publicToken || !this.portalUrl) {
      this.errorMsg = 'Önce portal davet linki üretin.';
      this.successMsg = null;
      return;
    }
    if (!this.canCopyInvite(customer)) {
      this.errorMsg = 'Pasif müşteriye davet gönderilemez. Önce portal erişimini aktifleştirin.';
      this.successMsg = null;
      return;
    }

    const greetingName = customer.name ? ` ${customer.name}` : '';
    const credentialHint = customer.email
      ? `Panelde kayıtlı telefon/e-posta: ${customer.phone} / ${customer.email}`
      : `Panelde kayıtlı telefon: ${customer.phone}`;
    const authHint = customer.hasPassword
      ? 'Daha önce şifre oluşturduysanız "Giriş" sekmesinden devam edin.'
      : '"Hesabı Tamamla" sekmesinden telefon bilginizle şifrenizi oluşturun.';
    const message = [
      `Merhaba${greetingName},`,
      '',
      'Müşteri portalı erişiminiz açıldı. Katalog ve parça asistanına aşağıdaki linkten ulaşabilirsiniz:',
      this.portalUrl,
      '',
      credentialHint,
      authHint
    ].join('\n');

    try {
      await navigator.clipboard.writeText(message);
      this.successMsg = `${customer.name || 'Portal kullanıcısı'} için davet mesajı kopyalandı.`;
      this.errorMsg = null;
    } catch {
      this.successMsg = null;
      this.errorMsg = 'Davet mesajı kopyalanamadı.';
    }
  }

  onSearch(query: string) {
    this.searchQuery = query;
    this.applyFilters();
  }

  onStatusFilterChange(value: string) {
    if (value === 'active' || value === 'pending' || value === 'inactive' || value === 'all') {
      this.statusFilter = value;
      this.applyFilters();
    }
  }

  applyFilters() {
    const q = this.searchQuery.trim().toLowerCase();

    this.filteredCustomers = this.customers.filter(c => {
      const statusOk = this.statusFilter === 'all' || this.getPortalState(c) === this.statusFilter;
      if (!statusOk) return false;
      if (!q) return true;

      return (
        (c.name || '').toLowerCase().includes(q) ||
        (c.company || '').toLowerCase().includes(q) ||
        (c.email || '').toLowerCase().includes(q) ||
        (c.phone || '').toLowerCase().includes(q)
      );
    });
  }

  getStatusBadge(status: string) {
    switch (status) {
      case 'active':
        return 'bg-green-100 text-green-800 dark:bg-green-500/20 dark:text-green-400';
      case 'pending':
        return 'bg-amber-100 text-amber-800 dark:bg-amber-500/20 dark:text-amber-300';
      case 'inactive':
        return 'bg-gray-100 text-gray-800 dark:bg-gray-500/20 dark:text-gray-400';
      default:
        return 'bg-gray-100 text-gray-800 dark:bg-gray-500/20 dark:text-gray-400';
    }
  }

  getPortalState(customer: Customer): 'active' | 'pending' | 'inactive' {
    if (customer.status !== 'active') return 'inactive';
    return customer.hasPassword ? 'active' : 'pending';
  }

  getPortalStateLabel(customer: Customer): string {
    switch (this.getPortalState(customer)) {
      case 'active':
        return 'Aktif';
      case 'pending':
        return 'Hesap Bekliyor';
      case 'inactive':
        return 'Pasif';
    }
  }

  getPortalStateHint(customer: Customer): string {
    switch (this.getPortalState(customer)) {
      case 'active':
        return customer.lastLoginDate ? 'Müşteri giriş yaptı.' : 'Şifre tanımlı, ilk giriş bekleniyor.';
      case 'pending':
        return 'Davet gönderildiğinde müşteri şifresini oluşturacak.';
      case 'inactive':
        return 'Portal erişimi kapalı.';
    }
  }

  canCopyInvite(customer: Customer): boolean {
    return !!this.publicToken && customer.status === 'active';
  }

  formatDate(date: string | null | undefined): string {
    if (!date) return '-';
    const parsed = new Date(date);
    if (Number.isNaN(parsed.getTime())) return '-';
    return parsed.toLocaleDateString('tr-TR');
  }

  formatDateTime(date: string | null | undefined): string {
    if (!date) return '-';
    const parsed = new Date(date);
    if (Number.isNaN(parsed.getTime())) return '-';
    return parsed.toLocaleString('tr-TR', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  private createEmptyForm() {
    return {
      name: '',
      phone: '',
      email: '',
      companyName: '',
      note: '',
      initialPassword: '',
      isActive: true
    };
  }
}
