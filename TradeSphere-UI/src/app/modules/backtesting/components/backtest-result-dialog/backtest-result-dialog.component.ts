import { Component, Inject } from '@angular/core';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { BacktestResultDetails } from '../../models/backtest.model';

interface BacktestTrade {
  side: string;
  entryPrice: number;
  exitPrice: number;
  pnl: number;
  entryTime: number;
  exitTime: number;
}

interface BacktestResultJson {
  trades?: BacktestTrade[];
  equity?: Array<{ time: number; equity: number }>;
  initialCapital?: number;
  finalCapital?: number;
  interval?: string;
  dailyCandles?: number;
  intradayCandles?: number;
  diagnostics?: string[];
}

@Component({
  selector: 'app-backtest-result-dialog',
  templateUrl: './backtest-result-dialog.component.html',
  styleUrls: []
})
export class BacktestResultDialogComponent {
  readonly parsed: BacktestResultJson;
  readonly trades: BacktestTrade[];
  readonly equityPoints: Array<{ time: number; equity: number }>;
  readonly diagnostics: string[];

  constructor(@Inject(MAT_DIALOG_DATA) public data: BacktestResultDetails) {
    this.parsed = this.parseResultJson(data.resultJson);
    this.trades = this.parsed.trades ?? [];
    this.equityPoints = this.parsed.equity ?? [];
    this.diagnostics = this.parsed.diagnostics ?? [];
  }

  private parseResultJson(resultJson: string | null | undefined): BacktestResultJson {
    if (!resultJson) {
      return {};
    }

    try {
      return JSON.parse(resultJson);
    } catch {
      return {};
    }
  }

  get finalCapital(): number | null {
    if (typeof this.parsed.finalCapital === 'number') {
      return this.parsed.finalCapital;
    }

    const lastPoint = this.equityPoints[this.equityPoints.length - 1];
    return lastPoint ? lastPoint.equity : null;
  }

  formatExchangeTime(value: number): Date | null {
    if (!value) {
      return null;
    }

    return new Date(value * 1000);
  }
}
