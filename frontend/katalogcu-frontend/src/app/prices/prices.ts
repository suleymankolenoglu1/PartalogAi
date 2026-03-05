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

  isCurrentPlan(plan: PlanId): boolean {
    return this.currentPlan === plan;
  }

  getPlanActionLabel(plan: PlanId): string {
    if (!this.isLoggedIn) return 'Giriş Yap ve Seç';
    if (this.isCurrentPlan(plan)) return 'Mevcut Plan';
    if (plan < this.currentPlan) return 'Bu Plana Düşür';
    return 'Bu Plana Yükselt';
  }

  selectPlan(plan: PlanId) {
    if (!this.isLoggedIn) {
      this.router.navigate(['/login']);
      return;
    }

    if (this.isCurrentPlan(plan)) return;

    if (plan < this.currentPlan) {
      const approve = confirm('Planı düşürmek istediğine emin misin? Bu planın dışındaki modüller kapanacak.');
      if (!approve) return;
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

  cancelPaidPlan() {
    if (!this.isLoggedIn || this.currentPlan === 1 || this.isSubmitting) return;

    const approve = confirm('Ücretli planı iptal edip Katalog paketine dönmek istiyor musun?');
    if (!approve) return;

    this.isSubmitting = true;
    this.submitError = null;
    this.submitSuccess = null;

    this.authService.cancelPlan().subscribe({
      next: () => {
        this.isSubmitting = false;
        this.submitSuccess = 'Ücretli plan iptal edildi. Katalog paketine geçildi.';
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.isSubmitting = false;
        this.submitError = err?.error?.message || 'Plan iptali sırasında hata oluştu.';
      }
    });
  }
}
