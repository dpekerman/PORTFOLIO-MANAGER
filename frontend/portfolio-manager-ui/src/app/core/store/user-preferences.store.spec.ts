import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { AuthStore } from './auth.store';
import { UserPreferencesApiService } from '../services/user-preferences-api.service';
import { UserPreferencesStore } from './user-preferences.store';

describe('UserPreferencesStore', () => {
  const accessToken = signal<string | null>(null);
  const isAuthenticated = signal(false);
  const api = {
    getAll: vi.fn(),
    upsert: vi.fn(() => of(undefined)),
    delete: vi.fn(() => of(undefined)),
  };

  beforeEach(() => {
    accessToken.set(null);
    isAuthenticated.set(false);
    vi.clearAllMocks();
    api.getAll.mockReturnValue(of({ a: '{"x":1}', bad: '{nope' }));
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthStore, useValue: { accessToken, isAuthenticated } },
        { provide: UserPreferencesApiService, useValue: api },
      ],
    });
  });

  function login(store: InstanceType<typeof UserPreferencesStore>) {
    accessToken.set('tok');
    isAuthenticated.set(true);
    TestBed.tick();
    return store;
  }

  it('loads preferences on login and parses JSON values', () => {
    const store = TestBed.inject(UserPreferencesStore);
    TestBed.tick();
    expect(store.isLoaded()).toBe(false);
    login(store);
    expect(store.isLoaded()).toBe(true);
    expect(store.get<{ x: number }>('a')).toEqual({ x: 1 });
    expect(store.get('bad')).toBeNull();
    expect(store.get('missing')).toBeNull();
  });

  it('clears preferences on logout', () => {
    const store = login(TestBed.inject(UserPreferencesStore));
    isAuthenticated.set(false);
    TestBed.tick();
    expect(store.prefs()).toEqual({});
    expect(store.isLoaded()).toBe(false);
  });

  it('set and remove update local state and the API', () => {
    const store = TestBed.inject(UserPreferencesStore);
    store.set('k', { v: 2 });
    expect(store.get('k')).toEqual({ v: 2 });
    expect(api.upsert).toHaveBeenCalledWith('k', '{"v":2}');
    store.remove('k');
    expect(store.get('k')).toBeNull();
    expect(api.delete).toHaveBeenCalledWith('k');
  });

  it('keeps local state when the API write fails', () => {
    api.upsert.mockReturnValueOnce(throwError(() => new Error('down')) as never);
    const store = TestBed.inject(UserPreferencesStore);
    store.set('k', 1);
    expect(store.get('k')).toBe(1);
  });

  it('reload refreshes from the API and stays not-loaded on failure', () => {
    const store = TestBed.inject(UserPreferencesStore);
    api.getAll.mockReturnValueOnce(throwError(() => new Error('down')));
    store.reload();
    expect(store.isLoaded()).toBe(false);
    store.reload();
    expect(store.isLoaded()).toBe(true);
  });
});
