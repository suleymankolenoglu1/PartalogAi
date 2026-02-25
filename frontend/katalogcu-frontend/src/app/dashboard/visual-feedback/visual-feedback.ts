import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

interface VisualFeedback {
  id: string;
  partCode: string;
  partName: string;
  machineBrand?: string;
  machineType?: string;
  note?: string;
  imageUrl?: string;
  createdAt: string;
  userId?: string;
}

@Component({
  selector: 'app-visual-feedback',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './visual-feedback.html',
  styleUrl: './visual-feedback.css'
})
export class VisualFeedbackComponent implements OnInit {
  private http = inject(HttpClient);

  feedbackList: VisualFeedback[] = [];
  filteredList: VisualFeedback[] = [];
  isLoading = true;
  searchQuery = '';
  errorMsg: string | null = null;

  private apiUrl = environment.apiUrl;

  ngOnInit() {
    this.loadFeedback();
  }

  loadFeedback() {
    this.isLoading = true;
    this.errorMsg = null;

    this.http.get<VisualFeedback[]>(`${this.apiUrl}/visual-feedback`).subscribe({
      next: (data) => {
        this.feedbackList = data;
        this.filteredList = data;
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Feedback listesi alınamadı:', err);
        this.errorMsg = 'Veriler yüklenirken hata oluştu.';
        this.isLoading = false;
      }
    });
  }

  onSearch(query: string) {
    this.searchQuery = query;
    if (!query.trim()) {
      this.filteredList = this.feedbackList;
      return;
    }
    const q = query.toLowerCase();
    this.filteredList = this.feedbackList.filter(f =>
      (f.partCode && f.partCode.toLowerCase().includes(q)) ||
      (f.partName && f.partName.toLowerCase().includes(q)) ||
      (f.machineBrand && f.machineBrand.toLowerCase().includes(q))
    );
  }
}
