import { Component, OnDestroy, OnInit } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter, Subscription } from 'rxjs';
import { StockPick, StockPickDashboard, StockPicksService } from '../../services/stock-picks.service';

@Component({
  selector: 'app-stock-picks-dashboard',
  templateUrl: './stock-picks-dashboard.component.html',
  styleUrls: []
})
export class StockPicksDashboardComponent implements OnInit, OnDestroy {
  dashboard: StockPickDashboard | null = null;
  isLoading = true;
  errorMessage: string | null = null;
  mode: 'intraday' | 'long-term' = 'intraday';
  private routerSubscription?: Subscription;

  constructor(private router: Router, private stockPicksService: StockPicksService) { }

  ngOnInit(): void {
    this.updateModeFromUrl();
    this.loadPicks();
    this.routerSubscription = this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe(() => {
        const previousMode = this.mode;
        this.updateModeFromUrl();
        if (previousMode !== this.mode) {
          this.loadPicks();
        }
      });
  }

  ngOnDestroy(): void {
    this.routerSubscription?.unsubscribe();
  }

  loadPicks(): void {
    this.isLoading = true;
    this.errorMessage = null;
    this.dashboard = null;

    const request = this.mode === 'intraday'
      ? this.stockPicksService.getIntradayPicks()
      : this.stockPicksService.getLongTermPicks();

    request.subscribe({
      next: data => {
        this.dashboard = data;
        this.isLoading = false;
      },
      error: err => {
        this.errorMessage = err.error?.message || 'Could not load stock picks.';
        this.isLoading = false;
      }
    });
  }

  pageTitle(): string {
    return this.mode === 'intraday' ? 'Intraday Picks' : 'Long Term Picks';
  }

  pageSubtitle(): string {
    return this.mode === 'intraday'
      ? 'Daily top 10 Indian stock watchlist for intraday setups.'
      : 'Top 5 Indian stock watchlist for long-term accumulation research.';
  }

  metricLabel(): string {
    return this.mode === 'intraday' ? 'Volume Ratio' : 'Trend vs SMA200';
  }

  metricValue(pick: StockPick): string {
    return this.mode === 'intraday'
      ? `${pick.volumeRatio.toFixed(2)}x`
      : `${pick.trendStrengthPercent.toFixed(2)}%`;
  }

  scoreClass(score: number): string {
    if (score >= 75) {
      return 'bg-emerald-50 text-emerald-700 border-emerald-100';
    }

    if (score >= 55) {
      return 'bg-blue-50 text-blue-700 border-blue-100';
    }

    return 'bg-amber-50 text-amber-700 border-amber-100';
  }

  formatDate(date?: string): string {
    if (!date) {
      return '-';
    }

    return new Date(date).toLocaleString('en-IN', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
  }

  private updateModeFromUrl(): void {
    this.mode = this.router.url.includes('long-term') ? 'long-term' : 'intraday';
  }
}
