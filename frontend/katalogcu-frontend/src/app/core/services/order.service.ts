import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export type AdminOrderStatus = 0 | 1 | 2 | 3 | 9;

export interface AdminOrderProduct {
  id: string;
  code?: string;
  name?: string;
  price?: number;
  imageUrl?: string;
}

export interface AdminOrderItem {
  id: string;
  productId: string;
  quantity: number;
  unitPrice: number;
  product?: AdminOrderProduct | null;
}

export interface AdminOrderStatusHistory {
  id: string;
  previousStatus?: number | null;
  newStatus: number;
  isVisibleToCustomer?: boolean;
  source: string;
  note?: string | null;
  changedBy?: string | null;
  createdDate: string;
}

export interface AdminOrder {
  id: string;
  orderNumber: string;
  customerName: string;
  customerPhone: string;
  customerEmail: string;
  companyName?: string | null;
  customerId?: string | null;
  deliveryAddress?: string;
  deliveryCity?: string;
  deliveryDistrict?: string | null;
  deliveryNote?: string | null;
  paymentMethod?: string;
  totalAmount: number;
  status: AdminOrderStatus;
  createdDate: string;
  items: AdminOrderItem[];
  statusHistory?: AdminOrderStatusHistory[];
}

@Injectable({
  providedIn: 'root'
})
export class OrderService {
  private http = inject(HttpClient);
  private apiUrl = environment.apiUrl;

  getIncomingOrders(): Observable<AdminOrder[]> {
    return this.http.get<AdminOrder[]>(`${this.apiUrl}/orders`);
  }

  updateOrderStatus(
    orderId: string,
    status: AdminOrderStatus,
    note?: string,
    isVisibleToCustomer: boolean = true
  ): Observable<AdminOrder> {
    return this.http.put<AdminOrder>(`${this.apiUrl}/orders/${orderId}/status`, {
      status,
      note,
      isVisibleToCustomer
    });
  }
}
