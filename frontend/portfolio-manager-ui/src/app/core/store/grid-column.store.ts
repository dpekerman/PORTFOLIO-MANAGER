import { Signal, computed, effect, inject } from '@angular/core';
import {
  patchState,
  signalStore,
  withHooks,
  withMethods,
  withProps,
  withState,
} from '@ngrx/signals';
import { BreakpointStore } from './breakpoint.store';
import {
  ColumnDef,
  ColumnPreference,
  GRID_REGISTRY,
  GridDef,
  resolveHandsetKeys,
} from './grid-registry';
import { UserPreferencesStore } from './user-preferences.store';

const LS_KEY = 'pm_grid_columns_v1';
const OPPORTUNITIES_GRID_ID = 'trading-opportunities';
const OPPORTUNITY_PROFILES = ['Swing', 'Strategic'] as const;

/** Saved column order/visibility keyed by grid id, with optional strategy profile suffixes. */
type GridPrefs = Record<string, ColumnPreference[]>;
interface StoredPrefs {
  schemaVersion: 2;
  grids: GridPrefs;
}

const EMPTY_PREFS: StoredPrefs = { schemaVersion: 2, grids: {} };

function isRecord(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function isColumnPreference(value: unknown): value is ColumnPreference {
  return isRecord(value) && typeof value['key'] === 'string' && typeof value['visible'] === 'boolean';
}

function preferenceKey(gridId: string, profile?: string): string {
  return profile ? `${gridId}:${profile.toLowerCase()}` : gridId;
}

function migrateOpportunityProfiles(prefs: GridPrefs): GridPrefs {
  const legacy = prefs[OPPORTUNITIES_GRID_ID];
  if (!legacy) return prefs;

  const migrated = { ...prefs };
  delete migrated[OPPORTUNITIES_GRID_ID];
  for (const profile of OPPORTUNITY_PROFILES) {
    const key = preferenceKey(OPPORTUNITIES_GRID_ID, profile);
    migrated[key] ??= legacy.map((pref) => ({ ...pref }));
  }
  return migrated;
}

interface GridColumnState {
  prefs: StoredPrefs;
  /**
   * Handset escape hatch: show every *visible* column instead of only the primary ones.
   *
   * Intentionally **not** persisted — it is a momentary "let me see everything on this screen"
   * action, not a preference, and persisting it would silently change the saved layout.
   */
  showAllColumnsOnHandset: boolean;
}

// ── Pure resolution helpers ───────────────────────────────────────────────────
// Kept free of Angular and of store state so they stay cheap to unit test and easy to extend
// (a later phase adds a responsive `priority` to ColumnDef and resolves per breakpoint here).

/** Non-pinned columns, in registry order. Pinned columns are never user-orderable. */
function nonPinnedColumns(gridDef: GridDef): ColumnDef[] {
  return gridDef.columns.filter((c) => !c.pinned);
}

function defaultColumnVisibility(
  gridDef: GridDef,
  column: ColumnDef,
  profile?: string,
  addingToSavedLayout = false,
): boolean {
  const profileDefaults = profile ? gridDef.profileDefaults?.[profile] : undefined;
  if (profileDefaults) return column.required === true || profileDefaults.includes(column.key);
  const honourHidden = addingToSavedLayout || gridDef.respectDefaultHiddenWhenUnsaved === true;
  return column.required === true || !honourHidden || !column.defaultHidden;
}

/**
 * Visible column keys for one grid, in user-configured order.
 *
 * With no saved preference, profile defaults apply when available; other grids retain their registered
 * defaults. Saved order and visibility win, removed columns are dropped, new columns are appended, and
 * pinned columns are always appended last. Nothing rewrites a user's saved order or visibility.
 */
function withNewColumns(
  gridDef: GridDef,
  nonPinned: ColumnDef[],
  prefs: ColumnPreference[],
  profile?: string,
): (ColumnPreference & { added: boolean })[] {
  const validKeys = new Set(nonPinned.map((c) => c.key));
  const required = new Set(nonPinned.filter((c) => c.required).map((c) => c.key));
  const list = prefs
    .filter((p) => validKeys.has(p.key))
    .map((p) => ({ ...p, visible: p.visible || required.has(p.key), added: false }));
  const known = new Set(prefs.map((p) => p.key));
  for (const col of nonPinned) {
    if (known.has(col.key)) continue;
    const entry = { key: col.key, visible: defaultColumnVisibility(gridDef, col, profile, true), added: true };
    const anchor = col.insertAfter ? list.findIndex((p) => p.key === col.insertAfter) : -1;
    if (anchor >= 0) list.splice(anchor + 1, 0, entry);
    else list.push(entry);
  }
  return list;
}

/**
 * The clean layout a user without saved preferences sees, or "Reset to defaults" restores. Named
 * profile defaults take precedence; other grids use `defaultHidden` only when they opt in.
 */
export function defaultEditablePrefs(gridDef: GridDef, profile?: string): ColumnPreference[] {
  return nonPinnedColumns(gridDef).map((c) => ({
    key: c.key,
    visible: defaultColumnVisibility(gridDef, c, profile),
  }));
}

export function resolveKeys(gridDef: GridDef, prefs: ColumnPreference[] | undefined, profile?: string): string[] {
  const pinnedKeys = gridDef.columns.filter((c) => c.pinned).map((c) => c.key);
  if (!prefs) {
    if (!gridDef.respectDefaultHiddenWhenUnsaved && !(profile && gridDef.profileDefaults?.[profile])) {
      // No customisation — return all defaults
      return gridDef.columns.map((c) => c.key);
    }
    return [...defaultEditablePrefs(gridDef, profile).filter((p) => p.visible).map((p) => p.key), ...pinnedKeys];
  }

  // Legacy grids show newly registered columns regardless of `defaultHidden`; opted-in grids honour it.
  const honourHidden = gridDef.respectDefaultHiddenWhenUnsaved === true;
  const result = withNewColumns(gridDef, nonPinnedColumns(gridDef), prefs, profile)
    .filter((p) => (honourHidden ? p.visible : p.added || p.visible))
    .map((p) => p.key);

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
  profile?: string,
): ColumnPreference[] {
  if (!prefs) {
    return defaultEditablePrefs(gridDef, profile);
  }

  return withNewColumns(gridDef, nonPinnedColumns(gridDef), prefs, profile).map(({ key, visible }) => ({
    key,
    visible,
  }));
}

/** True when the editable layout differs from {@link defaultEditablePrefs} (order or visibility). */
export function isCustomisedLayout(
  gridDef: GridDef,
  prefs: readonly ColumnPreference[],
  profile?: string,
): boolean {
  const defaults = defaultEditablePrefs(gridDef, profile);
  if (prefs.length !== defaults.length) return true;
  return prefs.some((p, i) => p.key !== defaults[i].key || p.visible !== defaults[i].visible);
}

function normalizePrefs(value: unknown): StoredPrefs {
  const rawGrids =
    isRecord(value) && typeof value['schemaVersion'] === 'number' && isRecord(value['grids'])
      ? value['grids']
      : value;
  if (!isRecord(rawGrids)) return EMPTY_PREFS;

  const grids: GridPrefs = {};
  for (const [gridId, valueForGrid] of Object.entries(rawGrids)) {
    if (Array.isArray(valueForGrid)) {
      grids[gridId] = valueForGrid.filter(isColumnPreference).map(({ key, visible }) => ({ key, visible }));
    }
  }
  return { schemaVersion: 2, grids: migrateOpportunityProfiles(grids) };
}

function loadFromStorage(): StoredPrefs {
  try {
    const raw = localStorage.getItem(LS_KEY);
    return raw ? normalizePrefs(JSON.parse(raw)) : EMPTY_PREFS;
  } catch {
    return EMPTY_PREFS;
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
  withState<GridColumnState>(() => ({
    prefs: loadFromStorage(),
    showAllColumnsOnHandset: false,
  })),
  /** All registered grids. */
  withProps(() => ({ grids: GRID_REGISTRY })),
  withMethods(
    (store, userPrefs = inject(UserPreferencesStore), breakpoints = inject(BreakpointStore)) => {
      // One cached signal per grid/profile: grids read these in templates, so the same instance must be
      // returned on every call or Angular would re-create a computed on each change detection pass.
      const columnKeySignals = new Map<string, Signal<string[]>>();
      const responsiveKeySignals = new Map<string, Signal<string[]>>();

      /** Persists to localStorage and writes through to the DB. */
      const persist = (): void => {
        saveToStorage(store.prefs());
        userPrefs.set(LS_KEY, store.prefs());
      };

      const columnKeys = (gridId: string, profile?: string): Signal<string[]> => {
        const key = preferenceKey(gridId, profile);
        let keys = columnKeySignals.get(key);
        if (!keys) {
          keys = computed(() => {
            const gridDef = GRID_REGISTRY.find((g) => g.id === gridId);
            if (!gridDef) return [];
            return resolveKeys(gridDef, store.prefs().grids[key], profile);
          });
          columnKeySignals.set(key, keys);
        }
        return keys;
      };

      return {
        /** Cached computed signal of visible column keys for a grid, in user-configured order. */
        getColumnKeys: columnKeys,

        /**
         * Breakpoint-aware view over {@link getColumnKeys}.
         *
         * Desktop and tablet return *exactly* the saved keys, so wiring a grid to this signal can
         * never change its appearance on a wide screen. On a handset the list collapses to the
         * columns marked `priority: 'primary'` plus pinned ones (see `resolveHandsetKeys`), unless
         * {@link showAllColumnsOnHandset} is on.
         *
         * INVARIANT: this is a *projection*. It never writes `prefs`, localStorage or the DB, so a
         * user who opens the app on a phone still finds their desktop layout untouched.
         */
        getResponsiveColumnKeys(gridId: string, profile?: string): Signal<string[]> {
          const key = preferenceKey(gridId, profile);
          let keys = responsiveKeySignals.get(key);
          if (!keys) {
            keys = computed(() => {
              const visible = columnKeys(gridId, profile)();
              if (!breakpoints.isHandset()) return visible;
              const gridDef = GRID_REGISTRY.find((g) => g.id === gridId);
              if (!gridDef) return visible;
              return resolveHandsetKeys(gridDef, visible, store.showAllColumnsOnHandset());
            });
            responsiveKeySignals.set(key, keys);
          }
          return keys;
        },

        /** Reveal/hide every visible column on a handset. Applies to all grids; never persisted. */
        setShowAllColumnsOnHandset(showAll: boolean): void {
          patchState(store, { showAllColumnsOnHandset: showAll });
        },

        /**
         * Returns a mutable working copy of preferences for editing in the dialog.
         * Non-pinned columns only — pinned columns are always appended automatically.
         */
        getEditablePrefs(gridId: string, profile?: string): ColumnPreference[] {
          const gridDef = GRID_REGISTRY.find((g) => g.id === gridId);
          if (!gridDef) return [];
          return resolveEditablePrefs(gridDef, store.prefs().grids[preferenceKey(gridId, profile)], profile);
        },

        /** Persist updated preferences for a grid. */
        updatePrefs(gridId: string, prefs: ColumnPreference[], profile?: string): void {
          const key = preferenceKey(gridId, profile);
          patchState(store, (current) => ({
            prefs: { ...current.prefs, grids: { ...current.prefs.grids, [key]: prefs } },
          }));
          persist();
        },

        /** Clear saved preferences for one grid (restores defaults). */
        resetGrid(gridId: string, profile?: string): void {
          const key = preferenceKey(gridId, profile);
          patchState(store, (current) => {
            const next = { ...current.prefs.grids };
            delete next[key];
            return { prefs: { ...current.prefs, grids: next } };
          });
          persist();
        },

        /** Clear ALL saved column preferences. */
        resetAll(): void {
          patchState(store, { prefs: EMPTY_PREFS });
          try {
            localStorage.removeItem(LS_KEY);
          } catch {
            // storage unavailable — ignore
          }
          userPrefs.remove(LS_KEY);
        },

        /** Overlays the DB copy once it loads; the DB wins over localStorage. */
        applyFromDb(prefs: unknown): void {
          patchState(store, { prefs: normalizePrefs(prefs) });
        },
      };
    },
  ),
  withHooks({
    onInit(store, userPrefs = inject(UserPreferencesStore)) {
      // When DB preferences load, overlay them onto the state (DB wins over localStorage)
      effect(() => {
        const dbValue = userPrefs.get<unknown>(LS_KEY);
        if (dbValue) {
          store.applyFromDb(dbValue);
        }
      });
    },
  }),
);
