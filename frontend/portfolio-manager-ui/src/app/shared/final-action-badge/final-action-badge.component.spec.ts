import { TestBed } from '@angular/core/testing';
import { FinalActionBadgeComponent } from './final-action-badge.component';

function create(inputs: Record<string, unknown>) {
  const fixture = TestBed.createComponent(FinalActionBadgeComponent);
  for (const [k, v] of Object.entries(inputs)) fixture.componentRef.setInput(k, v);
  fixture.detectChanges();
  const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
  return { fixture, button };
}

describe('FinalActionBadgeComponent', () => {
  it('renders the supplied label, bucket class and accessible name without changing them', () => {
    const { button } = create({ label: 'Hold / Trail Stop', bucket: 'ma-hold', ariaLabel: 'Final Action details for AAPL: Hold / Trail Stop' });
    expect(button.textContent!.trim()).toBe('Hold / Trail Stop');
    expect(button.classList).toContain('ma-hold');
    expect(button.getAttribute('aria-label')).toBe('Final Action details for AAPL: Hold / Trail Stop');
  });

  it('falls back to the neutral bucket', () => {
    expect(create({ label: 'Mixed Actions / Review Lots' }).button.classList).toContain('ma-standby');
  });

  it('emits when activated', () => {
    const { fixture, button } = create({ label: 'Exit / Sell', bucket: 'ma-confirmed-sell' });
    let count = 0;
    fixture.componentInstance.activated.subscribe(() => count++);
    button.click();
    expect(count).toBe(1);
  });

  it('stays focusable but inert and aria-disabled when no details exist', () => {
    const { fixture, button } = create({ label: 'Mixed Actions / Review Lots', disabled: true });
    let count = 0;
    fixture.componentInstance.activated.subscribe(() => count++);
    button.click();
    expect(count).toBe(0);
    expect(button.disabled).toBe(false);
    expect(button.getAttribute('aria-disabled')).toBe('true');
  });
});
