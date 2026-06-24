import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { StockPicksDashboardComponent } from './pages/stock-picks-dashboard/stock-picks-dashboard.component';

const routes: Routes = [
  { path: '', component: StockPicksDashboardComponent }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class StockPicksRoutingModule { }
