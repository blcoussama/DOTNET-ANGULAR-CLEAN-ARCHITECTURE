import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE } from '../http/api.config';
import { AuthResponse, AuthUser, LoginRequest, RegisterRequest } from '../models/api.models';

/** Endpoints `/auth/*`. Le cookie httpOnly `refreshToken` voyage grâce à `withCredentials`. */
@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE}/auth`;

  login(body: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.base}/login`, body, { withCredentials: true });
  }

  register(body: RegisterRequest): Observable<unknown> {
    return this.http.post<unknown>(`${this.base}/register`, body, { withCredentials: true });
  }

  refresh(): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.base}/refresh`, {}, { withCredentials: true });
  }

  logout(): Observable<unknown> {
    return this.http.post<unknown>(`${this.base}/logout`, {}, { withCredentials: true });
  }

  me(): Observable<AuthUser> {
    return this.http.get<AuthUser>(`${this.base}/me`, { withCredentials: true });
  }
}
