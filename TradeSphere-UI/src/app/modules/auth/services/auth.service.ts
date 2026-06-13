import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { tap } from 'rxjs/operators';
import { Router } from '@angular/router';

@Injectable({
    providedIn: 'root'
})
export class AuthService {
    private apiUrl = 'http://localhost:5083/api/auth'; // Updated to match running API
    private tokenKey = 'tradesphere_token';

    constructor(private http: HttpClient, private router: Router) { }

    login(credentials: any): Observable<any> {
        return this.http.post<{ token: string }>(`${this.apiUrl}/login`, credentials).pipe(
            tap(response => {
                localStorage.setItem(this.tokenKey, response.token);
            })
        );
    }

    register(user: any): Observable<any> {
        return this.http.post<{ token: string }>(`${this.apiUrl}/register`, user).pipe(
            tap(response => {
                localStorage.setItem(this.tokenKey, response.token);
            })
        );
    }

    forgotPassword(email: string): Observable<any> {
        return this.http.post(`${this.apiUrl}/forgot-password`, { email });
    }

    verifyOtp(email: string, otp: string): Observable<any> {
        return this.http.post(`${this.apiUrl}/verify-otp`, { email, otp });
    }

    resetPassword(email: string, otp: string, newPassword: string): Observable<any> {
        return this.http.post(`${this.apiUrl}/reset-password`, { email, otp, newPassword });
    }

    googleAuth(credential: string): Observable<any> {
        return this.http.post<{ token: string }>(`${this.apiUrl}/google`, { credential }).pipe(
            tap(response => {
                localStorage.setItem(this.tokenKey, response.token);
            })
        );
    }

    logout(): void {
        localStorage.removeItem(this.tokenKey);
        this.router.navigate(['/auth/login']);
    }

    getToken(): string | null {
        return localStorage.getItem(this.tokenKey);
    }

    isAuthenticated(): boolean {
        return !!this.getToken();
    }
}

