import { Injectable } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { tap } from 'rxjs/operators';
import { Router } from '@angular/router';

@Injectable({
    providedIn: 'root'
})
export class AuthService {
    private apiUrl = `${environment.apiBaseUrl}/auth`; // Updated to match running API
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
        const token = this.getToken();
        return !!token && !this.isTokenExpired(token);
    }

    getUsername(): string {
        const token = this.getToken();
        if (!token || this.isTokenExpired(token)) return 'User';
        try {
            const payload = this.decodePayload(token);
            const name = payload.unique_name || payload.name || payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'] || 'User';
            return name;
        } catch (e) {
            console.error('Error decoding token', e);
            return 'User';
        }
    }

    private isTokenExpired(token: string): boolean {
        try {
            const payload = this.decodePayload(token);
            if (!payload.exp) {
                return false;
            }

            const expiryMs = Number(payload.exp) * 1000;
            return Date.now() >= expiryMs;
        } catch {
            return true;
        }
    }

    private decodePayload(token: string): any {
        const payloadBase64 = token.split('.')[1];
        if (!payloadBase64) {
            throw new Error('Invalid token format');
        }

        const base64 = payloadBase64.replace(/-/g, '+').replace(/_/g, '/');
        const jsonPayload = decodeURIComponent(atob(base64).split('').map(function(c) {
                return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
            }).join(''));

        return JSON.parse(jsonPayload);
    }
}
