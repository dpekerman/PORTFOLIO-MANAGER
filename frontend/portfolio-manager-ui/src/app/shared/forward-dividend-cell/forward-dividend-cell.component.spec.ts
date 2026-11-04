import { ComponentFixture, TestBed } from '@angular/core/testing';
import { StockQuote } from '../../core/models/portfolio.models';
import { DemoModeStore } from '../../core/store/demo-mode.store';
import { ForwardDividendCellComponent } from './forward-dividend-cell.component';

const quote = (over: Partial<StockQuote>): StockQuote => ({ symbol: 'ENB.TO', currentPrice: 68.69, ...over }) as StockQuote;

describe('ForwardDividendCellComponent', () => {
  let fixture: ComponentFixture<ForwardDividendCellComponent>;
  const text = () => (fixture.nativeElement as HTMLElement).textContent?.replace(/\s+/g, ' ').trim();

  function render(q: StockQuote | null, metric: 'perShare' | 'yield', currency = 'CAD') {
    fixture = TestBed.createComponent(ForwardDividendCellComponent);
    fixture.componentRef.setInput('quote', q);
    fixture.componentRef.setInput('metric', metric);
    fixture.componentRef.setInput('currency', currency);
    fixture.detectChanges();
  }

  beforeEach(() => {
    localStorage.removeItem('pm-demo-mode');
    TestBed.configureTestingModule({ imports: [ForwardDividendCellComponent] });
  });

  it('formats the dividend in the trading currency and the yield as a percentage once (no double scaling)', () => {
    const q = quote({ dividendStatus: 'Positive', forwardDividendPerShare: 3.88, forwardYieldPct: 5.65 });
    render(q, 'perShare', 'CAD');
    expect(text()).toBe('$3.88');
    render(q, 'yield');
    expect(text()).toBe('5.65%');
  });

  it('shows an em dash for missing data but a real 0 for a confirmed zero', () => {
    render(quote({ dividendStatus: 'Missing' }), 'perShare');
    expect(text()).toBe('—');
    render(quote({ dividendStatus: 'Zero', forwardDividendPerShare: 0, forwardYieldPct: 0 }), 'yield');
    expect(text()).toBe('0.00%');
  });

  it('routes values through demo-mode masking', () => {
    const demo = TestBed.inject(DemoModeStore);
    const spy = vi.spyOn(demo, 'maskPercent');
    render(quote({ dividendStatus: 'Positive', forwardDividendPerShare: 3.88, forwardYieldPct: 5.65 }), 'yield');
    expect(spy).toHaveBeenCalledWith(5.65);
  });
});
