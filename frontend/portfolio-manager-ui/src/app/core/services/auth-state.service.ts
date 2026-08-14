import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthResponse, UserInfo } from '../models/portfolio.models';
import { AuthApiService } from './auth-api.service';

@Injectable({ providedIn: 'root' })
export class AuthStateService {
  private readonly authApi = inject(AuthApiService);
  private readonly router = inject(Router);

  readonly currentUser = signal<UserInfo | null>(null);
  readonly accessToken = signal<string | null>(null);
  readonly setupRequired = signal<boolean | null>(null);

  readonly isAuthenticated = computed(() => this.accessToken() !== null);
  readonly isAdmin = computed(() => this.currentUser()?.roles.includes('Admin') ?? false);
  readonly isTrader = computed(() => this.currentUser()?.roles.includes('Trader') ?? false);
  readonly canWrite = computed(() => this.isAdmin() || this.isTrader());

  setAuth(response: AuthResponse): void {
    this.accessToken.set(response.accessToken);
    this.currentUser.set(response.user);
  }

  clearAuth(): void {
    this.accessToken.set(null);
    this.currentUser.set(null);
  }

  async initializeAuth(): Promise<void> {
    try {
      const { required } = await firstValueFrom(this.authApi.checkSetupRequired());
      this.setupRequired.set(required);
      if (required) return; // setup guard will redirect
    } catch {
      this.router.navigate(['/login']);
      return;
    }

    try {
      const response = await firstValueFrom(this.authApi.refreshToken());
      this.setAuth(response);
    } catch {
      // No valid refresh cookie — guard will redirect to /login
    }
  }
}
