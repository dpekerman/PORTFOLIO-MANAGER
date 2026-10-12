import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { defer, of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { AuthApiService } from '../services/auth-api.service';
import { AuthStore } from './auth.store';

const rateLimited = () => throwError(() => new HttpErrorResponse({ status: 429 }));

describe('AuthStore.initializeAuth', () => {
  let checkSetupRequired: ReturnType<typeof vi.fn>;
  let refreshToken: ReturnType<typeof vi.fn>;
  const navigate = vi.fn();
  const auth = { accessToken: 't', user: { roles: ['Admin'] } };

  const createState = () => {
    const state = TestBed.inject(AuthStore);
    state.setRateLimitBackoffMs(1);
    return state;
  };

  beforeEach(() => {
    navigate.mockReset();
    checkSetupRequired = vi.fn(() => of({ required: false }));
    refreshToken = vi.fn(() => of(auth));
    TestBed.configureTestingModule({
      providers: [
        AuthStore,
        { provide: AuthApiService, useValue: { checkSetupRequired, refreshToken } },
        { provide: Router, useValue: { navigate } },
      ],
    });
  });

  it('retries a rate-limited (429) refresh instead of treating it as a logout', async () => {
    let calls = 0;
    refreshToken.mockImplementation(() => defer(() => (++calls < 3 ? rateLimited() : of(auth))));
    const state = createState();

    await state.initializeAuth();

    expect(calls).toBe(3);
    expect(state.isAuthenticated()).toBe(true);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('does not retry a genuine auth failure', async () => {
    refreshToken.mockImplementation(() => throwError(() => new HttpErrorResponse({ status: 401 })));
    const state = createState();

    await state.initializeAuth();

    expect(refreshToken).toHaveBeenCalledOnce();
    expect(state.isAuthenticated()).toBe(false);
  });

  it('gives up once the retries are exhausted', async () => {
    let calls = 0;
    refreshToken.mockImplementation(() =>
      defer(() => {
        calls++;
        return rateLimited();
      }),
    );
    const state = createState();

    await state.initializeAuth();

    expect(calls).toBe(4);
    expect(state.isAuthenticated()).toBe(false);
  });

  it('retries a rate-limited setup check before deciding to send the user to login', async () => {
    let calls = 0;
    checkSetupRequired.mockImplementation(() =>
      defer(() => (++calls < 2 ? rateLimited() : of({ required: false }))),
    );
    const state = createState();

    await state.initializeAuth();

    expect(calls).toBe(2);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('records the setup check result and stops before refreshing when setup is required', async () => {
    checkSetupRequired.mockReturnValue(of({ required: true }));
    const state = createState();

    await state.initializeAuth();

    expect(state.setupRequired()).toBe(true);
    expect(refreshToken).not.toHaveBeenCalled();
  });

  it('derives roles from the signed-in user and clears them on clearAuth', () => {
    const state = createState();
    expect(state.canWrite()).toBe(false);

    state.setAuth({ accessToken: 't', user: { roles: ['Trader'] } } as never);
    expect(state.isAuthenticated()).toBe(true);
    expect(state.isTrader()).toBe(true);
    expect(state.isAdmin()).toBe(false);
    expect(state.canWrite()).toBe(true);

    state.clearAuth();
    expect(state.isAuthenticated()).toBe(false);
    expect(state.currentUser()).toBeNull();
  });
});
