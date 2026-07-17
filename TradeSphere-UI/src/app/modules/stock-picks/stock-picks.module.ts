import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { StockPicksRoutingModule } from './stock-picks-routing.module';
import { StockPicksDashboardComponent } from './pages/stock-picks-dashboard/stock-picks-dashboard.component';
import { StockAnalyzerComponent } from './pages/stock-analyzer/stock-analyzer.component';

@NgModule({
  declarations: [StockPicksDashboardComponent, StockAnalyzerComponent],
  imports: [
    CommonModule,
    FormsModule,
    StockPicksRoutingModule,
    MatIconModule,
    MatProgressSpinnerModule
  ]
})
export class StockPicksModule { }


