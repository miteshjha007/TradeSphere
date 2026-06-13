import { Component, OnInit } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { BacktestService } from '../../services/backtest.service';
import { Backtest } from '../../models/backtest.model';
import { RunBacktestDialogComponent } from '../../components/run-backtest-dialog/run-backtest-dialog.component';

@Component({
  selector: 'app-backtest-dashboard',
  templateUrl: './backtest-dashboard.component.html',
  styleUrls: []
})
export class BacktestDashboardComponent implements OnInit {
  backtests: Backtest[] = [];
  displayedColumns: string[] = ['strategyName', 'symbol', 'interval', 'dateRange', 'return', 'drawdown', 'actions'];
  isLoading = true;

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
      }
    });
  }

  deleteBacktest(id: number) {
    if (confirm('Delete this backtest result?')) {
      this.backtestService.deleteBacktest(id).subscribe(() => this.loadBacktests());
    }
  }
}
