import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { patchState, signalStore, withState } from '@ngrx/signals';
import { unprotected } from '@ngrx/signals/testing';
import { vi } from 'vitest';
import { AuthStore } from '../auth.store';
import { onceAuthenticated } from './auth-gate';
import { setError, setLoaded, setLoading, withRequestStatus } from './request-status.feature';
import { withStorageSync } from './storage-sync.feature';

describe('withRequestStatus', () => {
  const Store = signalStore({ providedIn: 'root' }, withRequestStatus());

  it('starts idle and transitions through the shared updaters', () => {
    const store = TestBed.inject(Store);
    const state = unprotected(store);
    expect(store.loading()).toBe(false);
    expect(store.error()).toBeNull();

    patchState(state, setLoading());
    expect(store.loading()).toBe(true);

    patchState(state, setError('nope'));
    expect(store.loading()).toBe(false);
    expect(store.error()).toBe('nope');

    patchState(state, setLoading());
    expect(store.error()).toBeNull();

    patchState(state, setLoaded());
    expect(store.loading()).toBe(false);
  });
});

describe('onceAuthenticated', () => {
  const authenticated = signal(false);

  it('fires exactly once, on the first authentication', () => {
    const callback = vi.fn();
    TestBed.configureTestingModule({
      providers: [{ provide: AuthStore, useValue: { isAuthenticated: authenticated } }],
    });
    TestBed.runInInjectionContext(() => onceAuthenticated(callback));

    TestBed.tick();
    expect(callback).not.toHaveBeenCalled();

    authenticated.set(true);
    TestBed.tick();
    authenticated.set(false);
    TestBed.tick();
    authenticated.set(true);
    TestBed.tick();

    expect(callback).toHaveBeenCalledTimes(1);
  });
});

describe('withStorageSync', () => {
  it('writes the initial state and every later change', () => {
    const write = vi.fn();
    const Store = signalStore(
      { providedIn: 'root' },
      withState({ theme: 'dark' }),
      withStorageSync<{ theme: string }>(write),
    );
    const store = TestBed.inject(Store);

    expect(write).toHaveBeenLastCalledWith({ theme: 'dark' });

    patchState(unprotected(store), { theme: 'light' });
    expect(write).toHaveBeenLastCalledWith({ theme: 'light' });
  });
});
