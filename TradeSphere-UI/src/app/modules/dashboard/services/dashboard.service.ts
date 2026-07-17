import { Injectable } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthService } from '../../auth/services/auth.service';

export interface DashboardData {
  totalBalance: number;
  totalPnl: number;
  activeStrategies: number;
  connectedExchanges: number;
  recentTrades: RecentTrade[];
  topStrategies: any[];
}

export interface RecentTrade {
  symbol: string;
  side: string;
  price: number;
  quantity: number;
  status: string;
  errorReason?: string;
  timeAgo: string;
}

@Injectable({
  providedIn: 'root'
})
export class DashboardService {
  private apiUrl = `${environment.apiBaseUrl}/dashboard`;

  constructor(private http: HttpClient, private authService: AuthService) { }

  getOverview(): Observable<DashboardData> {
    const token = this.authService.getToken();
    const headers = new HttpHeaders().set('Authorization', `Bearer ${token}`);
    return this.http.get<DashboardData>(this.apiUrl, { headers });
  }
}
