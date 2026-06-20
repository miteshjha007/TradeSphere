import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { PropFirmDashboardComponent } from './pages/prop-firm-dashboard/prop-firm-dashboard.component';

const routes: Routes = [
  { path: '', component: PropFirmDashboardComponent }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class PropFirmsRoutingModule { }
