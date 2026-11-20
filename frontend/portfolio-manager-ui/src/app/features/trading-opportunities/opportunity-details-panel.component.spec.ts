import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';
import { OpportunityDetail, OpportunityRow } from '../../core/models/opportunities.models';
import { FinalActionResult } from '../../core/models/portfolio.models';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { OpportunityDetailsPanelComponent } from './opportunity-details-panel.component';
import {
  opportunityDetail,
  opportunityRow,
  setupQualityFixture,
} from './testing/opportunity-fixtures';
import { EMPTY_MANUAL_R } from './opportunities-logic';

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
        useValue: {
          isDemoMode: signal(demo),
          maskValue: (v: number) => (demo ? v * 2 : v),
          maskPercent: (v: number) => v,
        },
      },
    ],
  });
  const fixture = TestBed.createComponent(OpportunityDetailsPanelComponent);
  fixture.componentRef.setInput('row', row);
  fixture.componentRef.setInput('detail', detail);
  fixture.detectChanges();
  return { el: fixture.nativeElement as HTMLElement, fixture, open };
}

describe('OpportunityDetailsPanelComponent', () => {
  it('uses the structural analysis currency and masks prices even when listing currency differs', () => {
    const row = opportunityRow('CDR.NE', {
      currency: 'CAD',
      setupQuality: setupQualityFixture({ currency: 'USD' }),
    });
    const { el } = render(row, opportunityDetail(row), true);
    const section = el.querySelector('[data-testid="setup-quality-details"]')!;
    expect(section.textContent).toContain('USD 200.00');
    expect(section.textContent).toContain('USD 190.00');
    expect(section.textContent).not.toContain('CAD 100.00');
  });
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
    const b = decision({
      role: 'Strategic',
      finalAction: 'Trim / Review',
      finalActionClass: 'ma-trim',
    });
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
      finalAction: {
        ...opportunityRow('XYZ.TO').finalAction,
        kind: 'None',
        label: null,
        decisionCount: 0,
      },
    });
    const { el } = render(row, opportunityDetail(row));
    expect(el.querySelector('[data-testid="details-final-action"]')!.textContent).toContain(
      'Not evaluated',
    );
  });

  it('shows no entry, stop or target values when the calculated setup is unavailable', () => {
    const base = opportunityRow('ABC.TO');
    const row = opportunityRow('ABC.TO', {
      setup: { ...base.setup, kind: 'Unavailable', reasonCodes: ['SETUP_NO_TARGET'] },
    });
    const { el } = render(row, opportunityDetail(row));
    const text = el.textContent ?? '';
    const setupValues = el.querySelector('[data-testid="setup-values"]')?.textContent ?? '';
    expect(text).toContain('Unavailable');
    expect(setupValues).not.toContain('Entry');
    expect(setupValues).not.toContain('Stop');
    expect(setupValues).not.toContain('Target');
    expect(el.querySelector<HTMLInputElement>('[data-testid="manual-r-entry"]')?.value).toBe('');
    expect(text).toContain('No saved risk plan');
  });

  it('masks money through demo mode', () => {
    const row = opportunityRow('ABC.TO');
    const { el } = render(row, opportunityDetail(row), true);
    expect(el.textContent).toContain((row.setup.stop! * 2).toFixed(2));
  });

  it('groups candidate levels, normalizes the target source, and visualizes the full risk/reward ratio', () => {
    const row = opportunityRow('ABC.TO');
    const { el } = render(row, opportunityDetail(row));
    const levels = el.querySelector('[data-testid="setup-values"]')!;
    expect(levels.textContent).toContain('Current price');
    expect(levels.textContent).toContain('Entry');
    expect(levels.textContent).toContain('Stop');
    expect(levels.textContent).toContain('Target');
    expect(levels.textContent).toContain('Market R');
    expect(el.querySelector('[data-testid="setup-target-source"]')?.textContent).toContain(
      'Reference/source: 52-week high',
    );

    const visual = el.querySelector<HTMLElement>('[data-testid="setup-risk-reward"]')!;
    expect(visual.getAttribute('role')).toBe('img');
    expect(visual.getAttribute('aria-label')).toContain('2.47 : 1');
    expect(visual.textContent).toContain('Risk 6.4%');
    expect(visual.textContent).toContain('Reward 15.8%');
    expect(visual.querySelector<HTMLElement>('.reward-segment')?.style.flexGrow).toBe('2.47');
  });

  it('shows currency context and fixed precision for fractional scoring components', () => {
    const row = opportunityRow('ABC.TO');
    const detail = opportunityDetail(row);
    detail.swingScore = {
      ...detail.swingScore,
      awardedTotal: 14.5,
      availableMax: 22,
      components: [
        {
          code: 'TR',
          name: 'Trend',
          max: 4,
          essential: true,
          state: 'Partial',
          awarded: 1.5,
          availableMax: 4,
          items: [
            { code: 'A', label: 'Alignment', max: 1, points: 0.75, available: true, detail: null },
          ],
          missingReasons: [],
          note: null,
        },
      ],
    };
    const { el, fixture } = render(row, detail);
    expect(el.textContent).toContain(`CAD ${row.setup.stop!.toFixed(2)}`);
    [...el.querySelectorAll<HTMLElement>('[role="tab"]')]
      .find((tab) => tab.textContent?.includes('Scores'))
      ?.click();
    fixture.detectChanges();

    expect(el.textContent).toContain('14.50 / 22.00 available scoring points');
    expect(el.textContent).toContain('1.50 / 4.00');
    expect(el.textContent).toContain('0.75 / 1.00');
  });

  it('distinguishes the fixed Swing-v2 denominator from observed evidence', () => {
    const row = opportunityRow('ABC.TO');
    const detail = opportunityDetail(row);
    detail.evidence.scoringVersion = 'pm029-v2';
    detail.swingScore = {
      ...detail.swingScore,
      scoringVersion: 'pm029-v2',
      scoreDenominatorMax: 80,
      awardedTotal: 60,
      availableMax: 70,
    };
    const { el, fixture } = render(row, detail);
    [...el.querySelectorAll<HTMLElement>('[role="tab"]')]
      .find((tab) => tab.textContent?.includes('Scores'))
      ?.click();
    fixture.detectChanges();

    expect(el.textContent).toContain('60.00 / 80.00 technical points');
    expect(el.textContent).toContain('70.00 / 80.00 evidence available');
    expect(el.textContent).toContain('Market setup and execution readiness are separate');
  });

  it('calculates Manual R locally and uses the selected ticker scenario supplied by the page', () => {
    const row = opportunityRow('ABC.TO');
    const detail = opportunityDetail(row);
    const { el, fixture } = render(row, detail);
    const setValue = (testId: string, value: string) => {
      const input = el.querySelector<HTMLInputElement>(`[data-testid="${testId}"]`)!;
      input.value = value;
      input.dispatchEvent(new Event('input', { bubbles: true }));
      fixture.detectChanges();
    };

    expect(el.querySelector('[data-testid="manual-r"]')?.textContent).toContain(
      'Temporary and scoped to this ticker and score perspective',
    );
    setValue('manual-r-entry', '100');
    setValue('manual-r-stop', '90');
    setValue('manual-r-target', '120');
    expect(el.querySelector('[data-testid="manual-r-result"]')?.textContent).toContain('2.00 : 1');
    expect(
      el.querySelector('[data-testid="manual-r-visual"]')?.getAttribute('aria-label'),
    ).toContain('10.0%');
    expect(
      el.querySelector('[data-testid="manual-r-visual"]')?.getAttribute('aria-label'),
    ).toContain('20.0%');
    expect(detail.setup.rewardRisk).not.toBe(2);

    setValue('manual-r-stop', '100');
    expect(el.querySelector('[data-testid="manual-r-result"]')?.textContent).toContain(
      'stop below entry',
    );
    expect(el.querySelector('[data-testid="manual-r-result"]')?.textContent).toContain('—');

    const nextRow = opportunityRow('XYZ.TO');
    fixture.componentRef.setInput('row', nextRow);
    fixture.componentRef.setInput('detail', opportunityDetail(nextRow));
    fixture.componentRef.setInput('manualRValues', EMPTY_MANUAL_R);
    fixture.detectChanges();
    expect(el.querySelector<HTMLInputElement>('[data-testid="manual-r-entry"]')?.value).toBe('');
    expect(el.querySelector('[data-testid="manual-r-result"]')?.textContent).toContain('—');
  });
});
