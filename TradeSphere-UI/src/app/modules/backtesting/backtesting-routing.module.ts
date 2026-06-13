import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { BacktestDashboardComponent } from './pages/backtest-dashboard/backtest-dashboard.component';

const routes: Routes = [
  { path: '', component: BacktestDashboardComponent }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class BacktestingRoutingModule { }
