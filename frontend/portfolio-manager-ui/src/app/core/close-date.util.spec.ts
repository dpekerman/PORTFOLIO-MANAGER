import { FormControl, FormGroup } from '@angular/forms';
import {
  closeDateValidator,
  revalidateCloseDateOn,
  sortDecisionSources,
  torontoToday,
  torontoTodayDate,
} from './close-date.util';

describe('close-date.util (PM-018)', () => {
  const group = (type: string, open: Date | null, close: Date | null) => {
    const form = new FormGroup({
      transactionType: new FormControl(type),
      openDate: new FormControl(open),
      closeDate: new FormControl(close, [closeDateValidator]),
    });
    revalidateCloseDateOn(form);
    return form;
  };

  it('sorts decision sources case-insensitively without changing values', () => {
    const input = ['manual', 'App Signal', 'catalyst', 'Risk Control', 'Stop Loss Trigerred'];
    expect(sortDecisionSources(input)).toEqual([
      'App Signal',
      'catalyst',
      'manual',
      'Risk Control',
      'Stop Loss Trigerred',
    ]);
    expect(input[0]).toBe('manual');
    expect(sortDecisionSources(null)).toEqual([]);
  });

  it('uses the Toronto calendar day, not the UTC day', () => {
    // 2026-11-02T02:00Z is still 2026-11-01 evening in Toronto (EST, UTC-5)
    expect(torontoToday(new Date('2026-11-02T02:00:00Z'))).toBe('2026-11-01');
    // 2026-07-01T03:30Z is 2026-06-30 23:30 in Toronto (EDT, UTC-4)
    expect(torontoToday(new Date('2026-07-01T03:30:00Z'))).toBe('2026-06-30');
    const d = torontoTodayDate(new Date('2026-11-02T02:00:00Z'));
    expect([d.getFullYear(), d.getMonth(), d.getDate()]).toEqual([2026, 10, 1]);
  });

  it('does not require a Close Date for OPEN', () => {
    expect(group('OPEN', new Date(2026, 8, 1), null).controls['closeDate'].valid).toBe(true);
  });

  it('requires a Close Date for CLOSE', () => {
    const form = group('CLOSE', new Date(2026, 8, 1), null);
    expect(form.controls['closeDate'].errors).toEqual({ required: true });
  });

  it('rejects a Close Date before the Open Date and accepts the same day', () => {
    expect(group('CLOSE', new Date(2026, 8, 5), new Date(2026, 8, 4)).controls['closeDate'].errors).toEqual({
      beforeOpen: true,
    });
    expect(group('CLOSE', new Date(2026, 8, 5), new Date(2026, 8, 5)).controls['closeDate'].valid).toBe(true);
  });

  it('revalidates when the type or open date changes', () => {
    const form = group('OPEN', new Date(2026, 8, 1), null);
    form.controls['transactionType'].setValue('CLOSE');
    expect(form.controls['closeDate'].invalid).toBe(true);
    form.controls['closeDate'].setValue(new Date(2026, 8, 10));
    form.controls['openDate'].setValue(new Date(2026, 8, 20));
    expect(form.controls['closeDate'].errors).toEqual({ beforeOpen: true });
  });
});
