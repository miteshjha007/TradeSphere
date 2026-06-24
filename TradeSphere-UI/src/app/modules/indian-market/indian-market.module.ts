import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { IndianMarketRoutingModule } from './indian-market-routing.module';
import { IndianMarketDashboardComponent } from './pages/indian-market-dashboard/indian-market-dashboard.component';

@NgModule({
  declarations: [IndianMarketDashboardComponent],
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    IndianMarketRoutingModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ]
})
export class IndianMarketModule { }
