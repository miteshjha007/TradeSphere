import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MainLayoutComponent } from './components/layout/main-layout/main-layout.component';
import { AuthGuard } from './modules/auth/guards/auth.guard';

const routes: Routes = [
  { path: 'auth', loadChildren: () => import('./modules/auth/auth.module').then(m => m.AuthModule) },
  {
    path: '',
    component: MainLayoutComponent,
    canActivate: [AuthGuard],
    children: [
      { path: 'dashboard', loadChildren: () => import('./modules/dashboard/dashboard.module').then(m => m.DashboardModule) },
      { path: 'exchanges', loadChildren: () => import('./modules/exchanges/exchanges.module').then(m => m.ExchangesModule) },
      { path: 'strategies', loadChildren: () => import('./modules/strategies/strategies.module').then(m => m.StrategiesModule) },
      { path: 'backtesting', loadChildren: () => import('./modules/backtesting/backtesting.module').then(m => m.BacktestingModule) },
      { path: 'reports', loadChildren: () => import('./modules/analytics/analytics.module').then(m => m.AnalyticsModule) },
      { path: 'prop-firms', loadChildren: () => import('./modules/prop-firms/prop-firms.module').then(m => m.PropFirmsModule) },
      { path: 'mt5', loadChildren: () => import('./modules/mt5/mt5.module').then(m => m.Mt5Module) },
      {
        path: 'indian-market',
        children: [
          { path: 'options', loadChildren: () => import('./modules/indian-market/indian-market.module').then(m => m.IndianMarketModule) },
          { path: 'ipos', loadChildren: () => import('./modules/ipos/ipos.module').then(m => m.IposModule) },
          { path: 'intraday-picks', loadChildren: () => import('./modules/stock-picks/stock-picks.module').then(m => m.StockPicksModule) },
          { path: 'long-term-picks', loadChildren: () => import('./modules/stock-picks/stock-picks.module').then(m => m.StockPicksModule) },
          { path: '', redirectTo: 'options', pathMatch: 'full' }
        ]
      },
      { path: 'ipos', redirectTo: 'indian-market/ipos', pathMatch: 'full' },
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' }
    ]
  }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
