import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../core/services/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  templateUrl: './register.html',
  styleUrl: './register.css'
})
export class RegisterComponent {
  private authService = inject(AuthService);
  private router = inject(Router);

  // Form Verileri
  fullName = '';
  email = '';
  password = '';
  termsAccepted = false;
  
  showPassword = false;
  isLoading = false;
  errorMessage = '';

  get isDuplicateEmailError(): boolean {
    return this.errorMessage.toLowerCase().includes('zaten kayıtlı');
  }

  togglePassword() {
    this.showPassword = !this.showPassword;
  }

  onRegister() {
    // Basit Validasyonlar
    if (!this.fullName || !this.email || !this.password) {
      this.errorMessage = 'Lütfen tüm alanları doldurun.';
      return;
    }

    if (!this.termsAccepted) {
      this.errorMessage = 'Lütfen kullanım koşullarını kabul edin.';
      return;
    }

    if (this.password.trim().length < 8) {
      this.errorMessage = 'Şifre en az 8 karakter olmalıdır.';
      return;
    }

    this.isLoading = true;
    this.errorMessage = '';

    // Servisi Çağır
    this.authService.register({
      fullName: this.fullName,
      email: this.email,
      password: this.password
    }).subscribe({
      next: () => {
        alert('Kayıt başarılı! Giriş sayfasına yönlendiriliyorsunuz.');
        this.router.navigate(['/login']);
      },
      error: (err: HttpErrorResponse) => {
        this.isLoading = false;
        this.errorMessage = this.resolveRegisterErrorMessage(err);
      }
    });
  }

  private resolveRegisterErrorMessage(err: HttpErrorResponse): string {
    const payload = err?.error;

    if (typeof payload === 'string' && payload.trim()) {
      return payload.trim();
    }

    if (payload?.message && typeof payload.message === 'string') {
      return payload.message.trim();
    }

    if (payload?.error && typeof payload.error === 'string') {
      return payload.error.trim();
    }

    if (payload?.errors && typeof payload.errors === 'object') {
      const values = Object.values(payload.errors).flat().filter((x): x is string => typeof x === 'string' && x.trim().length > 0);
      if (values.length > 0) {
        return values[0];
      }
    }

    if (err.status === 400) {
      return 'Kayıt bilgileri kabul edilmedi. E-posta adresini ve şifreyi kontrol edin.';
    }

    if (err.status === 0) {
      return 'Sunucuya ulasilamadi. API veya veritabani ayakta mi kontrol edin.';
    }

    return 'Kayıt sırasında bir hata oluştu!';
  }
}
