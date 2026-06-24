import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { IpoDashboardComponent } from './pages/ipo-dashboard/ipo-dashboard.component';

const routes: Routes = [
  { path: '', component: IpoDashboardComponent }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class IposRoutingModule { }
