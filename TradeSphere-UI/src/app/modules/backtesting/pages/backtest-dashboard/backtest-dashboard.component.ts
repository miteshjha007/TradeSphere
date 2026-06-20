import { Component, OnInit } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { BacktestService } from '../../services/backtest.service';
import { Backtest } from '../../models/backtest.model';
import { RunBacktestDialogComponent } from '../../components/run-backtest-dialog/run-backtest-dialog.component';
import { BacktestResultDialogComponent } from '../../components/backtest-result-dialog/backtest-result-dialog.component';

@Component({
  selector: 'app-backtest-dashboard',
  templateUrl: './backtest-dashboard.component.html',
  styleUrls: []
})
export class BacktestDashboardComponent implements OnInit {
  backtests: Backtest[] = [];
  displayedColumns: string[] = ['strategyName', 'symbol', 'interval', 'dateRange', 'return', 'drawdown', 'actions'];
  isLoading = true;
  filters = {
    strategy: '',
    symbol: '',
    interval: '',
    startDate: '',
    endDate: ''
  };

  constructor(private backtestService: BacktestService, private dialog: MatDialog) { }

  ngOnInit(): void {
    this.loadBacktests();
  }

  loadBacktests() {
    this.isLoading = true;
    this.backtestService.getMyBacktests().subscribe({
      next: (data: Backtest[]) => {
        this.backtests = data;
        this.isLoading = false;
      },
      error: () => this.isLoading = false
    });
  }

  openRunDialog() {
    const dialogRef = this.dialog.open(RunBacktestDialogComponent, {
      width: '500px'
    });

    dialogRef.afterClosed().subscribe((result: any) => {
      if (result) {
        this.loadBacktests();
        this.openResultDetails(result.id);
      }
    });
  }

  openResultDetails(id: number) {
    this.backtestService.getBacktestDetails(id).subscribe(details => {
      this.dialog.open(BacktestResultDialogComponent, {
        width: '900px',
        maxWidth: '95vw',
        data: details
      });
    });
  }

  deleteBacktest(id: number) {
    if (confirm('Delete this backtest result?')) {
      this.backtestService.deleteBacktest(id).subscribe(() => this.loadBacktests());
    }
  }

  deleteAllBacktests() {
    if (confirm('Delete all stored backtest results? This cannot be undone.')) {
      this.backtestService.deleteAllBacktests().subscribe(() => this.loadBacktests());
    }
  }

  get filteredBacktests(): Backtest[] {
    return this.backtests.filter(backtest => {
      const backtestStart = new Date(backtest.startDate);
      const backtestEnd = new Date(backtest.endDate);
      const filterStart = this.filters.startDate ? new Date(this.filters.startDate) : null;
      const filterEnd = this.filters.endDate ? new Date(this.filters.endDate) : null;

      if (filterEnd) {
        filterEnd.setHours(23, 59, 59, 999);
      }

      return this.matchesFilter(backtest.strategyName, this.filters.strategy) &&
        this.matchesFilter(backtest.symbol, this.filters.symbol) &&
        this.matchesFilter(backtest.interval, this.filters.interval) &&
        (!filterStart || backtestEnd >= filterStart) &&
        (!filterEnd || backtestStart <= filterEnd);
    });
  }

  get strategyOptions(): string[] {
    return this.uniqueOptions(this.backtests.map(b => b.strategyName));
  }

  get symbolOptions(): string[] {
    return this.uniqueOptions(this.backtests.map(b => b.symbol));
  }

  get intervalOptions(): string[] {
    return this.uniqueOptions(this.backtests.map(b => b.interval));
  }

  clearFilters(): void {
    this.filters = {
      strategy: '',
      symbol: '',
      interval: '',
      startDate: '',
      endDate: ''
    };
  }

  private matchesFilter(value: string | null | undefined, filter: string): boolean {
    return !filter || (value || '') === filter;
  }

  private uniqueOptions(values: Array<string | null | undefined>): string[] {
    return Array.from(new Set(values.filter((value): value is string => !!value))).sort();
  }
}
