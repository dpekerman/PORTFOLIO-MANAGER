import { effect, inject, Injectable, NgZone, OnDestroy } from '@angular/core';
import { AuthApiService } from './auth-api.service';
import { AuthStateService } from './auth-state.service';
import { ConfigService } from './config.service';

@Injectable({ providedIn: 'root' })
export class SessionTimeoutService implements OnDestroy {
  private readonly authState = inject(AuthStateService);
  private readonly authApi = inject(AuthApiService);
  private readonly config = inject(ConfigService);
  private readonly zone = inject(NgZone);

  private timer: ReturnType<typeof setTimeout> | null = null;
  private readonly events = ['mousemove', 'keydown', 'click', 'touchstart'];

  constructor() {
    effect(() => {
      const authenticated = this.authState.isAuthenticated();
      const minutes = this.config.config().sessionTimeoutMinutes;
      if (authenticated && minutes > 0) {
        this.start(minutes);
      } else {
        this.stop();
      }
    });
  }

  private start(minutes: number): void {
    this.stop();
    this.zone.runOutsideAngular(() => {
      const reset = () => this.schedule(minutes);
      this.events.forEach((e) => document.addEventListener(e, reset, { passive: true }));
      (this as unknown as { _reset: () => void })._reset = reset;
      this.schedule(minutes);
    });
  }

  private schedule(minutes: number): void {
    if (this.timer) clearTimeout(this.timer);
    this.timer = setTimeout(() => this.zone.run(() => this.forceLogout()), minutes * 60_000);
  }

  private forceLogout(): void {
    this.authApi.logout().subscribe();
    this.authState.clearAuth();
  }

  private stop(): void {
    if (this.timer) {
      clearTimeout(this.timer);
      this.timer = null;
    }
    const reset = (this as unknown as { _reset?: () => void })._reset;
    if (reset) {
      this.events.forEach((e) => document.removeEventListener(e, reset));
      delete (this as unknown as { _reset?: () => void })._reset;
    }
  }

  ngOnDestroy(): void {
    this.stop();
  }
}
