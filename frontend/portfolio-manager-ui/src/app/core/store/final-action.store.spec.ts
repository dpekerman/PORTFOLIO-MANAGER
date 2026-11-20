import { FinalActionResult } from '../models/portfolio.models';
import { toPageDecision } from './final-action.store';

const result: FinalActionResult = {
  symbol: 'AAPL',
  itemId: 7,
  finalAction: 'Starter Entry',
  finalActionId: 'STARTER ENTRY',
  reason: 'Constructive setup',
  hoverDescription: 'Hover text',
  finalActionClass: 'fa-buy',
  trendSetup: 'Constructive',
  trendSetupReason: 'above 50 SMA',
  trendSetupClass: 'ts-up',
  momentumShift: 'Improving',
  momentumShiftReason: 'RSI rising',
  momentumShiftClass: 'ms-up',
  baseAction: 'Buy',
  gapStatus: 'None',
  gapScore: 2,
  gapPct: 1.5,
} as unknown as FinalActionResult;

describe('toPageDecision', () => {
  it('maps the backend result onto the decision shape the grids render', () => {
    const d = toPageDecision(result);
    expect(d.finalAction).toBe('Starter Entry');
    expect(d.finalActionReason).toBe('Constructive setup');
    expect(d.hoverDescription).toBe('Hover text');
    expect(d.gapScore).toBe(2);
    expect(d.trendSetup).toBe('Constructive');
  });
});
