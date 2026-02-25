import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { tap } from 'rxjs/operators';
import { Router } from '@angular/router';

export interface AuthUserInfo {
  id: string;
  userId?: string;
  firstName: string;
  lastName: string;
  email: string;
  companyName?: string | null;
  phoneNumber?: string | null;
  role: string;
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

  // Giriş Yap
  login(credentials: { email: string; password: string }) {
    return this.http.post<{ token: string; user: AuthUserInfo }>(`${this.apiUrl}/auth/login`, credentials).pipe(
      tap(response => {
        if (response.token) {
          // Token'ı tarayıcıya kaydet
          localStorage.setItem('auth_token', response.token);
          // Kullanıcı bilgisini kaydet (Opsiyonel)
          this.setStoredUserInfo(response.user);
        }
      })
    );
  }

  register(userData: { fullName: string; email: string; password: string }) {
    return this.http.post<any>(`${this.apiUrl}/auth/register`, userData);
  }

  // Çıkış Yap
  logout() {
    localStorage.removeItem('auth_token');
    localStorage.removeItem('user_info');
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

  // ✅ UserId'yi getir (user_info yoksa token'dan al)
  getUserId(): string | null {
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

  getMe() {
    return this.http.get<AuthUserInfo>(`${this.apiUrl}/auth/me`);
  }

  updateMe(payload: UpdateProfileRequest) {
    return this.http.put<AuthUserInfo>(`${this.apiUrl}/auth/me`, payload).pipe(
      tap((user) => this.setStoredUserInfo(user))
    );
  }
}
