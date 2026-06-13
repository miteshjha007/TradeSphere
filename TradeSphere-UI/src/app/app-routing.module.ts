import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MainLayoutComponent } from './components/layout/main-layout/main-layout.component';

const routes: Routes = [
  { path: 'auth', loadChildren: () => import('./modules/auth/auth.module').then(m => m.AuthModule) },
  {
    path: '',
    component: MainLayoutComponent,
    children: [
      { path: 'dashboard', loadChildren: () => import('./modules/dashboard/dashboard.module').then(m => m.DashboardModule) },
      { path: 'exchanges', loadChildren: () => import('./modules/exchanges/exchanges.module').then(m => m.ExchangesModule) },
      { path: 'strategies', loadChildren: () => import('./modules/strategies/strategies.module').then(m => m.StrategiesModule) },
      { path: 'backtesting', loadChildren: () => import('./modules/backtesting/backtesting.module').then(m => m.BacktestingModule) },
      { path: 'reports', loadChildren: () => import('./modules/analytics/analytics.module').then(m => m.AnalyticsModule) },
      // Other modules will go here in future
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' }
    ]
  }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
