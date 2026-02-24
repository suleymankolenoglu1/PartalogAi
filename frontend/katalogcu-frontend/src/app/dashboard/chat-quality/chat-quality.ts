import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment.development';

type HelpfulFilter = 'all' | 'helpful' | 'unhelpful';

interface ChatFeedbackItem {
  id: string;
  createdAt: string;
  helpful: boolean;
  reason: string | null;
  userQuery: string | null;
  replySuggestion: string | null;
  sourceCodes: string[];
  userId: string | null;
}

interface ChatFeedbackApiResponse {
  items: any[];
  total: number;
  page: number;
  pageSize: number;
}

interface TrendPoint {
  dayKey: string;
  label: string;
  total: number;
  helpful: number;
  unhelpful: number;
}

@Component({
  selector: 'app-chat-quality',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './chat-quality.html',
  styleUrl: './chat-quality.css'
})
export class ChatQualityComponent implements OnInit {
  private http = inject(HttpClient);

  isLoading = true;
  errorMsg: string | null = null;

  searchQuery = '';
  helpfulFilter: HelpfulFilter = 'all';
  dateFrom = '';
  dateTo = '';

  allItems: ChatFeedbackItem[] = [];
  filteredItems: ChatFeedbackItem[] = [];

  totalCount = 0;
  shownCount = 0;
  helpfulCount = 0;
  unhelpfulCount = 0;
  noSourceCount = 0;
  avgSourceCount = 0;
  topReasons: Array<{ reason: string; count: number }> = [];
  trendPoints: TrendPoint[] = [];
  trendMax = 0;

  private readonly apiUrl = `${environment.apiUrl}/chatfeedback`;

  ngOnInit(): void {
    this.setLastDays(14);
    this.loadFeedback();
  }

  loadFeedback(): void {
    this.isLoading = true;
    this.errorMsg = null;

    this.http.get<ChatFeedbackApiResponse>(`${this.apiUrl}?page=1&pageSize=500`).subscribe({
      next: (res) => {
        const items = Array.isArray(res?.items) ? res.items : [];
        this.allItems = items.map((x) => this.mapItem(x));
        this.totalCount = this.allItems.length;
        this.applyFilters();
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Chat feedback okunamadı:', err);
        this.errorMsg = 'Chat kalite verileri yüklenirken hata oluştu.';
        this.isLoading = false;
      }
    });
  }

  applyFilters(): void {
    const q = this.searchQuery.trim().toLowerCase();
    const fromDate = this.parseStartOfDay(this.dateFrom);
    const toDate = this.parseEndOfDay(this.dateTo);

    this.filteredItems = this.allItems.filter((item) => {
      if (this.helpfulFilter === 'helpful' && !item.helpful) return false;
      if (this.helpfulFilter === 'unhelpful' && item.helpful) return false;

      const d = new Date(item.createdAt);
      if (fromDate && d < fromDate) return false;
      if (toDate && d > toDate) return false;

      if (!q) return true;

      return [
        item.userQuery || '',
        item.replySuggestion || '',
        item.reason || '',
        item.sourceCodes.join(' ')
      ].some((txt) => txt.toLowerCase().includes(q));
    });

    this.recomputeFilteredStats();
    this.recomputeTrend();
  }

  onSearchChange(value: string): void {
    this.searchQuery = value;
    this.applyFilters();
  }

  onHelpfulFilterChange(value: HelpfulFilter): void {
    this.helpfulFilter = value;
    this.applyFilters();
  }

  onDateFromChange(value: string): void {
    this.dateFrom = value;
    this.applyFilters();
  }

  onDateToChange(value: string): void {
    this.dateTo = value;
    this.applyFilters();
  }

  setLastDays(days: number): void {
    const end = new Date();
    const start = new Date();
    start.setDate(end.getDate() - Math.max(0, days - 1));
    this.dateFrom = this.toInputDate(start);
    this.dateTo = this.toInputDate(end);
    this.applyFilters();
  }

  helpfulRate(): string {
    if (!this.shownCount) return '%0';
    return `%${((this.helpfulCount / this.shownCount) * 100).toFixed(1)}`;
  }

  unhelpfulRate(): string {
    if (!this.shownCount) return '%0';
    return `%${((this.unhelpfulCount / this.shownCount) * 100).toFixed(1)}`;
  }

  trendHeightPercent(total: number): number {
    if (!this.trendMax) return 0;
    return (total / this.trendMax) * 100;
  }

