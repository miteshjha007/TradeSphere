import { Component, OnInit } from '@angular/core';
import { TradingOverview, TradingService } from '../../services/trading.service';

@Component({
  selector: 'app-analytics',
  templateUrl: './analytics.component.html',
  styleUrls: []
})
export class AnalyticsComponent implements OnInit {
  private readonly reportColumnStorageKey = 'tradesphere.report.visibleColumns';

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
  tradePage = 1;
  tradePageSize = 10;
  readonly tradePageSizeOptions = [10, 25, 50, 100];
  readonly tradeColumns = [
    { key: 'time', label: 'Time' },
    { key: 'strategy', label: 'Strategy' },
    { key: 'activity', label: 'Activity' },
    { key: 'ticket', label: 'Ticket' },
    { key: 'source', label: 'Source' },
    { key: 'account', label: 'Account' },
    { key: 'symbol', label: 'Symbol' },
    { key: 'side', label: 'Side' },
    { key: 'qty', label: 'Qty' },
    { key: 'price', label: 'Price' },
    { key: 'pnl', label: 'P/L' },
    { key: 'status', label: 'Status' },
    { key: 'reason', label: 'Reason' }
  ];
  visibleTradeColumns: Record<string, boolean> = this.tradeColumns.reduce((columns, column) => {
    columns[column.key] = true;
    return columns;
  }, {} as Record<string, boolean>);

  constructor(private tradingService: TradingService) { }

  ngOnInit(): void {
    this.loadVisibleColumns();
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
    if (status === 'Modified') return 'bg-blue-50 text-blue-700 border-blue-200';
    if (status === 'Pending' || status === 'Reconciled') return 'bg-amber-50 text-amber-700 border-amber-200';
    if (status === 'Failed') return 'bg-red-50 text-red-700 border-red-200';
    return 'bg-gray-50 text-gray-700 border-gray-200';
  }

  activityClass(activity: string): string {
    if (activity === 'Entry') return 'bg-emerald-50 text-emerald-700 border-emerald-200';
    if (activity === 'Risk Update') return 'bg-blue-50 text-blue-700 border-blue-200';
    if (activity === 'Exit Signal' || activity === 'Closed') return 'bg-purple-50 text-purple-700 border-purple-200';
    if (activity === 'Failed') return 'bg-red-50 text-red-700 border-red-200';
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

  get totalTradePages(): number {
    return Math.max(1, Math.ceil(this.filteredTrades.length / this.tradePageSize));
  }

  get currentTradePage(): number {
    return Math.min(this.tradePage, this.totalTradePages);
  }

  get pagedTrades() {
    const start = (this.currentTradePage - 1) * this.tradePageSize;
    return this.filteredTrades.slice(start, start + this.tradePageSize);
  }

  get tradePageStart(): number {
    return this.filteredTrades.length === 0 ? 0 : ((this.currentTradePage - 1) * this.tradePageSize) + 1;
  }

  get tradePageEnd(): number {
    return Math.min(this.currentTradePage * this.tradePageSize, this.filteredTrades.length);
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
    this.resetTradePage();
  }

  resetTradePage(): void {
    this.tradePage = 1;
  }

  setTradePage(page: number): void {
    this.tradePage = Math.min(Math.max(page, 1), this.totalTradePages);
  }

  get visibleTradeColumnCount(): number {
    return this.tradeColumns.filter(column => this.isTradeColumnVisible(column.key)).length;
  }

  isTradeColumnVisible(key: string): boolean {
    return this.visibleTradeColumns[key] !== false;
  }

  toggleTradeColumn(key: string): void {
    const visibleCount = this.visibleTradeColumnCount;
    if (this.visibleTradeColumns[key] !== false && visibleCount <= 1) {
      return;
    }

    this.visibleTradeColumns = {
      ...this.visibleTradeColumns,
      [key]: this.visibleTradeColumns[key] === false
    };
    this.saveVisibleColumns();
  }

  showAllTradeColumns(): void {
    this.visibleTradeColumns = this.tradeColumns.reduce((columns, column) => {
      columns[column.key] = true;
      return columns;
    }, {} as Record<string, boolean>);
    this.saveVisibleColumns();
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

  private loadVisibleColumns(): void {
    const saved = localStorage.getItem(this.reportColumnStorageKey);
    if (!saved) {
      return;
    }

    try {
      const parsed = JSON.parse(saved) as Record<string, boolean>;
      this.visibleTradeColumns = this.tradeColumns.reduce((columns, column) => {
        columns[column.key] = parsed[column.key] !== false;
        return columns;
      }, {} as Record<string, boolean>);
    } catch {
      localStorage.removeItem(this.reportColumnStorageKey);
    }
  }

  private saveVisibleColumns(): void {
    localStorage.setItem(this.reportColumnStorageKey, JSON.stringify(this.visibleTradeColumns));
  }
}
