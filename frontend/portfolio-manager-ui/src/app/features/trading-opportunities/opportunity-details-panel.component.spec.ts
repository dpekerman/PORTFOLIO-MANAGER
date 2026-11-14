import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';
import { OpportunityDetail, OpportunityRow } from '../../core/models/opportunities.models';
import { FinalActionResult } from '../../core/models/portfolio.models';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { OpportunityDetailsPanelComponent } from './opportunity-details-panel.component';
import { opportunityDetail, opportunityRow } from './testing/opportunity-fixtures';

const decision = (overrides: Partial<FinalActionResult>): FinalActionResult =>
  ({
    symbol: 'ABC.TO',
    source: 'Portfolio',
    itemId: 1,
    role: 'Swing',
    finalAction: 'Hold / Monitor',
    finalActionId: 'HOLD/MONITOR',
    finalActionClass: 'ma-hold',
    hoverDescription: 'Hold',
    evaluationState: 'CurrentValid',
    ...overrides,
  }) as FinalActionResult;

function render(row: OpportunityRow, detail: OpportunityDetail | null, demo = false) {
  const open = vi.fn();
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: MatDialog, useValue: { open } },
      {
        provide: DemoModeStore,
        useValue: { isDemoMode: signal(demo), maskValue: (v: number) => (demo ? v * 2 : v), maskPercent: (v: number) => v },
      },
    ],
  });
  const fixture = TestBed.createComponent(OpportunityDetailsPanelComponent);
  fixture.componentRef.setInput('row', row);
  fixture.componentRef.setInput('detail', detail);
  fixture.detectChanges();
  return { el: fixture.nativeElement as HTMLElement, open };
}

describe('OpportunityDetailsPanelComponent', () => {
  it('lists every original decision for a conflicting Multiple action and opens the chosen one', () => {
    const row = opportunityRow('ABC.TO', {
      finalAction: {
        ...opportunityRow('ABC.TO').finalAction,
        kind: 'Multiple',
        label: null,
        finalActionClass: null,
        decisionCount: 2,
        distinctLabels: ['Hold / Monitor', 'Trim / Review'],
      },
    });
    const a = decision({ role: 'Swing' });
    const b = decision({ role: 'Strategic', finalAction: 'Trim / Review', finalActionClass: 'ma-trim' });
    const { el, open } = render(row, { ...opportunityDetail(row), decisions: [a, b] });

    const fa = el.querySelector('[data-testid="details-final-action"]')!;
    expect(fa.textContent).toContain('Multiple (2)');
    const items = el.querySelectorAll('[data-testid="details-decisions"] li');
    expect(items).toHaveLength(2);
    expect(items[1].textContent).toContain('Trim / Review');
    expect(items[1].textContent).toContain('Portfolio · Strategic');

    (items[1].querySelector('button, [role="button"]') as HTMLElement).click();
    expect(open).toHaveBeenCalledTimes(1);
    expect(open.mock.calls[0][1].data.result).toBe(b);
  });

  it('shows Not evaluated for universe-only symbols instead of inventing a decision', () => {
    const row = opportunityRow('XYZ.TO', {
      finalAction: { ...opportunityRow('XYZ.TO').finalAction, kind: 'None', label: null, decisionCount: 0 },
    });
    const { el } = render(row, opportunityDetail(row));
    expect(el.querySelector('[data-testid="details-final-action"]')!.textContent).toContain('Not evaluated');
  });

  it('shows no entry, stop or target values when the calculated setup is unavailable', () => {
    const base = opportunityRow('ABC.TO');
    const row = opportunityRow('ABC.TO', {
      setup: { ...base.setup, kind: 'Unavailable', reasonCodes: ['SETUP_NO_TARGET'] },
    });
    const { el } = render(row, opportunityDetail(row));
    const text = el.textContent ?? '';
    expect(text).toContain('Unavailable');
    expect(text).not.toContain('Entry');
    expect(text).not.toContain(base.setup.stop!.toFixed(2));
    expect(text).not.toContain(base.setup.target!.toFixed(2));
    expect(text).toContain('No saved risk plan');
  });

  it('masks money through demo mode', () => {
    const row = opportunityRow('ABC.TO');
    const { el } = render(row, opportunityDetail(row), true);
    expect(el.textContent).toContain((row.setup.stop! * 2).toFixed(2));
  });
});
