import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminOrder, AdminOrderStatus, OrderService } from '../../core/services/order.service';

@Component({
  selector: 'app-orders',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './orders.html',
  styleUrl: './orders.css'
})
export class OrdersComponent implements OnInit {
  private orderService = inject(OrderService);

  orders: AdminOrder[] = [];
  selectedOrder: AdminOrder | null = null;
  searchQuery = '';
  statusFilter: 'all' | '0' | '1' | '2' | '3' | '9' = 'all';
  paymentFilter: 'all' | 'KapidaOdeme' | 'HavaleEFT' = 'all';
  dateFrom = '';
  dateTo = '';
  isLoading = false;
  isUpdatingStatus = false;
  loadError: string | null = null;
  statusDraft: AdminOrderStatus = 0;

  ngOnInit(): void {
    this.loadOrders();
  }

  loadOrders() {
    this.isLoading = true;
    this.loadError = null;
    this.orderService.getIncomingOrders().subscribe({
      next: (orders) => {
        this.orders = (orders || []).sort((a, b) =>
          new Date(b.createdDate).getTime() - new Date(a.createdDate).getTime()
        );
        this.syncSelection();
        this.isLoading = false;
      },
      error: (err) => {
        this.isLoading = false;
        this.orders = [];
        this.selectedOrder = null;
        this.loadError = err?.error?.message || err?.error || 'Siparişler yüklenemedi.';
      }
    });
  }

  selectOrder(order: AdminOrder) {
    this.selectedOrder = order;
    this.statusDraft = order.status;
  }

  onSearch(value: string) {
    this.searchQuery = value;
    this.syncSelection();
  }

  onStatusFilterChange(value: 'all' | '0' | '1' | '2' | '3' | '9') {
    this.statusFilter = value;
    this.syncSelection();
  }

  onPaymentFilterChange(value: 'all' | 'KapidaOdeme' | 'HavaleEFT') {
    this.paymentFilter = value;
    this.syncSelection();
  }

  onDateFromChange(value: string) {
    this.dateFrom = value;
    this.syncSelection();
  }

  onDateToChange(value: string) {
    this.dateTo = value;
    this.syncSelection();
  }

  clearDateFilter() {
    this.dateFrom = '';
    this.dateTo = '';
    this.syncSelection();
  }

  get filteredOrders(): AdminOrder[] {
    const query = this.searchQuery.trim().toLowerCase();
    const fromDate = this.dateFrom ? new Date(`${this.dateFrom}T00:00:00`) : null;
    const toDate = this.dateTo ? new Date(`${this.dateTo}T23:59:59.999`) : null;

    return this.orders.filter((order) => {
      if (this.statusFilter !== 'all' && String(order.status) !== this.statusFilter) return false;
      if (this.paymentFilter !== 'all' && (order.paymentMethod || 'KapidaOdeme') !== this.paymentFilter) return false;

      const createdAt = new Date(order.createdDate);
      if (fromDate && createdAt < fromDate) return false;
      if (toDate && createdAt > toDate) return false;

      if (!query) return true;

      return [
        order.orderNumber,
        order.customerName,
        order.customerPhone,
        order.customerEmail,
        order.deliveryCity,
        order.deliveryDistrict,
        order.deliveryAddress
      ]
        .filter(Boolean)
        .some(v => String(v).toLowerCase().includes(query));
    });
  }

  get summary() {
    return this.filteredOrders.reduce((acc, order) => {
      acc.total += 1;
      acc.totalAmount += order.totalAmount || 0;
      if (order.status === 0) acc.pending += 1;
      if (order.status === 1) acc.processing += 1;
      if (order.status === 2) acc.shipped += 1;
      if (order.status === 3) acc.completed += 1;
      return acc;
    }, {
      total: 0,
      pending: 0,
      processing: 0,
      shipped: 0,
      completed: 0,
      totalAmount: 0
    });
  }

  private syncSelection() {
    const filtered = this.filteredOrders;
    if (filtered.length === 0) {
      this.selectedOrder = null;
      this.statusDraft = 0;
      return;
    }

    if (this.selectedOrder) {
      const existing = filtered.find(o => o.id === this.selectedOrder!.id);
      if (existing) {
        this.selectedOrder = existing;
        this.statusDraft = existing.status;
        return;
      }
    }

    this.selectedOrder = filtered[0];
    this.statusDraft = this.selectedOrder.status;
  }

  updateStatus() {
    if (!this.selectedOrder || this.isUpdatingStatus) return;

    this.isUpdatingStatus = true;
    this.orderService.updateOrderStatus(this.selectedOrder.id, this.statusDraft).subscribe({
      next: (updated) => {
        const idx = this.orders.findIndex(o => o.id === updated.id);
        if (idx >= 0) this.orders[idx] = updated;
        this.syncSelection();
        this.isUpdatingStatus = false;
      },
      error: () => {
        this.isUpdatingStatus = false;
      }
    });
  }

  getStatusLabel(status: number): string {
    switch (status) {
      case 0: return 'Bekliyor';
      case 1: return 'Hazırlanıyor';
      case 2: return 'Kargoda';
      case 3: return 'Tamamlandı';
      case 9: return 'İptal';
      default: return String(status);
    }
  }

  getStatusClass(status: number): string {
    switch (status) {
      case 0: return 'status-pending';
      case 1: return 'status-processing';
      case 2: return 'status-shipped';
      case 3: return 'status-completed';
      case 9: return 'status-cancelled';
      default: return 'status-pending';
    }
  }

  getPaymentMethodLabel(paymentMethod?: string | null): string {
    if (paymentMethod === 'HavaleEFT') return 'Havale / EFT';
    if (paymentMethod === 'KapidaOdeme') return 'Kapıda Ödeme';
    return paymentMethod || 'Belirtilmedi';
  }

  getItemLineTotal(quantity: number, unitPrice: number): number {
    return (quantity || 0) * (unitPrice || 0);
  }
}
