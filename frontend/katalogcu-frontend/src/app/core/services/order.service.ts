import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment.development';

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

  updateOrderStatus(orderId: string, status: AdminOrderStatus): Observable<AdminOrder> {
    return this.http.put<AdminOrder>(`${this.apiUrl}/orders/${orderId}/status`, { status });
  }
}
