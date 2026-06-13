import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthService } from '../../auth/services/auth.service';
import { Backtest, BacktestResultDetails, RunBacktestDto } from '../models/backtest.model';

@Injectable({
    providedIn: 'root'
})
export class BacktestService {
    private apiUrl = 'http://localhost:5083/api/backtest';

    constructor(private http: HttpClient, private authService: AuthService) { }

    private getHeaders(): HttpHeaders {
        const token = this.authService.getToken();
        return new HttpHeaders().set('Authorization', `Bearer ${token}`);
    }

    getMyBacktests(): Observable<Backtest[]> {
        return this.http.get<Backtest[]>(this.apiUrl, { headers: this.getHeaders() });
    }

    getBacktestDetails(id: number): Observable<BacktestResultDetails> {
        return this.http.get<BacktestResultDetails>(`${this.apiUrl}/${id}`, { headers: this.getHeaders() });
    }

    runBacktest(data: RunBacktestDto): Observable<Backtest> {
        return this.http.post<Backtest>(`${this.apiUrl}/run`, data, { headers: this.getHeaders() });
    }

    deleteBacktest(id: number): Observable<void> {
        return this.http.delete<void>(`${this.apiUrl}/${id}`, { headers: this.getHeaders() });
    }
}
