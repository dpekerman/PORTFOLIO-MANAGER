import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { vi } from 'vitest';
import { AccountType } from '../models/portfolio.models';
import { AccountTypesApiService } from './account-types-api.service';
import { AccountTypesStateService } from './account-types-state.service';
import { AuthStateService } from './auth-state.service';

const item: AccountType = {
  id: 1,
  name: 'Original',
  version: 'v1',
  stockCount: 1,
  optionCount: 0,
  cashCount: 1,
};

describe('AccountTypesStateService', () => {
  const authenticated = signal(true);
  const currentUser = signal<{ id: string } | null>({ id: 'admin' });
  const api = {
    getAll: vi.fn(() => of([item])),
    add: vi.fn(() => of({ ...item, id: 2, name: 'New' })),
    rename: vi.fn(() =>
      of({ oldName: item.name, item: { ...item, name: 'Renamed', version: 'v2' } }),
    ),
    delete: vi.fn(() => of(undefined)),
  };

  beforeEach(() => {
    authenticated.set(true);
    currentUser.set({ id: 'admin' });
    api.getAll.mockReset().mockReturnValue(of([item]));
    api.add.mockReset().mockReturnValue(of({ ...item, id: 2, name: 'New' }));
    api.rename
      .mockReset()
      .mockReturnValue(
        of({ oldName: item.name, item: { ...item, name: 'Renamed', version: 'v2' } }),
      );
    api.delete.mockReset().mockReturnValue(of(undefined));
    TestBed.configureTestingModule({
      providers: [
        { provide: AccountTypesApiService, useValue: api },
        { provide: AuthStateService, useValue: { isAuthenticated: authenticated, currentUser } },
      ],
    });
  });

  function state(): AccountTypesStateService {
    const state = TestBed.inject(AccountTypesStateService);
    TestBed.tick();
    return state;
  }

  it('loads after auth without a signal-driven request loop', () => {
    const s = state();
    TestBed.tick();
    expect(s.names()).toEqual(['Original']);
    expect(s.ready()).toBe(true);
    expect(api.getAll).toHaveBeenCalledOnce();
  });

  it('uses committed server results and publishes rename events', async () => {
    const s = state();
    await s.add('New');
    expect(s.names()).toEqual(['Original', 'New']);
    await s.rename(item, 'Renamed');
    expect(s.names()).toEqual(['Renamed', 'New']);
    expect(s.renamed()).toEqual({ oldName: 'Original', newName: 'Renamed' });
    await s.delete({ ...item, id: 2, name: 'New' });
    expect(s.names()).toEqual(['Renamed']);
  });

  it('retains choices and surfaces a failed write', async () => {
    const s = state();
    api.add.mockReturnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 409,
            error: { detail: 'Duplicate account type.' },
          }),
      ),
    );
    await expect(s.add('Original')).rejects.toBeDefined();
    expect(s.names()).toEqual(['Original']);
    expect(s.error()).toBe('Duplicate account type.');
    expect(s.saving()).toBe(false);
  });

  it('does not replace the catalog with defaults on load failure', () => {
    const s = state();
    api.getAll.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    s.load();
    expect(s.names()).toEqual(['Original']);
    expect(s.ready()).toBe(false);
    expect(s.error()).toBeTruthy();
  });

  it('clears state on logout and ignores a delayed response', () => {
    const s = state();
    const response = new Subject<AccountType[]>();
    api.getAll.mockReturnValue(response);
    s.load();
    authenticated.set(false);
    currentUser.set(null);
    TestBed.tick();
    response.next([item]);
    expect(s.items()).toEqual([]);
    expect(s.ready()).toBe(false);
  });
});
