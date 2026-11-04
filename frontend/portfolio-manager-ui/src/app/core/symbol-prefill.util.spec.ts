import { normalizePrefillSymbol } from './symbol-prefill.util';

describe('normalizePrefillSymbol', () => {
  it('keeps Canadian and US symbols as they are', () => {
    expect(normalizePrefillSymbol('XIU.TO')).toBe('XIU.TO');
    expect(normalizePrefillSymbol('AAPL')).toBe('AAPL');
    expect(normalizePrefillSymbol('BRK-B')).toBe('BRK-B');
    expect(normalizePrefillSymbol('RCI-B.TO')).toBe('RCI-B.TO');
  });

  it('trims and upper-cases', () => {
    expect(normalizePrefillSymbol('  shop.to ')).toBe('SHOP.TO');
  });

  it('collapses a repeated suffix without touching the ticker identity', () => {
    expect(normalizePrefillSymbol('XIU.TO.TO')).toBe('XIU.TO');
    expect(normalizePrefillSymbol('abc.v.v')).toBe('ABC.V');
  });

  it('does not add or swap an exchange and tolerates empty input', () => {
    expect(normalizePrefillSymbol('ENB')).toBe('ENB');
    expect(normalizePrefillSymbol('')).toBe('');
    expect(normalizePrefillSymbol(null)).toBe('');
  });
});
