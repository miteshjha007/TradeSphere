import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { IposRoutingModule } from './ipos-routing.module';
import { IpoDashboardComponent } from './pages/ipo-dashboard/ipo-dashboard.component';

@NgModule({
  declarations: [IpoDashboardComponent],
  imports: [
    CommonModule,
    IposRoutingModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ]
})
export class IposModule { }
