import { TestBed } from '@angular/core/testing';
import { DashboardCollapseStore } from './dashboard-collapse.store';

describe('DashboardCollapseStore', () => {
  beforeEach(() => localStorage.clear());

  it('collapses only market-leadership by default and persists a full snapshot', () => {
    const store = TestBed.inject(DashboardCollapseStore);
    expect(store.isExpanded('market-leadership')).toBe(false);
    expect(store.isExpanded('top-movers')).toBe(true);
    expect(localStorage.getItem('dashboard_collapse_market-leadership')).toBe('true');
    expect(localStorage.getItem('dashboard_collapse_top-movers')).toBe('false');
  });

  it('toggles and sets sections, persisting each key', () => {
    const store = TestBed.inject(DashboardCollapseStore);
    store.toggleCollapsed('market-leadership');
    expect(store.isExpanded('market-leadership')).toBe(true);
    store.setCollapsed('top-movers', true);
    expect(store.isExpanded('top-movers')).toBe(false);
    expect(localStorage.getItem('dashboard_collapse_top-movers')).toBe('true');
    expect(localStorage.getItem('dashboard_collapse_market-leadership')).toBe('false');
  });

  it('restores persisted choices and can reset to defaults', () => {
    localStorage.setItem('dashboard_collapse_allocation', 'true');
    const store = TestBed.inject(DashboardCollapseStore);
    expect(store.isExpanded('allocation')).toBe(false);
    store.resetToDefaults();
    expect(store.isExpanded('allocation')).toBe(true);
    expect(store.isExpanded('market-leadership')).toBe(false);
  });
});
