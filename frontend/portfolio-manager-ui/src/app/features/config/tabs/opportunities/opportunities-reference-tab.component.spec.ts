import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { IndexConstituent } from '../../../../core/models/opportunities.models';
import { AuthStore } from '../../../../core/store/auth.store';
import { OpportunityReferenceStore } from '../../../../core/store/opportunity-reference.store';
import { OpportunitiesReferenceTabComponent } from './opportunities-reference-tab.component';

const row = (id: number, symbol: string, effectiveTo: string | null = null): IndexConstituent => ({
  id, indexCode: 'TSX60', symbol, companyName: `${symbol} Inc.`, effectiveFrom: '2026-11-12', effectiveTo,
  source: 'src', sourceAsOfDate: '2026-11-12', verifiedAtUtc: null, verifiedBy: null,
});

function setup(dialogResult: unknown) {
  const constituents = signal([row(1, 'AAA.TO'), row(2, 'BBB.TO', '2026-11-16')]);
  const store = {
    constituents,
    currentConstituents: () => constituents().filter((c) => !c.effectiveTo),
    retiredConstituents: () => constituents().filter((c) => !!c.effectiveTo),
    benchmarks: signal([]),
    loading: signal(false),
    error: signal(null),
    saving: signal(false),
    load: vi.fn(),
    retireConstituent: vi.fn(),
    restoreConstituent: vi.fn(),
    addConstituent: vi.fn(),
    upsertBenchmark: vi.fn(),
  };
  const dialog = { open: vi.fn(() => ({ afterClosed: () => of(dialogResult) })) };
  TestBed.configureTestingModule({
    providers: [
      { provide: OpportunityReferenceStore, useValue: store },
      { provide: AuthStore, useValue: { isAdmin: signal(true) } },
      { provide: MatDialog, useValue: dialog },
    ],
  });
  const fixture = TestBed.createComponent(OpportunitiesReferenceTabComponent);
  fixture.detectChanges();
  return { fixture, store, dialog, el: fixture.nativeElement as HTMLElement };
}

describe('OpportunitiesReferenceTabComponent', () => {
  it('lists current constituents by default and offers a retire icon, not a one-click retire', () => {
    const { el } = setup(false);
    const rows = Array.from(el.querySelectorAll('[data-testid="constituents-table"] tr.mat-mdc-row'));
    expect(rows).toHaveLength(1);
    expect(rows[0].textContent).toContain('AAA.TO');
    expect(el.querySelector('[aria-label="Retire AAA.TO"]')).not.toBeNull();
  });

  it('opens a confirmation first and does not retire when it is cancelled', () => {
    const { el, store, dialog } = setup(false);
    (el.querySelector('[aria-label="Retire AAA.TO"]') as HTMLButtonElement).click();
    expect(dialog.open).toHaveBeenCalledTimes(1);
    expect(store.retireConstituent).not.toHaveBeenCalled();
  });

  it('retires only after the confirmation answers yes', () => {
    const { el, store } = setup(true);
    (el.querySelector('[aria-label="Retire AAA.TO"]') as HTMLButtonElement).click();
    expect(store.retireConstituent).toHaveBeenCalledWith({ id: 1, effectiveTo: null });
  });

  it('restores a retired constituent from the Retired view', async () => {
    const { fixture, el, store } = setup(false);
    (el.querySelectorAll('mat-button-toggle button')[1] as HTMLButtonElement).click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    (el.querySelector('[aria-label="Restore BBB.TO"]') as HTMLButtonElement).click();
    expect(store.restoreConstituent).toHaveBeenCalledWith(2);
  });
});
