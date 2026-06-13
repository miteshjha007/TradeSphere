import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatFormFieldModule } from '@angular/material/form-field';

import { ExchangeRoutingModule } from './exchanges-routing.module';
import { ExchangeListComponent } from './pages/exchange-list/exchange-list.component';
import { AddExchangeDialogComponent } from './components/add-exchange-dialog/add-exchange-dialog.component';


@NgModule({
  declarations: [
    ExchangeListComponent,
    AddExchangeDialogComponent
  ],
  imports: [
    CommonModule,
    ExchangeRoutingModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule,
    MatProgressSpinnerModule,
    MatFormFieldModule
  ]
})
export class ExchangesModule { }
