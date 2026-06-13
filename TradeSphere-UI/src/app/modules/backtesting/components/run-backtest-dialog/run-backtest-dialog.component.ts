import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef } from '@angular/material/dialog';
import { StrategyService } from '../../../strategies/services/strategy.service';
import { BacktestService } from '../../services/backtest.service';
import { Strategy } from '../../../strategies/models/strategy.model';

@Component({
  selector: 'app-run-backtest-dialog',
  templateUrl: './run-backtest-dialog.component.html',
  styleUrls: []
})
export class RunBacktestDialogComponent implements OnInit {
  form: FormGroup;
  strategies: Strategy[] = [];
  isLoading = false;

  constructor(
    private fb: FormBuilder,
    private strategyService: StrategyService,
    private backtestService: BacktestService,
    private dialogRef: MatDialogRef<RunBacktestDialogComponent>
  ) {
    const defaultStart = new Date();
    defaultStart.setMonth(defaultStart.getMonth() - 1);

    this.form = this.fb.group({
      strategyId: ['', Validators.required],
      symbol: ['BTCUSDT', Validators.required],
      interval: ['1h', Validators.required],
      startDate: [defaultStart.toISOString().split('T')[0], Validators.required],
      endDate: [new Date().toISOString().split('T')[0], Validators.required],
      initialCapital: [10000, Validators.required]
    });
  }

  ngOnInit(): void {
    this.strategyService.getAvailableStrategies().subscribe(data => this.strategies = data);
  }

  onSubmit() {
    if (this.form.valid) {
      this.isLoading = true;
      this.backtestService.runBacktest(this.form.value).subscribe({
        next: (res) => {
          this.isLoading = false;
          this.dialogRef.close(res);
        },
        error: (err) => {
          console.error(err);
          this.isLoading = false;
        }
      });
    }
  }

  onCancel() {
    this.dialogRef.close();
  }
}
