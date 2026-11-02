import { TestBed } from '@angular/core/testing';
import { DemoModeStore } from './demo-mode.store';

describe('DemoModeStore', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
  });

  it('passes values through when demo mode is off', () => {
    const store = TestBed.inject(DemoModeStore);
    expect(store.maskValue(1234)).toBe(1234);
    expect(store.maskPercent(5.5)).toBe(5.5);
    expect(store.displayValue(1234.5, 1)).toBe('1,234.5');
  });

  it('blur mode scales by the session factor stored in sessionStorage', () => {
    sessionStorage.setItem('pm_demo_factor', '0.5');
    const store = TestBed.inject(DemoModeStore);
    store.enable();
    expect(store.maskValue(1000)).toBe(500);
    expect(store.displayValue(1000)).toBe('500');
    expect(store.maskPercent(10)).toBe(Math.round(10 * (0.7 + (0.5 % 0.3)) * 10) / 10);
  });

  it('fake mode is deterministic and preserves the sign of percentages', () => {
    const store = TestBed.inject(DemoModeStore);
    store.enable();
    store.setStyle('fake');
    expect(store.maskValue(1234)).toBe(store.maskValue(1234));
    expect(store.maskPercent(3)).toBeGreaterThanOrEqual(0);
    expect(store.maskPercent(-3)).toBeLessThanOrEqual(0);
  });

  it('persists the mode and style', () => {
    const store = TestBed.inject(DemoModeStore);
    store.toggle();
    store.setStyle('fake');
    TestBed.tick();
    expect(localStorage.getItem('pm_demo_mode')).toBe('true');
    expect(localStorage.getItem('pm_demo_style')).toBe('fake');
    store.disable();
    expect(localStorage.getItem('pm_demo_mode')).toBe('false');
  });
});
