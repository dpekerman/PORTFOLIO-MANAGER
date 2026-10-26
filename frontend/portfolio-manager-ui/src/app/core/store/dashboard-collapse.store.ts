import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { BreakpointStore } from './breakpoint.store';
import { withStorageSync } from './features/storage-sync.feature';

const SECTIONS = [
  'portfolio-value-history',
  'top-movers',
  'market-indices',
  'allocation',
  'market-leadership',
  'upcoming-earnings',
  'action-center',
  'signal-changes',
  'priority-candidates',
  'ytd-performance',
  'latest-eod-signals',
];

/** Phones keep their own keys so a desktop "expanded" snapshot never overrides the phone default. */
const storageKey = (sectionId: string, handset: boolean) =>
  `dashboard_collapse_${handset ? 'm_' : ''}${sectionId}`;

/** Desktop and tablet: only market-leadership starts collapsed. Phones: everything does. */
const defaultCollapsed = (sectionId: string, handset: boolean) =>
  handset || sectionId === 'market-leadership';

interface DashboardCollapseState {
  /** Phone layout, fixed when the store is created. */
  handset: boolean;
  /** Explicit choices only; a section absent from the map uses its default. */
  collapsed: Record<string, boolean>;
}

function loadPersisted(handset: boolean): DashboardCollapseState {
  const collapsed: Record<string, boolean> = {};
  for (const sectionId of SECTIONS) {
    const stored = localStorage.getItem(storageKey(sectionId, handset));
    if (stored !== null) {
      collapsed[sectionId] = stored === 'true';
    }
  }
  return { handset, collapsed };
}

/**
 * Collapsed/expanded state of the dashboard sections. Each section persists to its own
 * localStorage key `dashboard_collapse_<sectionId>`, and every known section is written on each
 * change (resolved with its default) so storage always holds a full snapshot.
 */
export const DashboardCollapseStore = signalStore(
  { providedIn: 'root' },
  withState<DashboardCollapseState>(() => loadPersisted(inject(BreakpointStore).isHandset())),
  withMethods((store) => {
    const isCollapsed = (sectionId: string) =>
      store.collapsed()[sectionId] ?? defaultCollapsed(sectionId, store.handset());

    return {
      /** True if the section is expanded. */
      isExpanded(sectionId: string): boolean {
        return !isCollapsed(sectionId);
      },

      toggleCollapsed(sectionId: string): void {
        patchState(store, ({ collapsed }) => ({
          collapsed: {
            ...collapsed,
            [sectionId]: !(collapsed[sectionId] ?? defaultCollapsed(sectionId, store.handset())),
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
          collapsed: Object.fromEntries(SECTIONS.map((id) => [id, defaultCollapsed(id, store.handset())])),
        });
      },
    };
  }),
  withStorageSync<DashboardCollapseState>(({ collapsed, handset }) => {
    for (const sectionId of SECTIONS) {
      localStorage.setItem(
        storageKey(sectionId, handset),
        String(collapsed[sectionId] ?? defaultCollapsed(sectionId, handset)),
      );
    }
  }),
);
