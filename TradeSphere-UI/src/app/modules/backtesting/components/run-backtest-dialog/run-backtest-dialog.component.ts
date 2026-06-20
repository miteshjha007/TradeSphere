import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef } from '@angular/material/dialog';
import { StrategyService } from '../../../strategies/services/strategy.service';
import { BacktestService } from '../../services/backtest.service';
import { Strategy } from '../../../strategies/models/strategy.model';
import { Mt5Account, Mt5Service } from '../../../mt5/services/mt5.service';

@Component({
  selector: 'app-run-backtest-dialog',
  templateUrl: './run-backtest-dialog.component.html',
  styleUrls: []
})
export class RunBacktestDialogComponent implements OnInit {
  form: FormGroup;
  strategies: Strategy[] = [];
  mt5Accounts: Mt5Account[] = [];
  isLoading = false;

  constructor(
    private fb: FormBuilder,
    private strategyService: StrategyService,
    private backtestService: BacktestService,
    private mt5Service: Mt5Service,
    private dialogRef: MatDialogRef<RunBacktestDialogComponent>
  ) {
    const defaultStart = new Date();
    defaultStart.setMonth(defaultStart.getMonth() - 1);

    this.form = this.fb.group({
      strategyId: ['', Validators.required],
      dataSource: ['Delta', Validators.required],
      mt5AccountId: [''],
      symbol: ['BTCUSD', Validators.required],
      interval: ['3m', Validators.required],
      startDate: [defaultStart.toISOString().split('T')[0], Validators.required],
      endDate: [new Date().toISOString().split('T')[0], Validators.required],
      initialCapital: [10000, Validators.required]
    });
  }

  ngOnInit(): void {
    this.strategyService.getAvailableStrategies().subscribe(data => this.strategies = data);
    this.mt5Service.getAccounts().subscribe(data => this.mt5Accounts = data);
    this.form.get('dataSource')?.valueChanges.subscribe(source => {
      const mt5Account = this.form.get('mt5AccountId');
      if (source === 'MT5') {
        mt5Account?.setValidators([Validators.required]);
        if (this.form.get('symbol')?.value === 'BTCUSD') {
          this.form.patchValue({ symbol: 'XAUUSD' }, { emitEvent: false });
        }
      } else {
        mt5Account?.clearValidators();
        if (this.form.get('symbol')?.value === 'XAUUSD') {
          this.form.patchValue({ symbol: 'BTCUSD' }, { emitEvent: false });
        }
      }
      mt5Account?.updateValueAndValidity();
    });
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
