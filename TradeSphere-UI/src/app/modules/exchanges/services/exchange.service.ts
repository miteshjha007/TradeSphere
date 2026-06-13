import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthService } from '../../auth/services/auth.service';
import { Exchange, UserExchange, ConnectExchangeDto } from '../models/exchange.model';

@Injectable({
  providedIn: 'root'
})
export class ExchangeService {
  private apiUrl = 'http://localhost:5083/api/exchange';

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
}
