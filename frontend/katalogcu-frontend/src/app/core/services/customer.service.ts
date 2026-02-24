import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment.development';

export interface Customer {
  id: string;
  name: string;
  company?: string | null;
  email?: string | null;
  phone: string;
  orderCount: number;
  totalSpent: number;
  lastVisitDate: string;
  lastOrderDate?: string | null;
  status: 'active' | 'inactive';
  note?: string | null;
  createdDate: string;
}

export interface PublicCustomerRegisterRequest {
  publicToken: string;
  name: string;
  phone: string;
  email?: string;
  companyName?: string;
  note?: string;
}

export interface PublicCustomerRegisterResponse {
  success: boolean;
  created: boolean;
  customerId: string;
  message: string;
}

export interface PublicCustomerLoginRequest {
  publicToken: string;
  phone?: string;
  email?: string;
  password: string;
}

export interface PublicCustomerAccountRegisterRequest {
  publicToken: string;
  name: string;
  phone: string;
  email?: string;
  password: string;
}

export interface PublicCustomerSessionResponse {
  success: boolean;
  sessionToken: string;
  customer: {
    id: string;
    name: string;
    phone: string;
    email?: string;
    company?: string;
  };
}

export interface PublicCustomerPasswordResetRequest {
  publicToken: string;
  phone?: string;
  email?: string;
}

export interface PublicCustomerPasswordResetRequestResponse {
  success: boolean;
  message: string;
  resetCode?: string | null;
}

export interface PublicCustomerPasswordResetConfirmRequest {
  publicToken: string;
  phone?: string;
  email?: string;
  resetCode: string;
  newPassword: string;
}

export interface PublicCustomerOrder {
  id: string;
  orderNumber: string;
  status: number;
  totalAmount: number;
  createdDate: string;
  paymentMethod?: string;
  deliveryCity?: string;
  itemCount?: number;
}

export interface PublicCustomerOrderDetailItem {
  id: string;
  productId: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  product?: {
    id: string;
    code?: string;
    name?: string;
    imageUrl?: string;
    description?: string;
  } | null;
}

export interface PublicCustomerOrderDetail {
  id: string;
  orderNumber: string;
  status: number;
  totalAmount: number;
  createdDate: string;
  customerName: string;
  customerPhone: string;
  customerEmail: string;
  deliveryAddress?: string;
  deliveryCity?: string;
  deliveryDistrict?: string;
  deliveryNote?: string;
  paymentMethod?: string;
  items: PublicCustomerOrderDetailItem[];
}

@Injectable({
  providedIn: 'root'
})
export class CustomerService {
  private http = inject(HttpClient);
  private apiUrl = environment.apiUrl;

  getCustomers(): Observable<Customer[]> {
    return this.http.get<Customer[]>(`${this.apiUrl}/customers`);
  }

  registerFromPublic(payload: PublicCustomerRegisterRequest): Observable<PublicCustomerRegisterResponse> {
    return this.http.post<PublicCustomerRegisterResponse>(`${this.apiUrl}/customers/public-register`, payload);
  }

  loginPublicCustomer(payload: PublicCustomerLoginRequest): Observable<PublicCustomerSessionResponse> {
    return this.http.post<PublicCustomerSessionResponse>(`${this.apiUrl}/customers/public-auth/login`, payload);
  }

  registerPublicCustomer(payload: PublicCustomerAccountRegisterRequest): Observable<PublicCustomerSessionResponse> {
    return this.http.post<PublicCustomerSessionResponse>(`${this.apiUrl}/customers/public-auth/register`, payload);
  }

  requestPublicPasswordReset(payload: PublicCustomerPasswordResetRequest): Observable<PublicCustomerPasswordResetRequestResponse> {
    return this.http.post<PublicCustomerPasswordResetRequestResponse>(`${this.apiUrl}/customers/public-auth/password-reset/request`, payload);
  }

  confirmPublicPasswordReset(payload: PublicCustomerPasswordResetConfirmRequest): Observable<PublicCustomerSessionResponse> {
    return this.http.post<PublicCustomerSessionResponse>(`${this.apiUrl}/customers/public-auth/password-reset/confirm`, payload);
  }

  getPublicCustomerMe(publicToken: string, sessionToken: string): Observable<any> {
    const params = new HttpParams()
      .set('publicToken', publicToken)
      .set('sessionToken', sessionToken);
    return this.http.get<any>(`${this.apiUrl}/customers/public-auth/me`, { params });
  }

  getPublicCustomerOrders(publicToken: string, sessionToken: string): Observable<PublicCustomerOrder[]> {
    const params = new HttpParams()
      .set('publicToken', publicToken)
      .set('sessionToken', sessionToken);
    return this.http.get<PublicCustomerOrder[]>(`${this.apiUrl}/customers/public-auth/orders`, { params });
  }

  getPublicCustomerOrderDetail(publicToken: string, sessionToken: string, orderId: string): Observable<PublicCustomerOrderDetail> {
    const params = new HttpParams()
      .set('publicToken', publicToken)
      .set('sessionToken', sessionToken);
    return this.http.get<PublicCustomerOrderDetail>(`${this.apiUrl}/customers/public-auth/orders/${orderId}`, { params });
  }
}
