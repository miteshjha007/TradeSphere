import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { StockPicksRoutingModule } from './stock-picks-routing.module';
import { StockPicksDashboardComponent } from './pages/stock-picks-dashboard/stock-picks-dashboard.component';

@NgModule({
  declarations: [StockPicksDashboardComponent],
  imports: [
    CommonModule,
    StockPicksRoutingModule,
    MatIconModule,
    MatProgressSpinnerModule
  ]
})
export class StockPicksModule { }
