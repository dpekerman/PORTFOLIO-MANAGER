import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Subject, of, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { IndexConstituent, OpportunityReference } from '../models/opportunities.models';
import { OpportunitiesApiService } from '../services/opportunities-api.service';
import { OpportunityReferenceStore, referenceErrorMessage } from './opportunity-reference.store';

const constituent = (id: number, symbol: string, effectiveTo: string | null = null): IndexConstituent => ({
  id,
  indexCode: 'TSX60',
  symbol,
  companyName: symbol,
  effectiveFrom: '2026-01-02',
  effectiveTo,
  source: 'Fixture source',
  sourceAsOfDate: '2026-11-01',
  verifiedAtUtc: null,
  verifiedBy: null,
});

function setup(api: Partial<Record<keyof OpportunitiesApiService, unknown>>) {
  const snack = { open: vi.fn() };
  TestBed.configureTestingModule({
    providers: [
      { provide: OpportunitiesApiService, useValue: api },
      { provide: MatSnackBar, useValue: snack },
    ],
  });
  return { store: TestBed.inject(OpportunityReferenceStore), snack };
}

describe('OpportunityReferenceStore', () => {
  it('loads and splits current and retired constituents', () => {
    const ref: OpportunityReference = { constituents: [constituent(1, 'AAA.TO'), constituent(2, 'BBB.TO', '2026-06-30')], benchmarks: [] };
    const { store } = setup({ getReference: () => of(ref) });
    store.load();
    expect(store.loading()).toBe(false);
    expect(store.currentConstituents().map((c) => c.symbol)).toEqual(['AAA.TO']);
    expect(store.retiredConstituents().map((c) => c.symbol)).toEqual(['BBB.TO']);
  });

  it('retiring replaces the row in place (history kept, nothing deleted)', () => {
    const retired$ = new Subject<IndexConstituent>();
    const { store } = setup({
      getReference: () => of({ constituents: [constituent(1, 'AAA.TO')], benchmarks: [] }),
      retireConstituent: () => retired$,
    });
    store.load();
    store.retireConstituent({ id: 1, effectiveTo: null });
    expect(store.saving()).toBe(true);
    retired$.next(constituent(1, 'AAA.TO', '2026-11-14'));
    expect(store.saving()).toBe(false);
    expect(store.constituents()).toHaveLength(1);
    expect(store.currentConstituents()).toHaveLength(0);
  });

  it('offers Undo after a retire, and undo restores the row', () => {
    const undo$ = new Subject<void>();
    const { store, snack } = setup({
      getReference: () => of({ constituents: [constituent(1, 'AAA.TO')], benchmarks: [] }),
      retireConstituent: () => of(constituent(1, 'AAA.TO', '2026-11-16')),
      restoreConstituent: () => of(constituent(1, 'AAA.TO')),
    });
    snack.open.mockReturnValue({ onAction: () => undo$ });
    store.load();
    store.retireConstituent({ id: 1, effectiveTo: null });
    expect(snack.open).toHaveBeenCalledWith(expect.stringContaining('retired'), 'Undo', expect.anything());
    expect(store.currentConstituents()).toHaveLength(0);

    undo$.next();
    expect(store.currentConstituents()).toHaveLength(1);
    expect(store.saving()).toBe(false);
  });

  it('reports a server conflict without changing state', () => {
    const conflict = new HttpErrorResponse({ status: 409, error: { title: 'Already a current constituent' } });
    const { store, snack } = setup({
      getReference: () => of({ constituents: [constituent(1, 'AAA.TO')], benchmarks: [] }),
      addConstituent: () => throwError(() => conflict),
    });
    store.load();
    store.addConstituent({ symbol: 'AAA.TO', companyName: null, indexCode: null, effectiveFrom: '2026-01-02', source: 's', sourceAsOfDate: '2026-11-01' });
    expect(store.constituents()).toHaveLength(1);
    expect(store.saving()).toBe(false);
    expect(snack.open).toHaveBeenCalledWith('Already a current constituent', 'Dismiss', expect.anything());
  });

  it('maps forbidden and validation errors to readable messages', () => {
    expect(referenceErrorMessage(new HttpErrorResponse({ status: 403 }), 'x')).toContain('administrators');
    expect(
      referenceErrorMessage(new HttpErrorResponse({ status: 400, error: { errors: { '': ['Symbol and Source are required.'] } } }), 'x'),
    ).toBe('Symbol and Source are required.');
    expect(referenceErrorMessage(new Error('boom'), 'fallback')).toBe('fallback');
  });
});
