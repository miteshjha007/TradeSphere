import { Injectable } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthService } from '../../auth/services/auth.service';
import { Exchange, UserExchange, ConnectExchangeDto } from '../models/exchange.model';

export interface ConnectionTestResult {
  success: boolean;
  message: string;
  walletBalance?: number;
  currency?: string;
  coinsBalance?: number;
  coinsCurrency?: string;
  futuresBalance?: number;
  futuresCurrency?: string;
}

@Injectable({
  providedIn: 'root'
})
export class ExchangeService {
  private apiUrl = `${environment.apiBaseUrl}/exchange`;

  constructor(private http: HttpClient, private authService: AuthService) { }

  private getHeaders(): HttpHeaders {
    const token = this.authService.getToken();
    return new HttpHeaders().set('Authorization', `Bearer ${token}`);
  }

  getSupportedExchanges(): Observable<Exchange[]> {
    return this.http.get<Exchange[]>(`${this.apiUrl}/supported`, { headers: this.getHeaders() });
  }

  getUserExchanges(): Observable<UserExchange[]> {
    return this.http.get<UserExchange[]>(this.apiUrl, { headers: this.getHeaders() });
  }

  connectExchange(data: ConnectExchangeDto): Observable<UserExchange> {
    return this.http.post<UserExchange>(this.apiUrl, data, { headers: this.getHeaders() });
  }

  deleteExchange(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`, { headers: this.getHeaders() });
  }

  testConnection(id: number): Observable<ConnectionTestResult> {
    return this.http.post<ConnectionTestResult>(`${this.apiUrl}/${id}/test-connection`, {}, { headers: this.getHeaders() });
  }
}
