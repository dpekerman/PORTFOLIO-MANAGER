import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AccountTypesStateService } from '../../../core/services/account-types-state.service';
import { AuthStateService } from '../../../core/services/auth-state.service';
import { AccountTypesComponent } from './account-types.component';

describe('Account Types configuration', () => {
  const admin = signal(true);
  const state = {
    items: signal([
      { id: 1, name: 'Used', version: 'v1', stockCount: 1, optionCount: 0, cashCount: 0 },
    ]),
    loading: signal(false),
    saving: signal(false),
    error: signal<string | null>(null),
    ready: signal(true),
    load: vi.fn(),
    add: vi.fn(async () => {}),
    rename: vi.fn(async () => {}),
    delete: vi.fn(async () => {}),
  };
  const open = vi.fn(() => ({ afterClosed: () => of(true) }));
  const snack = vi.fn();

  beforeEach(() => {
    admin.set(true);
    state.add.mockReset().mockResolvedValue(undefined);
    state.rename.mockReset().mockResolvedValue(undefined);
    open.mockClear();
    snack.mockClear();
    TestBed.configureTestingModule({
      providers: [
        { provide: AccountTypesStateService, useValue: state },
        { provide: AuthStateService, useValue: { isAdmin: admin } },
        { provide: MatDialog, useValue: { open } },
        { provide: MatSnackBar, useValue: { open: snack } },
      ],
    });
  });

  it('shows a read-only shared list to non-Admins', () => {
    admin.set(false);
    const fixture = TestBed.createComponent(AccountTypesComponent);
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    expect(element.querySelector('form')).toBeNull();
    expect(element.textContent).toContain('Only an Admin');
    expect(element.textContent).toContain('Used');
  });

  it('explains and disables deletion of used accounts', () => {
    const fixture = TestBed.createComponent(AccountTypesComponent);
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    const button = element.querySelector<HTMLButtonElement>('button[aria-label="Delete Used"]')!;
    expect(button.disabled).toBe(true);
  });

  it('sorts by clicking column headers', () => {
    state.items.set([
      { id: 1, name: 'Beta', version: 'v1', stockCount: 1, optionCount: 0, cashCount: 0 },
      { id: 2, name: 'Alpha', version: 'v2', stockCount: 5, optionCount: 0, cashCount: 0 },
    ]);
    const fixture = TestBed.createComponent(AccountTypesComponent);
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    const names = () =>
      Array.from(element.querySelectorAll('.acct-name')).map((x) => x.textContent?.trim());
    expect(names()).toEqual(['Alpha', 'Beta']);
    const header = (label: string) =>
      Array.from(element.querySelectorAll<HTMLButtonElement>('.acct-sort-btn')).find((x) =>
        x.textContent?.includes(label),
      )!;
    header('Name').click();
    fixture.detectChanges();
    expect(names()).toEqual(['Beta', 'Alpha']);
    header('Stocks').click();
    fixture.detectChanges();
    expect(names()).toEqual(['Beta', 'Alpha']);
    header('Stocks').click();
    fixture.detectChanges();
    expect(names()).toEqual(['Alpha', 'Beta']);
    state.items.set([
      { id: 1, name: 'Used', version: 'v1', stockCount: 1, optionCount: 0, cashCount: 0 },
    ]);
  });

  it('keeps the draft and reports errors when adding fails', async () => {
    state.add.mockRejectedValue(
      new HttpErrorResponse({ status: 409, error: { detail: 'Duplicate name.' } }),
    );
    const fixture = TestBed.createComponent(AccountTypesComponent);
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    element.querySelector<HTMLButtonElement>('button[aria-label="Add account type"]')!.click();
    fixture.detectChanges();
    const input = element.querySelector('input')!;
    input.value = 'Draft';
    input.dispatchEvent(new Event('input'));
    element
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await fixture.whenStable();
    expect(input.value).toBe('Draft');
    expect(snack).toHaveBeenCalledWith('Duplicate name.', 'Dismiss', expect.any(Object));
    expect(snack).not.toHaveBeenCalledWith(
      'Account types saved.',
      expect.anything(),
      expect.anything(),
    );
  });

  it('confirms global rename before saving', async () => {
    const fixture = TestBed.createComponent(AccountTypesComponent);
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    element.querySelector<HTMLButtonElement>('button[aria-label="Rename Used"]')!.click();
    fixture.detectChanges();
    const input = element.querySelector('input')!;
    input.value = 'After';
    input.dispatchEvent(new Event('input'));
    element
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await fixture.whenStable();
    expect(open).toHaveBeenCalledWith(
      expect.anything(),
      expect.objectContaining({
        data: expect.objectContaining({ message: expect.stringContaining('for all users') }),
      }),
    );
    expect(state.rename).toHaveBeenCalledWith(state.items()[0], 'After');
  });
});
