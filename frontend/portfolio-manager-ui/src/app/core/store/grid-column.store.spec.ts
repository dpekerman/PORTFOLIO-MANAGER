import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { BreakpointStore } from './breakpoint.store';
import { GridColumnStore, resolveEditablePrefs, resolveKeys } from './grid-column.store';
import { ColumnPreference, GRID_REGISTRY, GridDef, resolveHandsetKeys } from './grid-registry';
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

/** Same grid with a responsive priority on `a` plus the config gear column. */
const responsiveGrid: GridDef = {
  ...grid,
  columns: [
    { key: 'a', label: 'A', priority: 'primary' },
    { key: 'b', label: 'B' },
    { key: 'c', label: 'C', priority: 'secondary' },
    { key: 'actions', label: 'Actions', pinned: true },
    { key: 'colConfig', label: '', pinned: true },
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

describe('resolveHandsetKeys', () => {
  const visible = ['a', 'b', 'c', 'actions', 'colConfig'];

  it('keeps primary and pinned columns and drops the config column', () => {
    expect(resolveHandsetKeys(responsiveGrid, visible)).toEqual(['a', 'actions']);
  });

  it('returns every visible column except the config one when showAll is set', () => {
    expect(resolveHandsetKeys(responsiveGrid, visible, true)).toEqual(['a', 'b', 'c', 'actions']);
  });

  it('falls back to all visible columns when the grid declares no primary column', () => {
    expect(resolveHandsetKeys(grid, ['a', 'b', 'actions'])).toEqual(['a', 'b', 'actions']);
  });

  it('never resurrects a column the user hid on desktop', () => {
    expect(resolveHandsetKeys(responsiveGrid, ['b', 'actions'])).toEqual(['actions']);
  });

  it('preserves the user-configured order', () => {
    const reordered = { ...responsiveGrid };
    reordered.columns = [{ key: 'b', label: 'B', priority: 'primary' }, ...responsiveGrid.columns];
    expect(resolveHandsetKeys(reordered, ['b', 'a', 'actions'])).toEqual(['b', 'a', 'actions']);
  });
});

describe('GridColumnStore', () => {
  const dbValue = signal<Record<string, ColumnPreference[]> | null>(null);
  const userPrefs = { get: vi.fn(() => dbValue()), set: vi.fn(), remove: vi.fn() };
  const isHandset = signal(false);

  beforeEach(() => {
    localStorage.clear();
    dbValue.set(null);
    isHandset.set(false);
    userPrefs.set.mockReset();
    userPrefs.remove.mockReset();
    TestBed.configureTestingModule({
      providers: [
        { provide: UserPreferencesStore, useValue: userPrefs },
        {
          provide: BreakpointStore,
          useValue: {
            isHandset,
            isTablet: signal(false),
            isDesktop: signal(true),
          },
        },
      ],
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

  describe('getResponsiveColumnKeys', () => {
    const GRID = 'transactions-stocks';

    it('returns the same cached signal instance per gridId', () => {
      const s = store();
      expect(s.getResponsiveColumnKeys(GRID)).toBe(s.getResponsiveColumnKeys(GRID));
      expect(s.getResponsiveColumnKeys(GRID)).not.toBe(s.getResponsiveColumnKeys('watchlist'));
    });

    it('is identical to the desktop keys on a wide viewport', () => {
      const s = store();
      for (const g of GRID_REGISTRY) {
        expect(s.getResponsiveColumnKeys(g.id)()).toEqual(s.getColumnKeys(g.id)());
      }
    });

    it('collapses to primary + pinned columns on a handset', () => {
      const s = store();
      isHandset.set(true);

      expect(s.getResponsiveColumnKeys(GRID)()).toEqual([
        'tx_type',
        'tx_symbol',
        'tx_shares',
        'tx_avg_cost',
        'tx_open_date',
        'tx_gain_loss',
        'tx_actions',
      ]);
    });

    it('falls back to every visible column for a grid without priorities', () => {
      const s = store();
      isHandset.set(true);

      const expected = s
        .getColumnKeys('portfolio-cash')()
        .filter((k) => k !== 'colConfig');
      expect(s.getResponsiveColumnKeys('portfolio-cash')()).toEqual(expected);
    });

    it('reveals every visible column when showAllColumnsOnHandset is on', () => {
      const s = store();
      isHandset.set(true);
      s.setShowAllColumnsOnHandset(true);

      expect(s.getResponsiveColumnKeys(GRID)()).toEqual(
        s
          .getColumnKeys(GRID)()
          .filter((k) => k !== 'colConfig'),
      );

      s.setShowAllColumnsOnHandset(false);
      expect(s.getResponsiveColumnKeys(GRID)()).toHaveLength(7);
    });

    it('honours columns the user hid on desktop', () => {
      const s = store();
      s.updatePrefs(GRID, [
        { key: 'tx_symbol', visible: false },
        { key: 'tx_type', visible: true },
      ]);
      isHandset.set(true);

      expect(s.getResponsiveColumnKeys(GRID)()).not.toContain('tx_symbol');
      expect(s.getResponsiveColumnKeys(GRID)()).toContain('tx_type');
    });

    it('never writes saved preferences, localStorage or the DB', () => {
      const s = store();
      const before = JSON.stringify(localStorage.getItem(LS_KEY));
      isHandset.set(true);

      s.getResponsiveColumnKeys(GRID)();
      s.setShowAllColumnsOnHandset(true);
      s.getResponsiveColumnKeys(GRID)();

      expect(JSON.stringify(localStorage.getItem(LS_KEY))).toBe(before);
      expect(userPrefs.set).not.toHaveBeenCalled();
      expect(s.prefs()).toEqual({});
      // ...and the desktop projection is untouched once the viewport grows again
      isHandset.set(false);
      expect(s.getResponsiveColumnKeys(GRID)()).toEqual(s.getColumnKeys(GRID)());
    });

    it('returns an empty list for an unknown grid', () => {
      const s = store();
      isHandset.set(true);
      expect(s.getResponsiveColumnKeys('nope')()).toEqual([]);
    });
  });
});
