import { Signal, computed, effect, inject } from '@angular/core';
import { patchState, signalStore, withHooks, withMethods, withProps, withState } from '@ngrx/signals';
import { ColumnDef, ColumnPreference, GRID_REGISTRY, GridDef } from './grid-registry';
import { UserPreferencesStore } from './user-preferences.store';

const LS_KEY = 'pm_grid_columns_v1';

/** Saved column order/visibility for every grid the user has customised, keyed by grid id. */
type StoredPrefs = Record<string, ColumnPreference[]>;

interface GridColumnState {
  prefs: StoredPrefs;
}

// ── Pure resolution helpers ───────────────────────────────────────────────────
// Kept free of Angular and of store state so they stay cheap to unit test and easy to extend
// (a later phase adds a responsive `priority` to ColumnDef and resolves per breakpoint here).

/** Non-pinned columns, in registry order. Pinned columns are never user-orderable. */
function nonPinnedColumns(gridDef: GridDef): ColumnDef[] {
  return gridDef.columns.filter((c) => !c.pinned);
}

/**
 * Visible column keys for one grid, in user-configured order.
 *
 * With no saved preference every registered column shows. Otherwise saved order and visibility
 * win, columns that have since been removed from the registry are dropped, columns added to the
 * registry since the preference was saved are appended (forward-compat), and pinned columns are
 * always appended last.
 */
export function resolveKeys(gridDef: GridDef, prefs: ColumnPreference[] | undefined): string[] {
  if (!prefs) {
    // No customisation — return all defaults
    return gridDef.columns.map((c) => c.key);
  }

  const pinnedKeys = gridDef.columns.filter((c) => c.pinned).map((c) => c.key);
  const nonPinned = nonPinnedColumns(gridDef);
  const validKeys = new Set(nonPinned.map((c) => c.key));
  const prefKeys = new Set(prefs.map((p) => p.key));
  const result: string[] = [];

  // Restore saved order & visibility
  for (const pref of prefs) {
    if (validKeys.has(pref.key) && pref.visible) {
      result.push(pref.key);
    }
  }

  // Forward-compat: append any NEW default columns not yet in storage
  for (const col of nonPinned) {
    if (!prefKeys.has(col.key)) {
      result.push(col.key);
    }
  }

  // Pinned columns always come last
  result.push(...pinnedKeys);

  return result;
}

/**
 * Editable (non-pinned) preferences for one grid, including hidden columns so the config dialog
 * can toggle them back on. New registry columns are appended honouring their `defaultHidden`.
 */
export function resolveEditablePrefs(
  gridDef: GridDef,
  prefs: ColumnPreference[] | undefined,
): ColumnPreference[] {
  const nonPinned = nonPinnedColumns(gridDef);

  if (!prefs) {
    return nonPinned.map((c) => ({ key: c.key, visible: true }));
  }

  const validKeys = new Set(nonPinned.map((c) => c.key));
  const result: ColumnPreference[] = [];

  // Respect stored order; skip removed columns
  for (const pref of prefs) {
    if (validKeys.has(pref.key)) {
      result.push({ ...pref });
    }
  }

  // Append any NEW default columns not yet in storage (forward-compat)
  const prefKeys = new Set(prefs.map((p) => p.key));
  for (const col of nonPinned) {
    if (!prefKeys.has(col.key)) {
      result.push({ key: col.key, visible: !col.defaultHidden });
    }
  }

  return result;
}

function loadFromStorage(): StoredPrefs {
  try {
    const raw = localStorage.getItem(LS_KEY);
    return raw ? (JSON.parse(raw) as StoredPrefs) : {};
  } catch {
    return {};
  }
}

function saveToStorage(prefs: StoredPrefs): void {
  try {
    localStorage.setItem(LS_KEY, JSON.stringify(prefs));
  } catch {
    // Quota exceeded or storage unavailable — fail silently
  }
}

/**
 * Per-user column order and visibility for every grid in {@link GRID_REGISTRY}.
 *
 * Preferences live in two places on purpose: localStorage is read synchronously so the first paint
 * already uses the saved layout, and the DB copy (via {@link UserPreferencesStore}) overlays it as
 * soon as it arrives so the layout follows the user across devices. Writes go to both.
 */
export const GridColumnStore = signalStore(
  { providedIn: 'root' },
  withState<GridColumnState>(() => ({ prefs: loadFromStorage() })),
  /** All registered grids. */
  withProps(() => ({ grids: GRID_REGISTRY })),
  withMethods((store, userPrefs = inject(UserPreferencesStore)) => {
    // One cached signal per grid id: grids read these in templates, so the same instance must be
    // returned on every call or Angular would re-create a computed on each change detection pass.
    const columnKeySignals = new Map<string, Signal<string[]>>();

    /** Persists to localStorage and writes through to the DB. */
    const persist = (): void => {
      saveToStorage(store.prefs());
      userPrefs.set(LS_KEY, store.prefs());
    };

    return {
      /** Cached computed signal of visible column keys for a grid, in user-configured order. */
      getColumnKeys(gridId: string): Signal<string[]> {
        let keys = columnKeySignals.get(gridId);
        if (!keys) {
          keys = computed(() => {
            const gridDef = GRID_REGISTRY.find((g) => g.id === gridId);
            if (!gridDef) return [];
            return resolveKeys(gridDef, store.prefs()[gridId]);
          });
          columnKeySignals.set(gridId, keys);
        }
        return keys;
      },

      /**
       * Returns a mutable working copy of preferences for editing in the dialog.
       * Non-pinned columns only — pinned columns are always appended automatically.
       */
      getEditablePrefs(gridId: string): ColumnPreference[] {
        const gridDef = GRID_REGISTRY.find((g) => g.id === gridId);
        if (!gridDef) return [];
        return resolveEditablePrefs(gridDef, store.prefs()[gridId]);
      },

      /** Persist updated preferences for a grid. */
      updatePrefs(gridId: string, prefs: ColumnPreference[]): void {
        patchState(store, (current) => ({ prefs: { ...current.prefs, [gridId]: prefs } }));
        persist();
      },

      /** Clear saved preferences for one grid (restores defaults). */
      resetGrid(gridId: string): void {
        patchState(store, (current) => {
          const next = { ...current.prefs };
          delete next[gridId];
          return { prefs: next };
        });
        persist();
      },

      /** Clear ALL saved column preferences. */
      resetAll(): void {
        patchState(store, { prefs: {} });
        try {
          localStorage.removeItem(LS_KEY);
        } catch {
          // storage unavailable — ignore
        }
        userPrefs.remove(LS_KEY);
      },

      /** Overlays the DB copy once it loads; the DB wins over localStorage. */
      applyFromDb(prefs: StoredPrefs): void {
        patchState(store, { prefs });
      },
    };
  }),
  withHooks({
    onInit(store, userPrefs = inject(UserPreferencesStore)) {
      // When DB preferences load, overlay them onto the state (DB wins over localStorage)
      effect(() => {
        const dbValue = userPrefs.get<StoredPrefs>(LS_KEY);
        if (dbValue) {
          store.applyFromDb(dbValue);
        }
      });
    },
  }),
);
