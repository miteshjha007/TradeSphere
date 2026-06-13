import { Component, OnInit } from '@angular/core';
import { DashboardService, DashboardData } from '../../services/dashboard.service';

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: []
})
export class DashboardComponent implements OnInit {
  data: DashboardData | null = null;
  isLoading = true;

  constructor(private dashboardService: DashboardService) { }

  ngOnInit(): void {
    this.dashboardService.getOverview().subscribe({
      next: (res) => {
        this.data = res;
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Failed to load dashboard data', err);
        this.isLoading = false;
        // Mock data for demo if API fails/not running
        this.data = {
          totalBalance: 12450.00,
          totalPnl: 2450.00,
          activeStrategies: 3,
          connectedExchanges: 2,
          recentTrades: [],
          topStrategies: []
        };
      }
    });
  }
}
