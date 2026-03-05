import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { tap } from 'rxjs/operators';
import { Router } from '@angular/router';
import { getPlanDisplayName, normalizePlan, planFromRaw, PlanId } from '../models/plan.model';

export interface AuthUserInfo {
  id: string;
  userId?: string;
  firstName: string;
  lastName: string;
  email: string;
  companyName?: string | null;
  phoneNumber?: string | null;
  role: string;
  subscriptionPlan?: number;
  planName?: string;
  planActivatedAt?: string | null;
  planExpiresAt?: string | null;
  planSelected?: boolean;
  maxCatalogCount?: number;
  maxPagePerCatalog?: number;
}

export interface UserSession {
  userId: string;
  token: string;
  plan: PlanId;
  planName: string;
  planSelected: boolean;
  maxCatalogs: number;
  expiresAt: string | null;
}

interface LoginResponse {
  token: string;
  userId: string;
  plan: number;
  planName: string;
  planSelected?: boolean;
  maxCatalogs: number;
  expiresAt: string | null;
  user: AuthUserInfo;
}

export interface UpdateProfileRequest {
  firstName: string;
  lastName: string;
  companyName?: string | null;
  phoneNumber?: string | null;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);
  private apiUrl = environment.apiUrl;
  private readonly sessionStorageKey = 'user_session';

  // Giriş Yap
  login(credentials: { email: string; password: string }) {
    return this.http.post<LoginResponse>(`${this.apiUrl}/auth/login`, credentials).pipe(
      tap(response => this.persistLoginResponse(response))
    );
  }

  platformLogin(credentials: { email: string; password: string }) {
    return this.http.post<LoginResponse>(`${this.apiUrl}/platform-auth/login`, credentials).pipe(
      tap(response => this.persistLoginResponse(response))
    );
  }

  register(userData: { fullName: string; email: string; password: string }) {
    return this.http.post<any>(`${this.apiUrl}/auth/register`, userData);
  }

  // Çıkış Yap
  logout() {
    localStorage.removeItem('auth_token');
    localStorage.removeItem('user_info');
    localStorage.removeItem(this.sessionStorageKey);
    this.router.navigate(['/login']);
  }

  // Kullanıcı giriş yapmış mı?
  isLoggedIn(): boolean {
    return !!localStorage.getItem('auth_token');
  }

  // Token'ı getir
  getToken(): string | null {
    return localStorage.getItem('auth_token');
  }

  getStoredUserInfo(): AuthUserInfo | null {
    const raw = localStorage.getItem('user_info');
    if (!raw) return null;
    try {
      return JSON.parse(raw) as AuthUserInfo;
    } catch {
      return null;
    }
  }

  setStoredUserInfo(user: AuthUserInfo) {
    localStorage.setItem('user_info', JSON.stringify(user));
  }

  getSession(): UserSession | null {
    const raw = localStorage.getItem(this.sessionStorageKey);
    if (!raw) return null;
    try {
      const parsed = JSON.parse(raw) as UserSession;
      if (!parsed.userId || !parsed.token) return null;
      const plan = normalizePlan(parsed.plan);
      return {
        userId: parsed.userId,
        token: parsed.token,
        plan,
        planName: getPlanDisplayName(plan),
        planSelected: !!parsed.planSelected,
        maxCatalogs: Number(parsed.maxCatalogs || 0) > 0 ? Number(parsed.maxCatalogs) : 3,
        expiresAt: parsed.expiresAt ?? null
      };
    } catch {
      return null;
    }
  }

  setSession(session: UserSession) {
    const plan = normalizePlan(session.plan);
    const safe: UserSession = {
      ...session,
      plan,
      planName: getPlanDisplayName(plan),
      planSelected: !!session.planSelected,
      maxCatalogs: Number(session.maxCatalogs || 0) > 0 ? Number(session.maxCatalogs) : 3,
      expiresAt: session.expiresAt ?? null
    };
    localStorage.setItem(this.sessionStorageKey, JSON.stringify(safe));
  }

  // ✅ UserId'yi getir (user_info yoksa token'dan al)
  getUserId(): string | null {
    const session = this.getSession();
    if (session?.userId) return session.userId;

    const user = this.getStoredUserInfo();
    if (user) {
      if (user.id) return user.id;
      if (user.userId) return user.userId;
    }

    const token = this.getToken();
    if (!token) return null;

    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload?.nameid || payload?.sub || payload?.userId || null;
    } catch {
      return null;
    }
  }

  getCurrentPlan(): PlanId {
    const sessionPlan = this.getSession()?.plan;
    if (sessionPlan) return normalizePlan(sessionPlan);

    const userPlan = this.getStoredUserInfo()?.subscriptionPlan;
    if (typeof userPlan === 'number') return normalizePlan(userPlan);

    const tokenPlan = this.getPlanFromToken();
    if (tokenPlan) return tokenPlan;

    return 1;
  }

  getCurrentPlanDisplayName(): string {
    return getPlanDisplayName(this.getCurrentPlan());
  }

  getCurrentRole(): string {
    return (this.getStoredUserInfo()?.role ?? '').toLowerCase();
  }

  isPlatformAdmin(): boolean {
    return this.getCurrentRole() === 'platformadmin';
  }

  isPlanSelected(): boolean {
    const session = this.getSession();
    if (session) return !!session.planSelected;
    const user = this.getStoredUserInfo();
    return !!user?.planSelected;
  }

  getMe() {
    return this.http.get<AuthUserInfo>(`${this.apiUrl}/auth/me`);
  }

  updateMe(payload: UpdateProfileRequest) {
    return this.http.put<AuthUserInfo>(`${this.apiUrl}/auth/me`, payload).pipe(
      tap((user) => this.setStoredUserInfo(user))
    );
  }

  selectPlan(plan: PlanId) {
    return this.http.post<AuthUserInfo>(`${this.apiUrl}/auth/select-plan`, { plan }).pipe(
      tap((user) => {
        this.applyUserSessionFromUser(user, plan);
      })
    );
  }

  cancelPlan() {
    return this.http.post<AuthUserInfo>(`${this.apiUrl}/auth/cancel-plan`, {}).pipe(
      tap((user) => {
        this.applyUserSessionFromUser(user, 1);
      })
    );
  }

  private getPlanFromToken(): PlanId | null {
    const token = this.getToken();
    if (!token) return null;
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return planFromRaw(payload?.plan);
    } catch {
      return null;
    }
  }

  private persistLoginResponse(response: LoginResponse) {
    if (!response?.token) return;

    const plan = normalizePlan(response.plan);
    localStorage.setItem('auth_token', response.token);
    this.setStoredUserInfo(response.user);
    this.setSession({
      userId: response.userId || response.user?.userId || response.user?.id || '',
      token: response.token,
      plan,
      planName: getPlanDisplayName(plan),
      planSelected: response.planSelected ?? response.user?.planSelected ?? false,
      maxCatalogs: response.maxCatalogs ?? response.user?.maxCatalogCount ?? 3,
      expiresAt: response.expiresAt ?? response.user?.planExpiresAt ?? null
    });
  }

  private applyUserSessionFromUser(user: AuthUserInfo, fallbackPlan: PlanId) {
    this.setStoredUserInfo(user);
    const token = this.getToken();
    if (!token) return;

    const plan = normalizePlan(user.subscriptionPlan ?? fallbackPlan);
    this.setSession({
      userId: user.userId || user.id,
      token,
      plan,
      planName: getPlanDisplayName(plan),
      planSelected: !!user.planSelected,
      maxCatalogs: Number(user.maxCatalogCount || 0) > 0 ? Number(user.maxCatalogCount) : 3,
      expiresAt: user.planExpiresAt ?? null
    });
  }
}
