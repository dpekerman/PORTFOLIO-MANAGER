import { computed, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { ActionCenterFilter, OpenDecision, ValidationStatus } from '../../../core/models/portfolio.models';
import { filterDecisions, OpenDecisionsStore } from '../../../core/store/open-decisions.store';
import { PortfolioActionsWidgetComponent } from './portfolio-actions-widget.component';

const decision = (id: number, symbol: string, overrides: Partial<OpenDecision> = {}): OpenDecision => ({
  id,
  symbol,
  context: 'Holding',
  role: 'Core',
  actionLabel: 'Trim / Review',
  actionId: 'TRIM/REVIEW',
  bucket: 'TrimExit',
  urgency: 'High',
  rationale: 'Volume and momentum confirmed',
  evidence: 'Signal Tracker / EOD',
  signalDate: '2026-10-23',
  lastValidatedAtUtc: '2026-10-23T21:00:00Z',
  isStale: false,
  status: 'Active',
  myAction: 'Pending',
  signalPrice: 10,
  origin: 'Live',
  ...overrides,
});

describe('PortfolioActionsWidgetComponent (Action Center)', () => {
  const all = signal<OpenDecision[]>([]);
  const filter = signal<ActionCenterFilter>('All');
  const store = {
    allItems: all,
    filter,
    items: computed(() => filterDecisions(all(), filter())),
    validation: signal<ValidationStatus | null>(null),
    loading: signal(false),
    error: signal<string | null>(null),
    toggleFilter: vi.fn(),
    setFilter: vi.fn(),
    load: vi.fn(),
  };

  const validated: ValidationStatus = { state: 'Validated', lastCompleteEvaluationUtc: '2026-10-23T21:00:00Z', lastAttemptUtc: null, pendingCount: 0 };

  beforeEach(() => {
    store.toggleFilter.mockReset();
    store.setFilter.mockReset();
    store.load.mockReset();
    store.error.set(null);
    store.validation.set(validated);
    filter.set('All');
    all.set([
      decision(1, 'ENB.TO', { bucket: 'RiskReview', urgency: 'Urgent', actionLabel: 'Cut Loss / Thesis Review' }),
      decision(2, 'HPS-A.TO', { bucket: 'EntryAdd', actionLabel: 'Add / Staged Entry', myAction: 'Not Actioned' }),
      decision(3, 'CP.TO', {
        context: 'Watchlist',
        role: null,
        bucket: 'EntryAdd',
        urgency: 'Normal',
        isStale: true,
        origin: 'INITIAL_BASELINE',
      }),
    ]);
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: OpenDecisionsStore, useValue: store }],
    });
  });

  function render(): HTMLElement {
    const fixture = TestBed.createComponent(PortfolioActionsWidgetComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders one row per open decision with context, action, evidence and lifecycle', () => {
    const el = render();
    const rows = Array.from(el.querySelectorAll('[data-testid="ac-row"]'));
    expect(rows).toHaveLength(3);
    const first = rows[0].textContent!;
    expect(first).toContain('ENB.TO');
    expect(first).toContain('Holding');
    expect(first).toContain('Cut Loss / Thesis Review');
    expect(first).toContain('Urgent');
    expect(first).toContain('Volume and momentum confirmed');
    expect(first).toContain('Signal Tracker / EOD');
    expect(first).toContain('Active');
    expect(first).toContain('Pending review');
  });

  it('keeps a recorded "Not Actioned" response visible instead of relabelling it', () => {
    expect(render().textContent).toContain('Not Actioned');
  });

  it('flags stale validation and initial-baseline provenance honestly', () => {
    const rows = Array.from(render().querySelectorAll('[data-testid="ac-row"]'));
    const stale = rows[2].textContent!;
    expect(stale).toContain('stale');
    expect(stale).toContain('Initial baseline');
  });

  it('links each row to the Signal Tracker for the ticker (no trade is executed here)', () => {
    const link = render().querySelector('[data-testid="ac-row"] a.ac-open') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toContain('/signal-tracker');
    expect(link.getAttribute('href')).toContain('ticker=ENB.TO');
  });

  it('keeps only the Holdings / Watchlist chips; categories are filtered from the header counters', () => {
    const el = render();
    const chip = (name: ActionCenterFilter) => el.querySelector(`[data-testid="ac-filter-${name}"]`) as HTMLButtonElement;
    expect(el.querySelectorAll('.ac-chip')).toHaveLength(2);
    expect(chip('Holdings').textContent).toContain('2');
    expect(chip('Watchlist').textContent).toContain('1');
    for (const gone of ['All', 'Entry', 'ReduceExit', 'Risk'] as ActionCenterFilter[]) expect(chip(gone)).toBeNull();
    chip('Watchlist').click();
    expect(store.toggleFilter).toHaveBeenCalledWith('Watchlist');
  });

  it('shows which category the header counters selected, and clears it', () => {
    filter.set('Risk');
    const el = render();
    expect(el.querySelector('[data-testid="ac-showing"]')!.textContent).toContain('Risk Review');
    (el.querySelector('.ac-clear') as HTMLButtonElement).click();
    expect(store.setFilter).toHaveBeenCalledWith('All');
  });

  it('lists only the filtered decisions', () => {
    filter.set('Watchlist');
    const rows = render().querySelectorAll('[data-testid="ac-row"]');
    expect(rows).toHaveLength(1);
    expect(rows[0].textContent).toContain('CP.TO');
  });

  it('says "No current actionable decisions" only when the evaluation is validated', () => {
    all.set([]);
    const el = render();
    expect(el.querySelector('[data-testid="ac-empty"]')!.textContent).toContain('No current actionable decisions');
    expect(el.querySelectorAll('[data-testid="ac-row"]')).toHaveLength(0);
  });

  it('shows "Validation pending" instead of an all-clear while recommendations await revalidation', () => {
    all.set([]);
    store.validation.set({ state: 'Pending', lastCompleteEvaluationUtc: null, lastAttemptUtc: null, pendingCount: 6 });
    const el = render();
    expect(el.querySelector('[data-testid="ac-pending"]')!.textContent).toContain('Validation pending');
    expect(el.querySelector('[data-testid="ac-pending"]')!.textContent).toContain('6 recommendation(s)');
    expect(el.textContent).not.toContain('No current actionable decisions');
    expect(el.querySelector('.ac-empty-icon')).toBeNull();
  });

  it('explains that no evaluation has completed when nothing is pending and nothing is validated', () => {
    all.set([]);
    store.validation.set({ state: 'Pending', lastCompleteEvaluationUtc: null, lastAttemptUtc: null, pendingCount: 0 });
    expect(render().querySelector('[data-testid="ac-pending"]')!.textContent).toContain('No EOD evaluation has completed yet');
  });

  it('shows "Evaluation unavailable / stale" when the latest evaluation failed or is incomplete', () => {
    all.set([]);
    store.validation.set({ state: 'Unavailable', lastCompleteEvaluationUtc: null, lastAttemptUtc: '2026-10-26T01:00:00Z', pendingCount: 0 });
    const el = render();
    expect(el.querySelector('[data-testid="ac-unavailable"]')!.textContent).toContain('Evaluation unavailable / stale');
    expect(el.querySelector('[data-testid="ac-unavailable"]')!.textContent).toContain('never');
    expect(el.textContent).not.toContain('No current actionable decisions');
  });

  it('keeps existing decisions visible and adds a banner when validation is stale or pending', () => {
    store.validation.set({ state: 'Unavailable', lastCompleteEvaluationUtc: '2026-10-20T21:00:00Z', lastAttemptUtc: null, pendingCount: 0 });
    let el = render();
    expect(el.querySelectorAll('[data-testid="ac-row"]')).toHaveLength(3);
    expect(el.querySelector('[data-testid="ac-banner"]')!.textContent).toContain('Evaluation unavailable / stale');

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [provideRouter([]), { provide: OpenDecisionsStore, useValue: store }] });
    store.validation.set({ state: 'Pending', lastCompleteEvaluationUtc: '2026-10-23T21:00:00Z', lastAttemptUtc: null, pendingCount: 2 });
    el = render();
    expect(el.querySelector('[data-testid="ac-banner"]')!.textContent).toContain('Validation pending for 2');
  });

  it('offers a retry when nothing could be loaded', () => {
    all.set([]);
    store.error.set('Could not load current decisions');
    const el = render();
    expect(el.textContent).toContain('could not be loaded');
    (el.querySelector('.ac-error button') as HTMLButtonElement).click();
    expect(store.load).toHaveBeenCalled();
  });
});
