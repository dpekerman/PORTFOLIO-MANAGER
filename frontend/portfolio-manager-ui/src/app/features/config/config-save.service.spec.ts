import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of, throwError } from 'rxjs';
import { ConfigSaveService, ConfigSection } from './config-save.service';

describe('ConfigSaveService', () => {
  let service: ConfigSaveService;
  let snackBar: { open: ReturnType<typeof vi.fn> };
  const destroyRef = { onDestroy: vi.fn() } as never;

  const section = (id: string, dirty: boolean, save: ConfigSection['save']): ConfigSection => ({
    id,
    dirty: signal(dirty),
    save,
  });

  beforeEach(() => {
    snackBar = { open: vi.fn() };
    TestBed.configureTestingModule({ providers: [{ provide: MatSnackBar, useValue: snackBar }] });
    service = TestBed.inject(ConfigSaveService);
  });

  it('saves only dirty sections and reports success', () => {
    const dirtySave = vi.fn(() => of(null));
    const cleanSave = vi.fn(() => of(null));
    service.register(section('a', true, dirtySave), destroyRef);
    service.register(section('b', false, cleanSave), destroyRef);

    expect(service.dirtyCount()).toBe(1);
    service.saveAll();

    expect(dirtySave).toHaveBeenCalledTimes(1);
    expect(cleanSave).not.toHaveBeenCalled();
    expect(snackBar.open).toHaveBeenCalledWith('All configuration settings saved.', 'OK', {
      duration: 4000,
    });
    expect(service.saving()).toBe(false);
  });

  it('reports failed sections instead of claiming success', () => {
    service.register(
      section('Scanner', true, () => throwError(() => new Error('x'))),
      destroyRef,
    );
    service.register(
      section('EOD', true, () => of(null)),
      destroyRef,
    );

    service.saveAll();

    expect(snackBar.open).toHaveBeenCalledWith('Could not save: Scanner', 'Dismiss', {
      duration: 6000,
    });
  });

  it('tells the user when there is nothing to save', () => {
    service.register(
      section('a', false, () => of(null)),
      destroyRef,
    );
    service.saveAll();
    expect(snackBar.open).toHaveBeenCalledWith('No pending changes to save.', 'OK', {
      duration: 3000,
    });
  });

  it('resets every section that supports it', () => {
    const reset = vi.fn();
    service.register({ ...section('a', false, () => of(null)), reset }, destroyRef);
    service.register(
      section('b', false, () => of(null)),
      destroyRef,
    );
    service.resetAll();
    expect(reset).toHaveBeenCalledTimes(1);
  });
});
