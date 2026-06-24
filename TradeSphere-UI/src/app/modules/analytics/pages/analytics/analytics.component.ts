import { Component, OnInit } from '@angular/core';
import { TradingOverview, TradingService } from '../../services/trading.service';

@Component({
  selector: 'app-analytics',
  templateUrl: './analytics.component.html',
  styleUrls: []
})
export class AnalyticsComponent implements OnInit {
  data: TradingOverview = { trades: [], positions: [] };
  isLoading = true;
  errorMessage = '';
  filters = {
    strategy: '',
    source: '',
    account: '',
    symbol: '',
    startDate: '',
    endDate: ''
  };

  constructor(private tradingService: TradingService) { }

  ngOnInit(): void {
    this.loadTradingOverview();
  }

  loadTradingOverview(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.tradingService.getOverview().subscribe({
      next: (res) => {
        this.data = res;
        this.isLoading = false;
      },
      error: (err) => {
        this.errorMessage = err.error?.message || 'Failed to load trading activity.';
        this.isLoading = false;
      }
    });
  }

  get failedTrades(): number {
    return this.filteredTrades.filter(t => t.status === 'Failed').length;
  }

  get realizedPnl(): number {
    return this.filteredTrades.reduce((sum, trade) => sum + (trade.pnl || 0), 0);
  }

  get unrealizedPnl(): number {
    return this.data.positions.reduce((sum, position) => sum + (position.unrealizedPnl || 0), 0);
  }

  statusClass(status: string): string {
    if (status === 'Filled' || status === 'Open' || status === 'Closed') return 'bg-green-50 text-green-700 border-green-200';
    if (status === 'Pending' || status === 'Reconciled') return 'bg-amber-50 text-amber-700 border-amber-200';
    if (status === 'Failed') return 'bg-red-50 text-red-700 border-red-200';
    return 'bg-gray-50 text-gray-700 border-gray-200';
  }

  get filteredTrades() {
    return this.data.trades.filter(trade => {
      const createdAt = new Date(trade.createdAt);
      const startDate = this.filters.startDate ? new Date(this.filters.startDate) : null;
      const endDate = this.filters.endDate ? new Date(this.filters.endDate) : null;

      if (endDate) {
        endDate.setHours(23, 59, 59, 999);
      }

      return this.matchesFilter(trade.strategyName, this.filters.strategy) &&
        this.matchesFilter(trade.executionProvider || trade.exchangeName, this.filters.source) &&
        this.matchesFilter(trade.executionAccount || trade.exchangeName, this.filters.account) &&
        this.matchesFilter(trade.symbol, this.filters.symbol) &&
        (!startDate || createdAt >= startDate) &&
        (!endDate || createdAt <= endDate);
    });
  }

  get strategyOptions(): string[] {
    return this.uniqueOptions(this.data.trades.map(t => t.strategyName));
  }

  get sourceOptions(): string[] {
    return this.uniqueOptions(this.data.trades.map(t => t.executionProvider || t.exchangeName));
  }

  get accountOptions(): string[] {
    return this.uniqueOptions(this.data.trades.map(t => t.executionAccount || t.exchangeName));
  }

  get symbolOptions(): string[] {
    return this.uniqueOptions(this.data.trades.map(t => t.symbol));
  }

  clearFilters(): void {
    this.filters = {
      strategy: '',
      source: '',
      account: '',
      symbol: '',
      startDate: '',
      endDate: ''
    };
  }

  deleteAllTrades(): void {
    if (!confirm('Delete all stored report trade records? This will not close broker positions.')) {
      return;
    }

    this.tradingService.deleteAllTrades().subscribe({
      next: () => this.loadTradingOverview(),
      error: (err) => {
        this.errorMessage = err.error?.message || 'Failed to delete trade records.';
      }
    });
  }

  private matchesFilter(value: string | null | undefined, filter: string): boolean {
    return !filter || (value || '') === filter;
  }

  private uniqueOptions(values: Array<string | null | undefined>): string[] {
    return Array.from(new Set(values.filter((value): value is string => !!value))).sort();
  }
}
