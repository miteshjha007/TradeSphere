import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthService } from '../../auth/services/auth.service';

export interface StockPick {
  rank: number;
  symbol: string;
  name: string;
  bias: string;
  horizon: string;
  risk: string;
  lastPrice: number;
  score: number;
  change1DPercent: number;
  change5DPercent: number;
  change20DPercent: number;
  volumeRatio: number;
  volatilityPercent: number;
  trendStrengthPercent: number;
  entryZone: string;
  stopLoss: string;
  target1: string;
  target2: string;
  reasons: string[];
}

export interface StockPickDashboard {
  lastUpdatedAt: string;
  universe: string;
  source: string;
  methodology: string;
  warnings: string[];
  picks: StockPick[];
}

export interface StockAnalysisRequest {
  symbol: string;
  horizon: 'ShortTerm' | 'LongTerm';
}

export interface StockAnalysis {
  lastUpdatedAt: string;
  symbol: string;
  name: string;
  horizon: string;
  verdict: string;
  recommendation: string;
  risk: string;
  lastPrice: number;
  technicalScore: number;
  fundamentalScore: number;
  overallScore: number;
  change1DPercent: number;
  change5DPercent: number;
  change20DPercent: number;
  volatilityPercent: number;
  entryZone: string;
  stopLoss: string;
  target1: string;
  target2: string;
  technicalSignals: string[];
  fundamentalSignals: string[];
  warnings: string[];
}
@Injectable({ providedIn: 'root' })
export class StockPicksService {
  private apiUrl = 'http://localhost:5083/api/indian-market/stock-picks';

  constructor(private http: HttpClient, private authService: AuthService) { }

  private getHeaders(): HttpHeaders {
    const token = this.authService.getToken();
    return new HttpHeaders().set('Authorization', `Bearer ${token}`);
  }

  getIntradayPicks(): Observable<StockPickDashboard> {
    return this.http.get<StockPickDashboard>(`${this.apiUrl}/intraday`, { headers: this.getHeaders() });
  }

  getLongTermPicks(): Observable<StockPickDashboard> {
    return this.http.get<StockPickDashboard>(`${this.apiUrl}/long-term`, { headers: this.getHeaders() });
  }
  analyzeStock(request: StockAnalysisRequest): Observable<StockAnalysis> {
    return this.http.post<StockAnalysis>(`${this.apiUrl}/analyze`, request, { headers: this.getHeaders() });
  }
}

