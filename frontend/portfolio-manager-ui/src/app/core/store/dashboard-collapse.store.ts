import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { withStorageSync } from './features/storage-sync.feature';

const SECTIONS = [
  'portfolio-value-history',
  'top-movers',
  'market-indices',
  'allocation',
  'market-leadership',
  'market-signals',
  'upcoming-earnings',
  'action-center',
  'signal-changes',
  'priority-candidates',
  'ytd-performance',
];

const storageKey = (sectionId: string) => `dashboard_collapse_${sectionId}`;

/** Only the market-leadership section starts collapsed. */
const defaultCollapsed = (sectionId: string) => sectionId === 'market-leadership';

interface DashboardCollapseState {
  /** Explicit choices only; a section absent from the map uses its default. */
  collapsed: Record<string, boolean>;
}

function loadPersisted(): DashboardCollapseState {
  const collapsed: Record<string, boolean> = {};
  for (const sectionId of SECTIONS) {
    const stored = localStorage.getItem(storageKey(sectionId));
    if (stored !== null) {
      collapsed[sectionId] = stored === 'true';
    }
  }
  return { collapsed };
}

/**
 * Collapsed/expanded state of the dashboard sections. Each section persists to its own
 * localStorage key `dashboard_collapse_<sectionId>`, and every known section is written on each
 * change (resolved with its default) so storage always holds a full snapshot.
 */
export const DashboardCollapseStore = signalStore(
  { providedIn: 'root' },
  withState<DashboardCollapseState>(loadPersisted),
  withMethods((store) => {
    const isCollapsed = (sectionId: string) =>
      store.collapsed()[sectionId] ?? defaultCollapsed(sectionId);

    return {
      /** True if the section is expanded. */
      isExpanded(sectionId: string): boolean {
        return !isCollapsed(sectionId);
      },

      toggleCollapsed(sectionId: string): void {
        patchState(store, ({ collapsed }) => ({
          collapsed: {
            ...collapsed,
            [sectionId]: !(collapsed[sectionId] ?? defaultCollapsed(sectionId)),
          },
        }));
      },

      setCollapsed(sectionId: string, isCollapsedValue: boolean): void {
        patchState(store, ({ collapsed }) => ({
          collapsed: { ...collapsed, [sectionId]: isCollapsedValue },
        }));
      },

      resetToDefaults(): void {
        patchState(store, {
          collapsed: Object.fromEntries(SECTIONS.map((id) => [id, defaultCollapsed(id)])),
        });
      },
    };
  }),
  withStorageSync<DashboardCollapseState>(({ collapsed }) => {
    for (const sectionId of SECTIONS) {
      localStorage.setItem(
        storageKey(sectionId),
        String(collapsed[sectionId] ?? defaultCollapsed(sectionId)),
      );
    }
  }),
);
