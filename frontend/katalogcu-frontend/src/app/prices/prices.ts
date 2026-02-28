import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { AuthService } from '../core/services/auth.service';
import { PlanId } from '../core/models/plan.model';

@Component({
  selector: 'app-prices',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './prices.html',
  styleUrl: './prices.css'
})
export class PricesComponent {
  private authService = inject(AuthService);
  private router = inject(Router);

  isSubmitting = false;
  submitError: string | null = null;
  submitSuccess: string | null = null;

  get isLoggedIn(): boolean {
    return this.authService.isLoggedIn();
  }

  get currentPlan(): PlanId {
    return this.authService.getCurrentPlan();
  }

  get planSelected(): boolean {
    return this.authService.isPlanSelected();
  }

  selectPlan(plan: PlanId) {
    if (!this.isLoggedIn) {
      this.router.navigate(['/login']);
      return;
    }

    this.isSubmitting = true;
    this.submitError = null;
    this.submitSuccess = null;

    this.authService.selectPlan(plan).subscribe({
      next: () => {
        this.isSubmitting = false;
        this.submitSuccess = 'Planınız güncellendi.';
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.isSubmitting = false;
        this.submitError = err?.error?.message || 'Plan seçimi sırasında hata oluştu.';
      }
    });
  }
}
