import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AuthResponse,
  LoginRequest,
  SetupRequest,
  SetupRequiredResponse,
  UserInfo,
} from '../models/portfolio.models';

@Injectable({ providedIn: 'root' })
export class AuthApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/auth';

  checkSetupRequired(): Observable<SetupRequiredResponse> {
    return this.http.get<SetupRequiredResponse>(`${this.base}/setup-required`);
  }

  completeSetup(request: SetupRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.base}/setup`, request);
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.base}/login`, request);
  }

  refreshToken(): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.base}/refresh`, {}, { withCredentials: true });
  }

  logout(): Observable<void> {
    return this.http.post<void>(`${this.base}/logout`, {}, { withCredentials: true });
  }

  getMe(): Observable<UserInfo> {
    return this.http.get<UserInfo>(`${this.base}/me`);
  }
}
