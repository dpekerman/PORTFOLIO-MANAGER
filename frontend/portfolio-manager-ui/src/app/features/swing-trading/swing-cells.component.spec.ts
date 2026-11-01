import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { SwingTradeRow } from '../../core/models/swing-trading.models';
import { SwingPlannedStopCellComponent } from './swing-planned-stop-cell.component';
import { SwingRiskCellComponent } from './swing-risk-cell.component';
import { swingRow } from './swing-row.testing';

function render<T>(type: new () => T, row: SwingTradeRow, demo = false) {
  TestBed.configureTestingModule({
    providers: [
      {
        provide: DemoModeStore,
        useValue: { isDemoMode: signal(demo), maskValue: (v: number) => (demo ? v * 2 : v), maskPercent: (v: number) => v },
      },
    ],
  });
  const fixture = TestBed.createComponent(type as never) as { componentRef: { setInput(n: string, v: unknown): void }; detectChanges(): void; nativeElement: HTMLElement };
  fixture.componentRef.setInput('row', row);
  fixture.detectChanges();
  return fixture;
}

describe('SwingPlannedStopCellComponent', () => {
  it('highlights a breach once, with an accessible name and the observed price, stop and timestamp', () => {
    const el = render(SwingPlannedStopCellComponent, swingRow({ plannedStopState: 'Breached' })).nativeElement;
    expect(el.querySelector('.ps-breached')).not.toBeNull();
    const flags = el.querySelectorAll('[data-testid="planned-stop-flag"]');
    expect(flags).toHaveLength(1);
    const name = flags[0].getAttribute('aria-label')!;
    expect(name).toContain('Breached');
    expect(name).toContain('11.85');
    expect(name).toContain('11.70');
    expect(name).toContain('2026-10-28 20:00 UTC');
    expect(name.toLowerCase()).not.toContain('executed');
  });

  it('reveals the explanation on click, not only on hover', () => {
    const fixture = render(SwingPlannedStopCellComponent, swingRow({ plannedStopState: 'Breached' }));
    const flag = fixture.nativeElement.querySelector('[data-testid="planned-stop-flag"]') as HTMLButtonElement;
    expect(flag.getAttribute('aria-expanded')).toBe('false');
    flag.click();
    fixture.detectChanges();
    expect(flag.getAttribute('aria-expanded')).toBe('true');
    expect(fixture.nativeElement.querySelector('[role="note"]')!.textContent).toContain('BREACHED');
  });

  it('shows a neutral Unverified marker for stale prices, never the red breach style', () => {
    const el = render(SwingPlannedStopCellComponent, swingRow({ plannedStopState: 'Unverified', priceBasis: 'Stale' })).nativeElement;
    expect(el.querySelector('.ps-breached')).toBeNull();
    expect(el.querySelector('[data-testid="planned-stop-flag"]')!.textContent).toContain('Unverified');
  });

  it('shows only the number when clear and "Not set" without a plan', () => {
    const clear = render(SwingPlannedStopCellComponent, swingRow({ plannedStopState: 'Clear' })).nativeElement;
    expect(clear.querySelector('[data-testid="planned-stop-flag"]')).toBeNull();
    expect(clear.textContent).toContain('11.85');
    TestBed.resetTestingModule();
    const none = render(SwingPlannedStopCellComponent, swingRow({ plannedStop: null, plannedStopState: 'NoPlan' })).nativeElement;
    expect(none.textContent).toContain('Not set');
    expect(none.querySelector('.ps')).toBeNull();
  });

  it('uses masked values in demo mode', () => {
    const el = render(SwingPlannedStopCellComponent, swingRow({ plannedStopState: 'Breached' }), true).nativeElement;
    expect(el.textContent).toContain('23.70');
  });
});

describe('SwingRiskCellComponent', () => {
  const many = swingRow({
    severity: 'Critical',
    riskStatuses: ['MissingRiskPlan', 'UnknownCoverage', 'NoBrokerProtection', 'StopBreached', 'StaleEvidence'],
  });
  const chips = (el: HTMLElement) => Array.from(el.querySelectorAll('[data-testid="risk-status-chip"]')).map((c) => c.textContent!.trim());

  it('shows the most material statuses and a real button for the rest', () => {
    const el = render(SwingRiskCellComponent, many).nativeElement;
    expect(el.querySelector('[data-testid="severity-chip"]')!.textContent).toContain('Critical');
    expect(chips(el)).toEqual(['At/below stop', 'Stale / no price']);
    const more = el.querySelector('[data-testid="risk-more"]') as HTMLButtonElement;
    expect(more.tagName).toBe('BUTTON');
    expect(more.textContent).toContain('+3 more');
    expect(more.getAttribute('aria-label')).toBe('Show 3 more risk flags');
  });

  it('expands to every status with click and collapses again', () => {
    const fixture = render(SwingRiskCellComponent, many);
    const more = fixture.nativeElement.querySelector('[data-testid="risk-more"]') as HTMLButtonElement;
    more.click();
    fixture.detectChanges();
    expect(chips(fixture.nativeElement)).toHaveLength(5);
    expect(more.getAttribute('aria-expanded')).toBe('true');
    more.click();
    fixture.detectChanges();
    expect(chips(fixture.nativeElement)).toHaveLength(2);
  });

  it('has no more button when everything fits, and keeps a broker-stop breach visible', () => {
    const el = render(SwingRiskCellComponent, swingRow({ riskStatuses: ['StopBreached', 'UnknownCoverage'], plannedStopState: 'NoPlan' })).nativeElement;
    expect(el.querySelector('[data-testid="risk-more"]')).toBeNull();
    expect(chips(el)).toContain('At/below stop');
  });
});
