import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthService } from '../../auth/services/auth.service';

export interface PropFirm {
  id: number;
  name: string;
  websiteUrl?: string;
  status: string;
  notes?: string;
}

export interface CreatePropFirm {
  name: string;
  websiteUrl?: string;
  notes?: string;
}

export interface PropFirmAccount {
  id: number;
  propFirmId: number;
  propFirmName: string;
  mt5AccountId?: number;
  mt5AccountName?: string;
  name: string;
  accountSize: number;
  profitTarget: number;
  dailyDrawdownLimit: number;
  maxDrawdownLimit: number;
  minimumTradingDays: number;
  maxRiskPerTradePercent: number;
  newsTradingAllowed: boolean;
  weekendHoldingAllowed: boolean;
  status: string;
  startedAt?: string;
  notes?: string;
}

export interface CreatePropFirmAccount {
  propFirmId: number;
  mt5AccountId?: number;
  name: string;
  accountSize: number;
  profitTarget: number;
  dailyDrawdownLimit: number;
  maxDrawdownLimit: number;
  minimumTradingDays: number;
  maxRiskPerTradePercent: number;
  newsTradingAllowed: boolean;
  weekendHoldingAllowed: boolean;
  startedAt?: string;
  notes?: string;
}

@Injectable({ providedIn: 'root' })
export class PropFirmService {
  private apiUrl = 'http://localhost:5083/api/propfirm';

  constructor(private http: HttpClient, private authService: AuthService) { }

  private getHeaders(): HttpHeaders {
    const token = this.authService.getToken();
    return new HttpHeaders().set('Authorization', `Bearer ${token}`);
  }

  getFirms(): Observable<PropFirm[]> {
    return this.http.get<PropFirm[]>(`${this.apiUrl}/firms`, { headers: this.getHeaders() });
  }

  createFirm(data: CreatePropFirm): Observable<PropFirm> {
    return this.http.post<PropFirm>(`${this.apiUrl}/firms`, data, { headers: this.getHeaders() });
  }

  deleteFirm(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/firms/${id}`, { headers: this.getHeaders() });
  }

  getAccounts(): Observable<PropFirmAccount[]> {
    return this.http.get<PropFirmAccount[]>(`${this.apiUrl}/accounts`, { headers: this.getHeaders() });
  }

  createAccount(data: CreatePropFirmAccount): Observable<PropFirmAccount> {
    return this.http.post<PropFirmAccount>(`${this.apiUrl}/accounts`, data, { headers: this.getHeaders() });
  }

  deleteAccount(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/accounts/${id}`, { headers: this.getHeaders() });
  }
}
