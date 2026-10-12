import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { GridColumnStore, resolveEditablePrefs, resolveKeys } from './grid-column.store';
import { ColumnPreference, GRID_REGISTRY, GridDef } from './grid-registry';
import { UserPreferencesStore } from './user-preferences.store';

const LS_KEY = 'pm_grid_columns_v1';

const grid: GridDef = {
  id: 'test-grid',
  label: 'Test',
  page: 'Test',
  pageIcon: 'grid_on',
  columns: [
    { key: 'a', label: 'A' },
    { key: 'b', label: 'B' },
    { key: 'c', label: 'C', defaultHidden: true },
    { key: 'actions', label: 'Actions', pinned: true },
  ],
};

describe('resolveKeys', () => {
  it('returns every registered column when nothing is saved', () => {
    expect(resolveKeys(grid, undefined)).toEqual(['a', 'b', 'c', 'actions']);
  });

  it('honours saved order and visibility', () => {
    const prefs: ColumnPreference[] = [
      { key: 'b', visible: true },
      { key: 'c', visible: false },
      { key: 'a', visible: true },
    ];
    expect(resolveKeys(grid, prefs)).toEqual(['b', 'a', 'actions']);
  });

  it('drops columns that no longer exist in the registry', () => {
    const prefs: ColumnPreference[] = [
      { key: 'gone', visible: true },
      { key: 'a', visible: true },
    ];
    expect(resolveKeys(grid, prefs)).toEqual(['a', 'b', 'c', 'actions']);
  });

  it('appends columns added to the registry after the preference was saved', () => {
    const prefs: ColumnPreference[] = [{ key: 'b', visible: true }];
    expect(resolveKeys(grid, prefs)).toEqual(['b', 'a', 'c', 'actions']);
  });

  it('always puts pinned columns last', () => {
    const prefs: ColumnPreference[] = [
      { key: 'actions', visible: true },
      { key: 'a', visible: true },
    ];
    expect(resolveKeys(grid, prefs)).toEqual(['a', 'b', 'c', 'actions']);
  });
});

describe('resolveEditablePrefs', () => {
  it('lists non-pinned columns as visible when nothing is saved', () => {
    expect(resolveEditablePrefs(grid, undefined)).toEqual([
      { key: 'a', visible: true },
      { key: 'b', visible: true },
      { key: 'c', visible: true },
    ]);
  });

  it('keeps hidden columns so they can be toggled back on', () => {
    const prefs: ColumnPreference[] = [
      { key: 'b', visible: false },
      { key: 'a', visible: true },
    ];
    expect(resolveEditablePrefs(grid, prefs)).toEqual([
      { key: 'b', visible: false },
      { key: 'a', visible: true },
      { key: 'c', visible: false },
    ]);
  });

  it('skips removed columns and never returns pinned ones', () => {
    const prefs: ColumnPreference[] = [
      { key: 'gone', visible: true },
      { key: 'actions', visible: true },
      { key: 'a', visible: true },
    ];
    expect(resolveEditablePrefs(grid, prefs).map((p) => p.key)).toEqual(['a', 'b', 'c']);
  });
});

describe('GridColumnStore', () => {
  const dbValue = signal<Record<string, ColumnPreference[]> | null>(null);
  const userPrefs = { get: vi.fn(() => dbValue()), set: vi.fn(), remove: vi.fn() };

  beforeEach(() => {
    localStorage.clear();
    dbValue.set(null);
    userPrefs.set.mockReset();
    userPrefs.remove.mockReset();
    TestBed.configureTestingModule({
      providers: [{ provide: UserPreferencesStore, useValue: userPrefs }],
    });
  });

  function store(): InstanceType<typeof GridColumnStore> {
    const s = TestBed.inject(GridColumnStore);
    TestBed.tick();
    return s;
  }

  it('exposes the registry', () => {
    expect(store().grids).toBe(GRID_REGISTRY);
  });

  it('returns the same cached signal instance per gridId', () => {
    const s = store();
    expect(s.getColumnKeys('watchlist')).toBe(s.getColumnKeys('watchlist'));
    expect(s.getColumnKeys('watchlist')).not.toBe(s.getColumnKeys('scanner'));
  });

  it('returns an empty list for an unknown grid', () => {
    expect(store().getColumnKeys('nope')()).toEqual([]);
  });

  it('reads saved preferences from localStorage on creation', () => {
    localStorage.setItem(
      LS_KEY,
      JSON.stringify({ 'portfolio-cash': [{ key: 'amount', visible: true }] }),
    );

    expect(store().getColumnKeys('portfolio-cash')()).toEqual([
      'amount',
      'description',
      'addedAt',
      'cashAccountType',
      'cashActions',
      'colConfig',
    ]);
  });

  it('writes updates to localStorage and through to the DB', () => {
    const s = store();
    const prefs: ColumnPreference[] = [
      { key: 'amount', visible: true },
      { key: 'description', visible: false },
    ];

    s.updatePrefs('portfolio-cash', prefs);

    expect(s.getColumnKeys('portfolio-cash')()).toEqual([
      'amount',
      'addedAt',
      'cashAccountType',
      'cashActions',
      'colConfig',
    ]);
    expect(JSON.parse(localStorage.getItem(LS_KEY)!)).toEqual({ 'portfolio-cash': prefs });
    expect(userPrefs.set).toHaveBeenCalledWith(LS_KEY, { 'portfolio-cash': prefs });
  });

  it('resetGrid restores defaults for one grid only', () => {
    const s = store();
    s.updatePrefs('portfolio-cash', [{ key: 'amount', visible: true }]);
    s.updatePrefs('watchlist', [{ key: 'symbol', visible: true }]);

    s.resetGrid('portfolio-cash');

    expect(s.getColumnKeys('portfolio-cash')()).toEqual(
      GRID_REGISTRY.find((g) => g.id === 'portfolio-cash')!.columns.map((c) => c.key),
    );
    expect(JSON.parse(localStorage.getItem(LS_KEY)!)).toEqual({
      watchlist: [{ key: 'symbol', visible: true }],
    });
  });

  it('resetAll clears storage and the DB key', () => {
    const s = store();
    s.updatePrefs('portfolio-cash', [{ key: 'amount', visible: true }]);

    s.resetAll();

    expect(localStorage.getItem(LS_KEY)).toBeNull();
    expect(userPrefs.remove).toHaveBeenCalledWith(LS_KEY);
    expect(s.getColumnKeys('portfolio-cash')()).toEqual(
      GRID_REGISTRY.find((g) => g.id === 'portfolio-cash')!.columns.map((c) => c.key),
    );
  });

  it('lets DB preferences overlay localStorage once they load', () => {
    localStorage.setItem(
      LS_KEY,
      JSON.stringify({ 'portfolio-cash': [{ key: 'amount', visible: true }] }),
    );
    const s = store();
    expect(s.getColumnKeys('portfolio-cash')()[0]).toBe('amount');

    dbValue.set({ 'portfolio-cash': [{ key: 'addedAt', visible: true }] });
    TestBed.tick();
    expect(s.getColumnKeys('portfolio-cash')()[0]).toBe('addedAt');
  });

  it('getEditablePrefs excludes pinned columns', () => {
    const keys = store()
      .getEditablePrefs('portfolio-cash')
      .map((p) => p.key);
    expect(keys).toEqual(['description', 'amount', 'addedAt', 'cashAccountType']);
  });
});
