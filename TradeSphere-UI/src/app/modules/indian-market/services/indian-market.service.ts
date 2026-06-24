import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthService } from '../../auth/services/auth.service';

export interface DhanAccount {
  id: number;
  name: string;
  status: string;
  clientIdPreview: string;
}

export interface ConnectDhanAccountDto {
  name: string;
  dhanClientId: string;
  accessToken: string;
}

export interface DhanConnectionTestResult {
  success: boolean;
  message: string;
  availableBalance?: number;
  utilizedAmount?: number;
  withdrawableBalance?: number;
  currency: string;
}

export interface IndianUnderlying {
  symbol: string;
  displayName: string;
  underlyingScrip: number;
  underlyingSegment: string;
  lotSize: number;
  strikeStep: number;
}

export interface OptionLeg {
  optionType: 'CE' | 'PE';
  securityId: string;
  lastPrice: number;
  averagePrice: number;
  impliedVolatility: number;
  openInterest: number;
  previousOpenInterest: number;
  volume: number;
  topBidPrice: number;
  topBidQuantity: number;
  topAskPrice: number;
  topAskQuantity: number;
  delta: number;
  theta: number;
  gamma: number;
  vega: number;
}

export interface OptionChainRow {
  strikePrice: number;
  call?: OptionLeg;
  put?: OptionLeg;
}

export interface OptionChain {
  underlying: string;
  expiry: string;
  underlyingLastPrice: number;
  rows: OptionChainRow[];
}

export interface DhanOptionOrderRequest {
  dhanAccountId: number;
  underlying: string;
  expiry: string;
  strikePrice: number;
  optionType: 'CE' | 'PE';
  securityId: string;
  transactionType: 'BUY' | 'SELL';
  quantity: number;
  productType: string;
  orderType: string;
  price?: number;
}

export interface DhanOrderResult {
  success: boolean;
  message: string;
  orderId?: string;
  orderStatus?: string;
  rawResponse?: string;
}

@Injectable({ providedIn: 'root' })
export class IndianMarketService {
  private apiUrl = 'http://localhost:5083/api/indian-market';

  constructor(private http: HttpClient, private authService: AuthService) { }

  private getHeaders(): HttpHeaders {
    const token = this.authService.getToken();
    return new HttpHeaders().set('Authorization', `Bearer ${token}`);
  }

  getUnderlyings(): Observable<IndianUnderlying[]> {
    return this.http.get<IndianUnderlying[]>(`${this.apiUrl}/underlyings`, { headers: this.getHeaders() });
  }

  getAccounts(): Observable<DhanAccount[]> {
    return this.http.get<DhanAccount[]>(`${this.apiUrl}/dhan/accounts`, { headers: this.getHeaders() });
  }

  connectAccount(dto: ConnectDhanAccountDto): Observable<DhanAccount> {
    return this.http.post<DhanAccount>(`${this.apiUrl}/dhan/accounts`, dto, { headers: this.getHeaders() });
  }

  deleteAccount(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/dhan/accounts/${id}`, { headers: this.getHeaders() });
  }

  testConnection(id: number): Observable<DhanConnectionTestResult> {
    return this.http.post<DhanConnectionTestResult>(`${this.apiUrl}/dhan/accounts/${id}/test-connection`, {}, { headers: this.getHeaders() });
  }

  getExpiries(dhanAccountId: number, underlying: string): Observable<string[]> {
    return this.http.post<string[]>(`${this.apiUrl}/options/expiries`, { dhanAccountId, underlying }, { headers: this.getHeaders() });
  }

  getOptionChain(dhanAccountId: number, underlying: string, expiry: string): Observable<OptionChain> {
    return this.http.post<OptionChain>(`${this.apiUrl}/options/chain`, { dhanAccountId, underlying, expiry }, { headers: this.getHeaders() });
  }

  placeOrder(dto: DhanOptionOrderRequest): Observable<DhanOrderResult> {
    return this.http.post<DhanOrderResult>(`${this.apiUrl}/options/orders`, dto, { headers: this.getHeaders() });
  }
}
