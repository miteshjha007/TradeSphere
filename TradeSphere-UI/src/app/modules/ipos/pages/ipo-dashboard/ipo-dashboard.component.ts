import { Component, OnInit } from '@angular/core';
import { IpoDashboard, IpoItem, IpoService } from '../../services/ipo.service';

@Component({
  selector: 'app-ipo-dashboard',
  templateUrl: './ipo-dashboard.component.html',
  styleUrls: []
})
export class IpoDashboardComponent implements OnInit {
  dashboard: IpoDashboard | null = null;
  selectedTab: 'current' | 'upcoming' | 'filings' = 'upcoming';
  isLoading = true;
  errorMessage: string | null = null;

  constructor(private ipoService: IpoService) { }

  ngOnInit(): void {
    this.loadDashboard();
  }

  loadDashboard(): void {
    this.isLoading = true;
    this.errorMessage = null;
    this.ipoService.getDashboard().subscribe({
      next: data => {
        this.dashboard = data;
        this.isLoading = false;
      },
      error: err => {
        this.errorMessage = err.error?.message || 'Could not load IPO dashboard.';
        this.isLoading = false;
      }
    });
  }

  listForTab(): IpoItem[] {
    if (!this.dashboard) {
      return [];
    }

    if (this.selectedTab === 'current') {
      return this.dashboard.topCurrent;
    }

    if (this.selectedTab === 'filings') {
      return this.dashboard.recentFilings;
    }

    return this.dashboard.topUpcoming;
  }

  scoreClass(score: number): string {
    if (score >= 75) {
      return 'text-emerald-700 bg-emerald-50 border-emerald-100';
    }

    if (score >= 55) {
      return 'text-blue-700 bg-blue-50 border-blue-100';
    }

    return 'text-amber-700 bg-amber-50 border-amber-100';
  }

  formatDate(date?: string): string {
    if (!date) {
      return '-';
    }

    return new Date(date).toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' });
  }

  openDocument(item: IpoItem): void {
    if (item.documentUrl) {
      window.open(item.documentUrl, '_blank', 'noopener');
    }
  }
}
