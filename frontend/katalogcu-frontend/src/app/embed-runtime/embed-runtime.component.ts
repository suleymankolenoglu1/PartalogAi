import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { CatalogService } from '../core/services/catalog.service';

@Component({
  selector: 'app-embed-runtime',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './embed-runtime.component.html',
  styleUrl: './embed-runtime.component.css'
})
export class EmbedRuntimeComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private catalogService = inject(CatalogService);

  isLoading = true;
  errorMessage: string | null = null;

  ngOnInit(): void {
    const embedKey = String(this.route.snapshot.paramMap.get('embedKey') ?? '').trim();
    if (!embedKey) {
      this.isLoading = false;
      this.errorMessage = 'Embed kimliği bulunamadı.';
      return;
    }

    this.catalogService.getEmbedTargetConfig(embedKey).subscribe({
      next: (config) => {
        this.router.navigateByUrl(config.runtimePath, { replaceUrl: true });
      },
      error: (err) => {
        this.isLoading = false;
        this.errorMessage = typeof err?.error === 'string'
          ? err.error
          : (err?.error?.message ?? 'Embed hedefi yüklenemedi.');
      }
    });
  }
}
