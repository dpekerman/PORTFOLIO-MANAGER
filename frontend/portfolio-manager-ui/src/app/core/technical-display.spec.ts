import { currencyCodeForAnalysis, currencyCodeForTradingSymbol } from './technical-display';

describe('Currency code helpers', () => {
  it('maps Toronto symbols to CAD and others to USD', () => {
    expect(currencyCodeForTradingSymbol('RY.TO')).toBe('CAD');
    expect(currencyCodeForTradingSymbol(' ry.to ')).toBe('CAD');
    expect(currencyCodeForTradingSymbol('AAPL')).toBe('USD');
  });

  it('does not throw for a missing symbol', () => {
    expect(currencyCodeForTradingSymbol(undefined)).toBe('USD');
    expect(currencyCodeForTradingSymbol(null)).toBe('USD');
  });

  it('prefers an explicit analysis currency, e.g. a CDR analyzed on its US underlying', () => {
    expect(currencyCodeForAnalysis('SPGI.TO', 'USD')).toBe('USD');
    expect(currencyCodeForAnalysis('AAPL', 'cad')).toBe('CAD');
  });

  it('falls back to the symbol market for missing or unsupported analysis currency', () => {
    expect(currencyCodeForAnalysis('SPGI.TO', null)).toBe('CAD');
    expect(currencyCodeForAnalysis('MSFT', 'EUR')).toBe('USD');
  });
});
