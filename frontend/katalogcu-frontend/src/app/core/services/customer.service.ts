import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

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
  lastLoginDate?: string | null;
  lastActivityDate: string;
  hasPassword: boolean;
  status: 'active' | 'inactive';
  note?: string | null;
  createdDate: string;
}

export interface UpsertPortalCustomerRequest {
  name: string;
  phone: string;
  email?: string;
  companyName?: string;
  note?: string;
  initialPassword?: string;
  isActive: boolean;
}

export interface PublicCustomerLoginRequest {
  publicToken: string;
  phone?: string;
  email?: string;
  password: string;
}

export interface PortalHomeLoginRequest {
  identifier: string;
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

export interface PortalHomeLoginResponse extends PublicCustomerSessionResponse {
  publicToken: string;
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

export interface PublicCustomerOrderStatusHistory {
  id: string;
  previousStatus?: number | null;
  newStatus: number;
  source: string;
  note?: string | null;
  changedBy?: string | null;
  createdDate: string;
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
  statusHistory: PublicCustomerOrderStatusHistory[];
}

@Injectable({
  providedIn: 'root'
})
export class CustomerService {
  private http = inject(HttpClient);
  private apiUrl = environment.apiUrl;

  private buildPublicSessionHeaders(sessionToken: string): HttpHeaders {
    return new HttpHeaders({ 'X-Public-Session': sessionToken });
  }

  getCustomers(): Observable<Customer[]> {
    return this.http.get<Customer[]>(`${this.apiUrl}/customers`);
  }

  createPortalCustomer(payload: UpsertPortalCustomerRequest): Observable<Customer> {
    return this.http.post<Customer>(`${this.apiUrl}/customers/portal-users`, payload);
  }

  updatePortalCustomer(id: string, payload: UpsertPortalCustomerRequest): Observable<Customer> {
    return this.http.put<Customer>(`${this.apiUrl}/customers/portal-users/${id}`, payload);
  }

  setPortalCustomerAccess(id: string, isActive: boolean): Observable<Customer> {
    return this.http.patch<Customer>(`${this.apiUrl}/customers/portal-users/${id}/access`, { isActive });
  }

  loginPublicCustomer(payload: PublicCustomerLoginRequest): Observable<PublicCustomerSessionResponse> {
    return this.http.post<PublicCustomerSessionResponse>(`${this.apiUrl}/customers/public-auth/login`, payload);
  }

  loginPortalHome(payload: PortalHomeLoginRequest): Observable<PortalHomeLoginResponse> {
    return this.http.post<PortalHomeLoginResponse>(`${this.apiUrl}/customers/public-auth/portal-login`, payload);
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
    const params = new HttpParams().set('publicToken', publicToken);
    const headers = this.buildPublicSessionHeaders(sessionToken);
    return this.http.get<any>(`${this.apiUrl}/customers/public-auth/me`, { params, headers });
  }

  getPublicCustomerOrders(publicToken: string, sessionToken: string): Observable<PublicCustomerOrder[]> {
    const params = new HttpParams().set('publicToken', publicToken);
    const headers = this.buildPublicSessionHeaders(sessionToken);
    return this.http.get<PublicCustomerOrder[]>(`${this.apiUrl}/customers/public-auth/orders`, { params, headers });
  }

  getPublicCustomerOrderDetail(publicToken: string, sessionToken: string, orderId: string): Observable<PublicCustomerOrderDetail> {
    const params = new HttpParams().set('publicToken', publicToken);
    const headers = this.buildPublicSessionHeaders(sessionToken);
    return this.http.get<PublicCustomerOrderDetail>(`${this.apiUrl}/customers/public-auth/orders/${orderId}`, { params, headers });
  }
}
