import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl } from '@angular/forms';
import { vi } from 'vitest';
import { AccountTypesStateService } from '../../core/services/account-types-state.service';
import { AccountTypeSelectComponent } from './account-type-select.component';

describe('Shared account selector', () => {
  const names = signal(['Old']);
  const ready = signal(true);
  const loading = signal(false);
  const error = signal<string | null>(null);
  const load = vi.fn();
  beforeEach(() => {
    names.set(['Old']);
    ready.set(true);
    loading.set(false);
    error.set(null);
    load.mockReset();
    TestBed.configureTestingModule({
      providers: [
        {
          provide: AccountTypesStateService,
          useValue: { names, ready, loading, error, load },
        },
      ],
    });
  });

  it('preserves a stored selection through loading/errors and offers retry', async () => {
    const fixture = TestBed.createComponent(AccountTypeSelectComponent);
    fixture.componentInstance.writeValue('Old');
    loading.set(true);
    ready.set(false);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(fixture.componentInstance.validate(new FormControl('Old'))).toEqual({
      accountTypesUnavailable: true,
    });
    loading.set(false);
    error.set('Load failed');
    fixture.detectChanges();
    await fixture.whenStable();
    const element: HTMLElement = fixture.nativeElement;
    expect(element.textContent).toContain('Old');
    expect(element.textContent).toContain('Load failed');
    element.querySelector('button')!.click();
    expect(load).toHaveBeenCalledTimes(2);
  });

  it('validates newly shared choices, null, and stale renamed selections', () => {
    const fixture = TestBed.createComponent(AccountTypeSelectComponent);
    const changed = vi.fn();
    fixture.componentInstance.registerOnValidatorChange(changed);
    fixture.detectChanges();
    expect(fixture.componentInstance.validate(new FormControl(null))).toBeNull();
    names.set(['New']);
    fixture.detectChanges();
    expect(changed).toHaveBeenCalled();
    expect(fixture.componentInstance.validate(new FormControl('New'))).toBeNull();
    expect(fixture.componentInstance.validate(new FormControl('Old'))).toEqual({
      accountTypeStale: true,
    });
  });
});
