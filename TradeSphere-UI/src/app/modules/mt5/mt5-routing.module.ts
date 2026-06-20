import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { Mt5DashboardComponent } from './pages/mt5-dashboard/mt5-dashboard.component';

const routes: Routes = [
  { path: '', component: Mt5DashboardComponent }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class Mt5RoutingModule { }
