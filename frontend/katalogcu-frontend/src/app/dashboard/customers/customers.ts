import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Customer, CustomerService } from '../../core/services/customer.service';

@Component({
  selector: 'app-customers',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './customers.html',
  styleUrl: './customers.css'
})
export class CustomersComponent implements OnInit {
  private customerService = inject(CustomerService);

  isLoading = true;
  errorMsg: string | null = null;
  customers: Customer[] = [];
  filteredCustomers: Customer[] = [];
  searchQuery = '';
  statusFilter: 'all' | 'active' | 'inactive' = 'all';

  ngOnInit() {
    this.loadCustomers();
  }

  loadCustomers() {
    this.isLoading = true;
    this.errorMsg = null;

    this.customerService.getCustomers().subscribe({
      next: (rows) => {
        this.customers = rows || [];
        this.applyFilters();
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Müşteri listesi alınamadı:', err);
        this.errorMsg = 'Müşteri verisi yüklenemedi.';
        this.customers = [];
        this.filteredCustomers = [];
        this.isLoading = false;
      }
    });
  }

  onSearch(query: string) {
    this.searchQuery = query;
    this.applyFilters();
  }

  onStatusFilterChange(value: string) {
    if (value === 'active' || value === 'inactive' || value === 'all') {
      this.statusFilter = value;
      this.applyFilters();
    }
  }

  applyFilters() {
    const q = this.searchQuery.trim().toLowerCase();

    this.filteredCustomers = this.customers.filter(c => {
      const statusOk = this.statusFilter === 'all' || c.status === this.statusFilter;
      if (!statusOk) return false;
      if (!q) return true;

      return (
        (c.name || '').toLowerCase().includes(q) ||
        (c.company || '').toLowerCase().includes(q) ||
        (c.email || '').toLowerCase().includes(q) ||
        (c.phone || '').toLowerCase().includes(q)
      );
    });
  }

  getStatusBadge(status: string) {
    switch (status) {
      case 'active':
        return 'bg-green-100 text-green-800 dark:bg-green-500/20 dark:text-green-400';
      case 'inactive':
        return 'bg-gray-100 text-gray-800 dark:bg-gray-500/20 dark:text-gray-400';
      default:
        return 'bg-gray-100 text-gray-800 dark:bg-gray-500/20 dark:text-gray-400';
    }
  }

  formatDate(date: string | null | undefined): string {
    if (!date) return '-';
    const parsed = new Date(date);
    if (Number.isNaN(parsed.getTime())) return '-';
    return parsed.toLocaleDateString('tr-TR');
  }
}
