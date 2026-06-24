import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { IndianMarketDashboardComponent } from './pages/indian-market-dashboard/indian-market-dashboard.component';

const routes: Routes = [
  { path: '', component: IndianMarketDashboardComponent }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class IndianMarketRoutingModule { }
