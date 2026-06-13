import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { StrategyListComponent } from './pages/strategy-list/strategy-list.component';
import { StrategyCreateWizardComponent } from './components/strategy-create-wizard/strategy-create-wizard.component';

const routes: Routes = [
  { path: '', component: StrategyListComponent },
  { path: 'create', component: StrategyCreateWizardComponent }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class StrategiesRoutingModule { }
