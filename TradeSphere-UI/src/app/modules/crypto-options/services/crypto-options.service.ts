import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthService } from '../../auth/services/auth.service';

export interface CryptoOptionConfig {
  id: number;
  name: string;
  strategyType: string;
  underlying: string;
  symbol: string;
  exchange: string;
  expiryType: string;
  entryTime: string;
  exitTime: string;
  targetPremiumPerLeg: number;
  stopLossPercentPerLeg: number;
  strikeSelectionMode: string;
  strikeDistancePercent: number;
  maxDailyLoss: number;
  lotSize: number;
  isActive: boolean;
}

export interface CryptoOptionBacktestRun {
  id: number;
  strategyName: string;
  strategyType: string;
  symbol: string;
  exchange: string;
  fromDate: string;
  toDate: string;
  initialCapital: number;
  totalPnl: number;
  grossPnl: number;
  charges: number;
  totalTrades: number;
  winningDays: number;
  losingDays: number;
  maxDrawdown: number;
  profitFactor: number;
  status: string;
  errorMessage?: string;
}

export interface CryptoOptionSnapshot {
  id: number;
  exchange: string;
  underlying: string;
  symbol: string;
  expiryDate: string;
  strike: number;
  underlyingPrice: number;
  snapshotTime: string;
  callPremium?: number;
  callBid?: number;
  callAsk?: number;
  callDelta?: number;
  putPremium?: number;
  putBid?: number;
  putAsk?: number;
  putDelta?: number;
}

export interface CryptoOptionDailyPnl {
  tradeDate: string;
  grossPnl: number;
  netPnl: number;
  charges: number;
  maxIntradayLoss: number;
  ceLegPnl: number;
  peLegPnl: number;
  isCircuitBreakerHit: boolean;
  notes?: string;
}

export interface CryptoOptionLeg {
  id: number;
  legType: string;
  action: string;
  strike: number;
  entryTime: string;
  entryPremium: number;
  exitTime?: string;
  exitPremium?: number;
  quantity: number;
  pnl: number;
  status: string;
  exitReason: string;
  stopLossHit: boolean;
}


export interface CryptoOptionExpiry {
  label: string;
  expiryDate: string;
  isToday: boolean;
  isExpired: boolean;
  timeToExpiryHours?: number;
}

export interface CryptoOptionSuggestion {
  strategyType: string;
  status: string;
  recommendation: string;
  expiryDate?: string;
  callStrike?: number;
  putStrike?: number;
  callPremium?: number;
  putPremium?: number;
  callDelta?: number;
  putDelta?: number;
  estimatedCredit?: number;
  reason: string;
}

export interface CryptoOptionChainFetchResult {
  exchange: string;
  underlying: string;
  symbol: string;
  snapshotTime: string;
  selectedExpiryDate?: string;
  underlyingPrice: number;
  imported: number;
  expiries: CryptoOptionExpiry[];
  rows: CryptoOptionSnapshot[];
  suggestions: CryptoOptionSuggestion[];
  warnings: string[];
}
@Injectable({ providedIn: 'root' })
export class CryptoOptionsService {
  private apiUrl = 'http://localhost:5083/api/crypto-options';

  constructor(private http: HttpClient, private authService: AuthService) { }

  private getHeaders(): HttpHeaders {
    const token = this.authService.getToken();
    return new HttpHeaders().set('Authorization', `Bearer ${token}`);
  }

  getConfigs(): Observable<CryptoOptionConfig[]> {
    return this.http.get<CryptoOptionConfig[]>(`${this.apiUrl}/configs`, { headers: this.getHeaders() });
  }

  createConfig(data: any): Observable<CryptoOptionConfig> {
    return this.http.post<CryptoOptionConfig>(`${this.apiUrl}/configs`, data, { headers: this.getHeaders() });
  }

  runBacktest(data: any): Observable<CryptoOptionBacktestRun> {
    return this.http.post<CryptoOptionBacktestRun>(`${this.apiUrl}/backtest/run`, data, { headers: this.getHeaders() });
  }

  getRuns(): Observable<CryptoOptionBacktestRun[]> {
    return this.http.get<CryptoOptionBacktestRun[]>(`${this.apiUrl}/backtest/runs`, { headers: this.getHeaders() });
  }

  getDailyPnl(runId: number): Observable<CryptoOptionDailyPnl[]> {
    return this.http.get<CryptoOptionDailyPnl[]>(`${this.apiUrl}/backtest/runs/${runId}/daily-pnl`, { headers: this.getHeaders() });
  }

  getTrades(runId: number): Observable<CryptoOptionLeg[]> {
    return this.http.get<CryptoOptionLeg[]>(`${this.apiUrl}/backtest/runs/${runId}/trades`, { headers: this.getHeaders() });
  }

  getSnapshots(exchange?: string, symbol?: string): Observable<CryptoOptionSnapshot[]> {
    let params = new HttpParams();
    if (exchange) params = params.set('exchange', exchange);
    if (symbol) params = params.set('symbol', symbol);
    return this.http.get<CryptoOptionSnapshot[]>(`${this.apiUrl}/chain-snapshots`, { headers: this.getHeaders(), params });
  }


  getDeltaExpiries(exchange?: string, underlying?: string, symbol?: string): Observable<CryptoOptionExpiry[]> {
    let params = new HttpParams();
    if (exchange) params = params.set('exchange', exchange);
    if (underlying) params = params.set('underlying', underlying);
    if (symbol) params = params.set('symbol', symbol);
    return this.http.get<CryptoOptionExpiry[]>(`${this.apiUrl}/delta/expiries`, { headers: this.getHeaders(), params });
  }

  fetchDeltaChain(data: any): Observable<CryptoOptionChainFetchResult> {
    return this.http.post<CryptoOptionChainFetchResult>(`${this.apiUrl}/delta/chain`, data, { headers: this.getHeaders() });
  }
  getRiskReport(): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/reports/risk`, { headers: this.getHeaders() });
  }
}



