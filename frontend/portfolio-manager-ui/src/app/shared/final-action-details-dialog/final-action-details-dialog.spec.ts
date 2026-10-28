import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { FinalActionResult } from '../../core/models/portfolio.models';
import { FinalActionDetailsDialogComponent } from './final-action-details-dialog.component';
import { describeReasonCode, stateLabel, technicalConflictNote } from './final-action-labels';

const result = (overrides: Partial<FinalActionResult> = {}): FinalActionResult =>
  ({
    symbol: 'BRK.TO',
    source: 'Portfolio',
    itemId: 1,
    role: 'Strategic',
    finalAction: 'Hold / Monitor',
    finalActionId: 'HOLD/MONITOR',
    category: 'Advisory',
    direction: 'Neutral',
    severity: 'hold',
    priority: 'INFORMATIONAL',
    finalActionClass: 'ma-hold',
    trendSetup: 'Technical Caution',
    momentumShift: 'Active Sell Trigger',
    baseAction: 'Confirmed Sell Signal',
    gapStatus: 'No Gap',
    hoverDescription: 'Review stop-loss levels.',
    reason: 'Technical signal only. Original technical action: Trim / Review.',
    hasHardStructuralNegative: false,
    analysisPrice: 39.3,
    analysisTicker: 'BRK.TO',
    analysisCurrency: 'CAD',
    trendSetupReason: '',
    momentumShiftReason: '',
    trendSetupClass: '',
    momentumShiftClass: '',
    gapScore: 0,
    gapPct: 0,
    isActionable: false,
    reasonCodes: ['TECHNICAL_ONLY_NO_PORTFOLIO_TRIGGER'],
    originalAction: 'Trim / Review',
    evaluationState: 'CurrentValid',
    evidenceHealth: 'Fresh',
    dataAsOfDate: '2026-10-26',
    expectedSession: '2026-10-26',
    lastSuccessfulEvaluationUtc: '2026-10-26T22:30:00Z',
    ...overrides,
  }) as FinalActionResult;

describe('final-action-labels', () => {
  it('describes known codes in plain language and unknown ones as-is', () => {
    expect(describeReasonCode('POST_ENTRY_LOSS')).toBe('Price is below cost after entry');
    expect(describeReasonCode('AWAITING_REQUIRED_RSI')).toBe('Required input unavailable: rsi');
    expect(describeReasonCode('SOMETHING_NEW')).toBe('SOMETHING_NEW');
  });

  it('labels every effective state', () => {
    expect(stateLabel('AwaitingData')).toBe('Awaiting Data');
    expect(stateLabel('PriorSessionValid')).toBe('Valid prior-session recommendation');
    expect(stateLabel('HistoricalWarning')).toBe('Unresolved historical warning');
    expect(stateLabel(null)).toBe('Current evaluation');
  });

  it('explains a sell-like technical status next to a Hold, but not next to a real exit or Awaiting Data', () => {
    expect(technicalConflictNote(result())).toContain('not a portfolio decision');
    expect(technicalConflictNote(result({ category: 'TrimExit', finalAction: 'Trim / Review' }))).toBeNull();
    expect(technicalConflictNote(result({ finalAction: 'Awaiting Data' }))).toBeNull();
    expect(technicalConflictNote(result({ momentumShift: 'Uptrend' }))).toBeNull();
  });
});

describe('FinalActionDetailsDialogComponent', () => {
  function render(r: FinalActionResult): HTMLElement {
    TestBed.configureTestingModule({ providers: [{ provide: MAT_DIALOG_DATA, useValue: { result: r } }] });
    const fixture = TestBed.createComponent(FinalActionDetailsDialogComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('keeps the portfolio decision and the technical readings visibly separate, with the reason', () => {
    const el = render(result());
    expect(el.querySelector('[data-testid="fad-action"]')!.textContent).toContain('Hold / Monitor');
    expect(el.querySelector('[data-testid="fad-action"]')!.textContent).toContain('Not actionable');
    expect(el.textContent).toContain('Technical signals');
    expect(el.textContent).toContain('Active Sell Trigger');
    expect(el.querySelector('[data-testid="fad-conflict"]')).not.toBeNull();
    expect(el.querySelector('[data-testid="fad-reason"]')!.textContent).toContain('Original technical action');
    expect(el.textContent).toContain('Technical signal only — no portfolio-level trigger');
    expect(el.textContent).toContain('Technical rule produced');
  });

  it('shows the last valid action and evidence freshness for Awaiting Data', () => {
    const el = render(
      result({
        finalAction: 'Awaiting Data',
        evaluationState: 'AwaitingData',
        evidenceHealth: 'Stale',
        lastValidAction: 'Add to Trade',
        reasonCodes: ['AWAITING_STALE_DATA'],
        originalAction: null,
        dataAsOfDate: '2026-10-20',
      }),
    );
    expect(el.textContent).toContain('Awaiting Data');
    expect(el.textContent).toContain('Last valid action (historical)');
    expect(el.textContent).toContain('Add to Trade');
    expect(el.textContent).toContain('Stale');
    expect(el.textContent).toContain('2026-10-20');
    expect(el.querySelector('[data-testid="fad-conflict"]')).toBeNull();
  });

  it('is labelled for assistive technology and has a focusable close button', () => {
    const el = render(result());
    expect(el.querySelector('h2')!.id).toBe('fa-details-title');
    expect(el.querySelector('mat-dialog-content')!.getAttribute('aria-labelledby')).toBe('fa-details-title');
    expect(el.querySelector('button[mat-dialog-close]')).not.toBeNull();
  });
});
