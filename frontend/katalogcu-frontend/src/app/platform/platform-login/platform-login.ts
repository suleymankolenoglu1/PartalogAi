import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-platform-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './platform-login.html',
  styleUrl: './platform-login.css'
})
export class PlatformLoginComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  email = '';
  password = '';
  showPassword = false;
  isLoading = false;
  errorMessage = '';

  togglePassword() {
    this.showPassword = !this.showPassword;
  }

  onLogin() {
    if (!this.email || !this.password) {
      this.errorMessage = 'E-posta ve şifre zorunlu.';
      return;
    }

    this.errorMessage = '';
    this.isLoading = true;

    this.authService.platformLogin({ email: this.email, password: this.password }).subscribe({
      next: () => {
        this.isLoading = false;
        this.router.navigate(['/platform']);
      },
      error: (err) => {
        this.isLoading = false;
        const apiMessage = err?.error?.message;
        const status = Number(err?.status ?? 0);
        if (apiMessage) {
          this.errorMessage = apiMessage;
          return;
        }

        if (status === 404) {
          this.errorMessage = 'API endpoint bulunamadı (404). Backend’i yeniden başlat.';
          return;
        }

        this.errorMessage = `Platform girişinde hata oluştu${status ? ` (HTTP ${status})` : ''}.`;
      }
    });
  }
}
