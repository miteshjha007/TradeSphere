import { Injectable } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthService } from '../../auth/services/auth.service';

export interface Mt5Account {
  id: number;
  name: string;
  login: number;
  loginPreview: string;
  server: string;
  accountType: string;
  currency: string;
  leverage: number;
  tradingEnabled: boolean;
  status: string;
  balance?: number;
  equity?: number;
  freeMargin?: number;
  lastSyncedAt?: string;
  lastError?: string;
}

export interface ConnectMt5Account {
  name: string;
  login: number;
  server: string;
  password: string;
  accountType: string;
  currency: string;
  leverage: number;
  tradingEnabled: boolean;
}

export interface Mt5ConnectionTestResult {
  success: boolean;
  message: string;
  status: string;
  balance?: number;
  equity?: number;
  freeMargin?: number;
  bridgeEndpoint?: string;
}

export interface Mt5SymbolMapping {
  id: number;
  mt5AccountId: number;
  accountName: string;
  strategySymbol: string;
  brokerSymbol: string;
  isActive: boolean;
  notes?: string;
}

export interface UpsertMt5SymbolMapping {
  mt5AccountId: number;
  strategySymbol: string;
  brokerSymbol: string;
  isActive: boolean;
  notes?: string;
}

@Injectable({ providedIn: 'root' })
export class Mt5Service {
  private apiUrl = `${environment.apiBaseUrl}/mt5`;

  constructor(private http: HttpClient, private authService: AuthService) { }

  private getHeaders(): HttpHeaders {
    const token = this.authService.getToken();
    return new HttpHeaders().set('Authorization', `Bearer ${token}`);
  }

  getAccounts(): Observable<Mt5Account[]> {
    return this.http.get<Mt5Account[]>(`${this.apiUrl}/accounts`, { headers: this.getHeaders() });
  }

  connectAccount(data: ConnectMt5Account): Observable<Mt5Account> {
    return this.http.post<Mt5Account>(`${this.apiUrl}/accounts`, data, { headers: this.getHeaders() });
  }

  deleteAccount(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/accounts/${id}`, { headers: this.getHeaders() });
  }

  testConnection(id: number): Observable<Mt5ConnectionTestResult> {
    return this.http.post<Mt5ConnectionTestResult>(`${this.apiUrl}/accounts/${id}/test-connection`, {}, { headers: this.getHeaders() });
  }

  getSymbolMappings(): Observable<Mt5SymbolMapping[]> {
    return this.http.get<Mt5SymbolMapping[]>(`${this.apiUrl}/symbol-mappings`, { headers: this.getHeaders() });
  }

  upsertSymbolMapping(data: UpsertMt5SymbolMapping): Observable<Mt5SymbolMapping> {
    return this.http.post<Mt5SymbolMapping>(`${this.apiUrl}/symbol-mappings`, data, { headers: this.getHeaders() });
  }

  deleteSymbolMapping(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/symbol-mappings/${id}`, { headers: this.getHeaders() });
  }
}
