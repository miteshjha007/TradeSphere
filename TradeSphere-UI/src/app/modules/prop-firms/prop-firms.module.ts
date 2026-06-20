import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTabsModule } from '@angular/material/tabs';
import { PropFirmsRoutingModule } from './prop-firms-routing.module';
import { PropFirmDashboardComponent } from './pages/prop-firm-dashboard/prop-firm-dashboard.component';

@NgModule({
  declarations: [PropFirmDashboardComponent],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    PropFirmsRoutingModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatTabsModule
  ]
})
export class PropFirmsModule { }
