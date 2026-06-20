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
  errorMessage = '';

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
        this.errorMessage = 'Dashboard data could not be loaded.';
      }
    });
  }
}
