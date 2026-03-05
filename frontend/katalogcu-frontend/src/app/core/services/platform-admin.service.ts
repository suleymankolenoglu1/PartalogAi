import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export interface PlatformTenant {
  ownerId: string;
  ownerFullName: string;
  ownerEmail: string;
  companyName: string | null;
  phoneNumber: string | null;
  isSuspended: boolean;
  plan: number;
  planName: string;
  planExpiresAt: string | null;
  limits: {
    maxCatalogCount: number;
    maxPagePerCatalog: number;
  };
  usage: {
    catalogCount: number;
    partCount: number;
    customerCount?: number;
    orderCount?: number;
    lastCatalogAt: string | null;
  };
  createdAt: string;
  updatedAt: string | null;
}

interface PlatformTenantListResponse {
  total: number;
  items: PlatformTenant[];
}

export interface PlatformMetricsResponse {
  totals: {
    tenants: number;
    activeTenants: number;
    suspendedTenants: number;
    catalogs: number;
    parts: number;
    orders: number;
    aiJobs: number;
  };
  plans: {
    plan: number;
    planName: string;
    count: number;
  }[];
}

export interface UpdateTenantPlanRequest {
  plan: number;
  expiresAt?: string | null;
  reason?: string | null;
  operationId?: string | null;
}

export interface TenantUsagePoint {
  month: string;
  catalogs: number;
  parts: number;
  orders: number;
  aiJobs: number;
}

export interface TenantAuditEvent {
  timestamp: string;
  type: string;
  title: string;
  detail?: string | null;
  operationId?: string | null;
  operationCount?: number | null;
  changes?: TenantAuditChange[] | null;
}

export interface TenantAuditChange {
  field: string;
  before: string | null;
  after: string | null;
}

export interface PlatformTenantDetail {
  ownerId: string;
  ownerFullName: string;
  ownerEmail: string;
  companyName: string | null;
  phoneNumber: string | null;
  role: string;
  isSuspended: boolean;
  publicLinkEnabled: boolean;
  plan: number;
  planName: string;
  planActivatedAt: string | null;
  planExpiresAt: string | null;
  limits: {
    maxCatalogCount: number;
    maxPagePerCatalog: number;
  };
  usageTotals: {
    catalogCount: number;
    partCount: number;
    orderCount: number;
    aiJobCount: number;
    lastCatalogAt: string | null;
    lastOrderAt: string | null;
  };
  ecommerceEnabled: boolean;
  customerTotals: {
    customerCount: number;
    activeCustomerCount: number;
    totalRevenue: number;
    lastOrderAt: string | null;
  };
  topCustomers: {
    id: string;
    fullName: string;
    phone: string;
    email: string | null;
    isActive: boolean;
    orderCount: number;
    totalSpent: number;
    lastOrderDate: string | null;
    lastLoginDate: string | null;
  }[];
  recentOrders: {
    id: string;
    orderNumber: string;
    status: string;
    totalAmount: number;
    createdAt: string;
    customerName: string;
    customerPhone: string;
    customerEmail: string;
    deliveryAddress: string | null;
    deliveryCity: string | null;
    deliveryDistrict: string | null;
    deliveryNote: string | null;
    paymentMethod: string;
    itemCount: number;
    items: {
      productId: string;
      productCode: string;
      productName: string;
      quantity: number;
      unitPrice: number;
      lineTotal: number;
    }[];
  }[];
  monthlyUsage: TenantUsagePoint[];
  recentCatalogs: {
    id: string;
    name: string;
    status: string;
    createdAt: string;
    updatedAt: string | null;
  }[];
  auditLog: TenantAuditEvent[];
}

@Injectable({
  providedIn: 'root'
})
export class PlatformAdminService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = environment.apiUrl;

  getTenants(query = '', plan: number | null = null, status: 'all' | 'active' | 'suspended' = 'all') {
    const q = query.trim();
    const params = new URLSearchParams();
    if (q) params.set('q', q);
    if (plan && plan >= 1 && plan <= 3) params.set('plan', String(plan));
    if (status !== 'all') params.set('status', status);
    const queryString = params.toString();
    const url = queryString
      ? `${this.apiUrl}/platform/tenants?${queryString}`
      : `${this.apiUrl}/platform/tenants`;
    return this.http.get<PlatformTenantListResponse>(url);
  }

  getMetrics() {
    return this.http.get<PlatformMetricsResponse>(`${this.apiUrl}/platform/tenants/metrics`);
  }

  getTenantDetail(ownerId: string) {
    return this.http.get<PlatformTenantDetail>(`${this.apiUrl}/platform/tenants/${ownerId}`);
  }

  updateTenantPlan(ownerId: string, payload: UpdateTenantPlanRequest) {
    return this.http.patch(`${this.apiUrl}/platform/tenants/${ownerId}/plan`, payload);
  }

  suspendTenant(ownerId: string, reason?: string | null, operationId?: string | null) {
    return this.http.post(`${this.apiUrl}/platform/tenants/${ownerId}/suspend`, {
      reason: reason ?? null,
      operationId: operationId ?? null
    });
  }

  unsuspendTenant(ownerId: string, reason?: string | null, operationId?: string | null) {
    return this.http.post(`${this.apiUrl}/platform/tenants/${ownerId}/unsuspend`, {
      reason: reason ?? null,
      operationId: operationId ?? null
    });
  }
}
