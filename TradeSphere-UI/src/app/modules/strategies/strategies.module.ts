import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialogModule } from '@angular/material/dialog';
import { MatStepperModule } from '@angular/material/stepper';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterModule, Routes } from '@angular/router';

import { StrategyListComponent } from './pages/strategy-list/strategy-list.component';
import { StrategyCreateWizardComponent } from './components/strategy-create-wizard/strategy-create-wizard.component';

const routes: Routes = [
  { path: '', component: StrategyListComponent },
  { path: 'create', component: StrategyCreateWizardComponent }
];

@NgModule({
  declarations: [
    StrategyListComponent,
    StrategyCreateWizardComponent
  ],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatDialogModule,
    MatStepperModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    RouterModule.forChild(routes)
  ]
})
export class StrategiesModule { }
