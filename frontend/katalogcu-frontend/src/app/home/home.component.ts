import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './home.component.html',
  styleUrl: './home.component.css'
})
export class HomeComponent {
  portalInput = '';
  portalError: string | null = null;

  constructor(private router: Router) {}

  openPortal() {
    const token = this.extractToken(this.portalInput);
    if (!token) {
      this.portalError = 'Lütfen geçerli davet linkini veya token bilgisini girin.';
      return;
    }

    this.portalError = null;
    this.router.navigate(['/p', token]);
  }

  private extractToken(raw: string): string | null {
    const value = raw.trim();
    if (!value) return null;

    const pathMatch = value.match(/\/p\/([^/?#]+)/i);
    if (pathMatch?.[1]) return decodeURIComponent(pathMatch[1]);

    return /^[A-Za-z0-9._-]{12,}$/.test(value) ? value : null;
  }
}
