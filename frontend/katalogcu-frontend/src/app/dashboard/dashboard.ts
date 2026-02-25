import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import {
  CatalogAiJobItem,
  CatalogAiJobSummary,
  CatalogService,
  DashboardStats
} from '../core/services/catalog.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css'
})
export class DashboardComponent implements OnInit, OnDestroy {
  
  private catalogService = inject(CatalogService);
  private router = inject(Router);
  private aiJobsPollTimer: ReturnType<typeof setInterval> | null = null;

  stats: (DashboardStats & { visualEmbeddingCount?: number }) | null = null;
  isLoading = true;
  aiJobsLoading = true;
  aiJobsError: string | null = null;
  aiJobsSummary: CatalogAiJobSummary = {
    total: 0,
    pending: 0,
    processing: 0,
    completed: 0,
    failed: 0
  };
  aiJobs: CatalogAiJobItem[] = [];

  ngOnInit() {
    this.loadStats();
    this.loadAiJobs();
    this.startAiJobsPolling();
  }

  ngOnDestroy() {
    if (this.aiJobsPollTimer) {
      clearInterval(this.aiJobsPollTimer);
      this.aiJobsPollTimer = null;
    }
  }

  loadStats() {
    this.isLoading = true;
    this.catalogService.getDashboardStats().subscribe({
      next: (data) => {
        this.stats = data;
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Dashboard verisi çekilemedi:', err);
        this.isLoading = false;
      }
    });
  }

  loadAiJobs(silent = false) {
    if (!silent) {
      this.aiJobsLoading = true;
    }

    this.catalogService.getCatalogAiJobs(8).subscribe({
      next: (res) => {
        this.aiJobsSummary = res.summary ?? this.aiJobsSummary;
        this.aiJobs = res.jobs ?? [];
        this.aiJobsError = null;
        this.aiJobsLoading = false;
      },
      error: (err) => {
        if (!silent) {
          this.aiJobsError = err?.error?.message || 'AI işlemleri alınamadı.';
          this.aiJobsLoading = false;
        }
      }
    });
  }

  startAiJobsPolling() {
    if (this.aiJobsPollTimer) {
      clearInterval(this.aiJobsPollTimer);
    }

    this.aiJobsPollTimer = setInterval(() => {
      this.loadAiJobs(true);
    }, 10000);
  }

  // Statüye göre renk sınıfı döndüren yardımcı fonksiyon
  getStatusClass(status: string): string {
    switch (status) {
      case 'Published': return 'badge-ok';    // Yeşil
      case 'Processing': return 'badge-wait'; // Turuncu/Sarı
      case 'Draft': return 'badge-gray';      // Gri
      default: return 'badge-gray';
    }
  }

  // Statü metnini Türkçeye çeviren yardımcı fonksiyon
  getStatusLabel(status: string): string {
    switch (status) {
      case 'Published': return 'YAYINDA';
      case 'Processing': return 'İŞLENİYOR';
      case 'Draft': return 'TASLAK';
      case 'Pending': return 'ONAY BEKLİYOR';
      default: return status;
    }
  }

  getAiJobStatusClass(status: string): string {
    const s = status?.toLowerCase();
    if (s === 'pending') return 'job-chip pending';
    if (s === 'processing') return 'job-chip processing';
    if (s === 'completed') return 'job-chip completed';
    if (s === 'failed') return 'job-chip failed';
    return 'job-chip';
  }

  getAiJobStatusLabel(status: string): string {
    const s = status?.toLowerCase();
    if (s === 'pending') return 'Beklemede';
    if (s === 'processing') return 'İşleniyor';
    if (s === 'completed') return 'Tamamlandı';
    if (s === 'failed') return 'Başarısız';
    return status;
  }

  openAiJobInCatalogs(job: CatalogAiJobItem, event?: Event) {
    event?.stopPropagation();
    this.router.navigate(['/dashboard/catalogs'], {
      queryParams: {
        jobId: job.jobId,
        catalogId: job.catalogId
      }
    });
  }

  goToUpload() {
    this.router.navigate(['/dashboard/catalogs']);
  }
}
