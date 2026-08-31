import { Injectable, effect, signal } from '@angular/core';

/**
 * Manages collapsible section state for the Dashboard.
 * Persists state to localStorage with key: `dashboard_collapse_${sectionId}`
 *
 * Sections: portfolio-value-history, top-movers, market-indices, allocation,
 *           market-leadership (default collapsed), market-signals, action-center,
 *           upcoming-earnings, signal-changes, priority-candidates, ytd-performance
 */
@Injectable({
  providedIn: 'root',
})
export class DashboardCollapseStateService {
  private readonly sections = [
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

  private readonly collapsedSections = signal(new Map<string, boolean>());

  constructor() {
    // Load persisted state from localStorage
    this.loadPersistedState();

    // Auto-persist whenever state changes
    effect(() => {
      this.collapsedSections();
      this.persistState();
    });
  }

  /**
   * Check if a section is expanded.
   * @param sectionId - unique section identifier
   * @returns true if expanded, false if collapsed
   */
  isExpanded(sectionId: string): boolean {
    const isCollapsed = this.collapsedSections().get(sectionId) ?? this.defaultCollapsed(sectionId);
    return !isCollapsed;
  }

  /**
   * Toggle collapse state for a section.
   */
  toggleCollapsed(sectionId: string): void {
    this.collapsedSections.update((current) => {
      const next = new Map(current);
      const isCollapsed = next.get(sectionId) ?? this.defaultCollapsed(sectionId);
      next.set(sectionId, !isCollapsed);
      return next;
    });
  }

  /**
   * Set collapse state explicitly.
   */
  setCollapsed(sectionId: string, collapsed: boolean): void {
    this.collapsedSections.update((current) => {
      const next = new Map(current);
      next.set(sectionId, collapsed);
      return next;
    });
  }

  /**
   * Load persisted state from localStorage.
   * Each section has its own key: `dashboard_collapse_${sectionId}`
   */
  private loadPersistedState(): void {
    const loaded = new Map<string, boolean>();
    for (const sectionId of this.sections) {
      const key = `dashboard_collapse_${sectionId}`;
      const stored = localStorage.getItem(key);
      if (stored !== null) {
        loaded.set(sectionId, stored === 'true');
      }
    }

    this.collapsedSections.set(loaded);
  }

  /**
   * Persist all section states to localStorage.
   */
  private persistState(): void {
    for (const sectionId of this.sections) {
      const isCollapsed =
        this.collapsedSections().get(sectionId) ?? this.defaultCollapsed(sectionId);
      const key = `dashboard_collapse_${sectionId}`;
      localStorage.setItem(key, isCollapsed.toString());
    }
  }

  /**
   * Reset all sections to defaults (for testing or manual reset).
   */
  resetToDefaults(): void {
    this.collapsedSections.set(
      new Map(this.sections.map((sectionId) => [sectionId, this.defaultCollapsed(sectionId)])),
    );
    this.persistState();
  }

  private defaultCollapsed(sectionId: string): boolean {
    return sectionId === 'market-leadership';
  }
}
