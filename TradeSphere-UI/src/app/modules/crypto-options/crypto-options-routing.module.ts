import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { CryptoOptionsDashboardComponent } from './pages/crypto-options-dashboard/crypto-options-dashboard.component';

const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  { path: 'dashboard', component: CryptoOptionsDashboardComponent },
  { path: 'backtest', component: CryptoOptionsDashboardComponent },
  { path: 'strategy-config', component: CryptoOptionsDashboardComponent },
  { path: 'option-chain', component: CryptoOptionsDashboardComponent },
  { path: 'runs', component: CryptoOptionsDashboardComponent },
  { path: 'daily-pnl', component: CryptoOptionsDashboardComponent },
  { path: 'risk-report', component: CryptoOptionsDashboardComponent },
  { path: 'scanner', component: CryptoOptionsDashboardComponent },
  { path: 'paper-trading', component: CryptoOptionsDashboardComponent },
  { path: 'live-trading', component: CryptoOptionsDashboardComponent }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class CryptoOptionsRoutingModule { }
