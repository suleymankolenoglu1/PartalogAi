import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { CustomerService } from '../core/services/customer.service';
import { formatPublicAuthError } from '../public-view/public-auth-identity';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './home.component.html',
  styleUrl: './home.component.css'
})
export class HomeComponent {
  loginForm = {
    identifier: '',
    password: ''
  };
  portalError: string | null = null;
  isLoggingIn = false;

  constructor(
    private router: Router,
    private customerService: CustomerService
  ) {}

  loginPortal() {
    const identifier = this.loginForm.identifier.trim();
    if (!identifier || !this.loginForm.password) {
      this.portalError = 'Telefon/e-posta ve şifre zorunlu.';
      return;
    }

    this.portalError = null;
    this.isLoggingIn = true;

    this.customerService.loginPortalHome({
      identifier,
      password: this.loginForm.password
    }).subscribe({
      next: response => {
        this.isLoggingIn = false;
        this.loginForm.password = '';

        if (!response.publicToken || !response.sessionToken) {
          this.portalError = 'Portal oturumu oluşturulamadı. Lütfen tekrar deneyin.';
          return;
        }

        localStorage.setItem(`public_customer_session_${response.publicToken}`, response.sessionToken);
        this.router.navigate(['/p', response.publicToken]);
      },
      error: error => {
        this.isLoggingIn = false;
        this.portalError = formatPublicAuthError(error, 'Giriş yapılamadı.');
      }
    });
  }
}
