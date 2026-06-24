import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthService } from '../../auth/services/auth.service';

export interface IpoItem {
  companyName: string;
  status: string;
  segment: string;
  source: string;
  filingDate?: string;
  openDate?: string;
  closeDate?: string;
  listingDate?: string;
  priceBand: string;
  issueSizeCrore?: number;
  gmpPercent?: number;
  totalSubscriptionX?: number;
  qibSubscriptionX?: number;
  niiSubscriptionX?: number;
  retailSubscriptionX?: number;
  score: number;
  verdict: string;
  documentUrl: string;
  reasons: string[];
  missingSignals: string[];
}

export interface IpoDashboard {
  lastUpdatedAt: string;
  topCurrent: IpoItem[];
  topUpcoming: IpoItem[];
  current: IpoItem[];
  upcoming: IpoItem[];
  recentFilings: IpoItem[];
  warnings: string[];
}

@Injectable({ providedIn: 'root' })
export class IpoService {
  private apiUrl = 'http://localhost:5083/api/ipo';

  constructor(private http: HttpClient, private authService: AuthService) { }

  private getHeaders(): HttpHeaders {
    const token = this.authService.getToken();
    return new HttpHeaders().set('Authorization', `Bearer ${token}`);
  }

  getDashboard(): Observable<IpoDashboard> {
    return this.http.get<IpoDashboard>(`${this.apiUrl}/dashboard`, { headers: this.getHeaders() });
  }
}
