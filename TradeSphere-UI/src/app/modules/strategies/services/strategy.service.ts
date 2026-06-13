import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthService } from '../../auth/services/auth.service';
import { Strategy, UserStrategy, DeployStrategyDto, CreateStrategyDto } from '../models/strategy.model';

@Injectable({
  providedIn: 'root'
})
export class StrategyService {
  private apiUrl = 'http://localhost:5083/api/strategy';

  constructor(private http: HttpClient, private authService: AuthService) { }

  private getHeaders(): HttpHeaders {
    const token = this.authService.getToken();
    return new HttpHeaders().set('Authorization', `Bearer ${token}`);
  }

  getAvailableStrategies(): Observable<Strategy[]> {
    return this.http.get<Strategy[]>(`${this.apiUrl}/available`, { headers: this.getHeaders() });
  }

  getUserStrategies(): Observable<UserStrategy[]> {
    return this.http.get<UserStrategy[]>(`${this.apiUrl}/my-strategies`, { headers: this.getHeaders() });
  }

  deployStrategy(data: DeployStrategyDto): Observable<UserStrategy> {
    return this.http.post<UserStrategy>(`${this.apiUrl}/deploy`, data, { headers: this.getHeaders() });
  }

  createStrategy(data: CreateStrategyDto): Observable<Strategy> {
    return this.http.post<Strategy>(`${this.apiUrl}/create`, data, { headers: this.getHeaders() });
  }

  toggleStatus(id: number, status: string): Observable<void> {
    return this.http.patch<void>(`${this.apiUrl}/${id}/status`, `"${status}"`, {
      headers: this.getHeaders().set('Content-Type', 'application/json')
    });
  }

  deleteStrategy(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`, { headers: this.getHeaders() });
  }
}
