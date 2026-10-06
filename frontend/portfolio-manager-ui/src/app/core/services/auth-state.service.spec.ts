import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { defer, of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { AuthApiService } from './auth-api.service';
import { AuthStateService } from './auth-state.service';

const rateLimited = () => throwError(() => new HttpErrorResponse({ status: 429 }));

describe('AuthStateService.initializeAuth', () => {
  let checkSetupRequired: ReturnType<typeof vi.fn>;
  let refreshToken: ReturnType<typeof vi.fn>;
  const navigate = vi.fn();
  const auth = { accessToken: 't', user: { roles: ['Admin'] } };

  const createState = () => {
    const state = TestBed.inject(AuthStateService);
    state.rateLimitBackoffMs = 1;
    return state;
  };

  beforeEach(() => {
    navigate.mockReset();
    checkSetupRequired = vi.fn(() => of({ required: false }));
    refreshToken = vi.fn(() => of(auth));
    TestBed.configureTestingModule({
      providers: [
        AuthStateService,
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
});
