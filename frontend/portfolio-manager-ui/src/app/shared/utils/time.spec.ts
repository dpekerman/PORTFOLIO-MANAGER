import { dateToTimeString, timeStrToDate } from './time';

describe('time utils', () => {
  it('round-trips HH:mm', () => {
    expect(dateToTimeString(timeStrToDate('15:30'))).toBe('15:30');
    expect(dateToTimeString(timeStrToDate('07:05'))).toBe('07:05');
  });

  it('returns null for empty or malformed input', () => {
    expect(timeStrToDate('')).toBeNull();
    expect(timeStrToDate(null)).toBeNull();
    expect(timeStrToDate('abc')).toBeNull();
  });

  it('falls back to midnight for a missing date', () => {
    expect(dateToTimeString(null)).toBe('00:00');
  });
});
