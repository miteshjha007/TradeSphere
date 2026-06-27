import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthService } from '../../auth/services/auth.service';

export interface TradeRecord {
  id: number;
  strategyName: string;
  exchangeName: string;
  executionProvider: string;
  executionAccount: string;
  symbol: string;
  side: string;
  orderType: string;
  price: number | null;
  quantity: number;
  status: string;
  executedAt: string | null;
  createdAt: string;
  pnl: number;
  externalOrderId: string;
  errorReason?: string | null;
  brokerTicket?: string | null;
  activityType: string;
}

export interface PositionRecord {
  exchangeName: string;
  symbol: string;
  side: string;
  size: number;
  entryPrice: number;
  markPrice: number;
  unrealizedPnl: number;
  realizedPnl: number;
  margin: number;
  status: string;
  updatedAt: string;
}

export interface TradingOverview {
  trades: TradeRecord[];
  positions: PositionRecord[];
}

@Injectable({
  providedIn: 'root'
})
export class TradingService {
  private apiUrl = 'http://localhost:5083/api/trading';

  constructor(private http: HttpClient, private authService: AuthService) { }

  getOverview(): Observable<TradingOverview> {
    return this.http.get<TradingOverview>(`${this.apiUrl}/overview`, { headers: this.getHeaders() });
  }

  deleteAllTrades(): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/trades`, { headers: this.getHeaders() });
  }

  private getHeaders(): HttpHeaders {
    const token = this.authService.getToken();
    return new HttpHeaders().set('Authorization', `Bearer ${token}`);
  }
}