  private mapItem(raw: any): ChatFeedbackItem {
    const sourceCodes = this.toStringArray(raw?.SourceCodes ?? raw?.sourceCodes);
    return {
      id: this.pickString(raw, ['Id', 'id']) ?? '-',
      createdAt: this.pickString(raw, ['CreatedAt', 'createdAt']) ?? '',
      helpful: Boolean(raw?.Helpful ?? raw?.helpful),
      reason: this.pickString(raw, ['Reason', 'reason']),
      userQuery: this.pickString(raw, ['UserQuery', 'userQuery']),
      replySuggestion: this.pickString(raw, ['ReplySuggestion', 'replySuggestion']),
      sourceCodes,
      userId: this.pickString(raw, ['UserId', 'userId']),
    };
  }

  private recomputeFilteredStats(): void {
    this.shownCount = this.filteredItems.length;
    this.helpfulCount = this.filteredItems.filter(x => x.helpful).length;
    this.unhelpfulCount = this.shownCount - this.helpfulCount;
    this.noSourceCount = this.filteredItems.filter(x => x.sourceCodes.length === 0).length;
    const totalSources = this.filteredItems.reduce((sum, x) => sum + x.sourceCodes.length, 0);
    this.avgSourceCount = this.shownCount ? totalSources / this.shownCount : 0;

    const unhelpfulReasons = this.filteredItems
      .filter(x => !x.helpful)
      .map(x => (x.reason || '').trim())
      .filter(x => x.length > 0);

    const map = new Map<string, number>();
    for (const reason of unhelpfulReasons) {
      map.set(reason, (map.get(reason) || 0) + 1);
    }
    this.topReasons = [...map.entries()]
      .map(([reason, count]) => ({ reason, count }))
      .sort((a, b) => b.count - a.count)
      .slice(0, 5);
  }

  private recomputeTrend(): void {
    const dayMap = new Map<string, { helpful: number; unhelpful: number }>();
    for (const item of this.filteredItems) {
      const d = new Date(item.createdAt);
      if (Number.isNaN(d.getTime())) continue;
      const key = this.toInputDate(d);
      const row = dayMap.get(key) ?? { helpful: 0, unhelpful: 0 };
      if (item.helpful) row.helpful += 1;
      else row.unhelpful += 1;
      dayMap.set(key, row);
    }

    const start = this.parseStartOfDay(this.dateFrom) ?? this.daysAgoStart(13);
    const end = this.parseEndOfDay(this.dateTo) ?? this.parseEndOfDay(this.toInputDate(new Date()))!;
    const safeStart = start <= end ? start : end;
    const safeEnd = end >= start ? end : start;

    const points: TrendPoint[] = [];
    const cursor = new Date(safeStart);
    while (cursor <= safeEnd) {
      const key = this.toInputDate(cursor);
      const agg = dayMap.get(key) ?? { helpful: 0, unhelpful: 0 };
      const total = agg.helpful + agg.unhelpful;
      points.push({
        dayKey: key,
        label: key.slice(5), // MM-DD
        total,
        helpful: agg.helpful,
        unhelpful: agg.unhelpful,
      });
      cursor.setDate(cursor.getDate() + 1);
    }

    this.trendPoints = points;
    this.trendMax = Math.max(0, ...points.map(p => p.total));
  }

  private pickString(raw: any, keys: string[]): string | null {
    for (const key of keys) {
      const v = raw?.[key];
      if (typeof v === 'string' && v.trim().length > 0) return v.trim();
    }
    return null;
  }

  private toStringArray(input: any): string[] {
    if (!Array.isArray(input)) return [];
    return input
      .map((x: any) => (typeof x === 'string' ? x.trim() : ''))
      .filter((x: string) => x.length > 0);
  }

  private parseStartOfDay(value: string): Date | null {
    if (!value) return null;
    const d = new Date(`${value}T00:00:00`);
    return Number.isNaN(d.getTime()) ? null : d;
  }

  private parseEndOfDay(value: string): Date | null {
    if (!value) return null;
    const d = new Date(`${value}T23:59:59.999`);
    return Number.isNaN(d.getTime()) ? null : d;
  }

  private toInputDate(d: Date): string {
    const year = d.getFullYear();
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private daysAgoStart(days: number): Date {
    const d = new Date();
    d.setDate(d.getDate() - days);
    d.setHours(0, 0, 0, 0);
    return d;
  }
}
