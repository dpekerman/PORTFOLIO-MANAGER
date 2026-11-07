import { AbstractControl, ValidationErrors } from '@angular/forms';

const TORONTO = 'America/Toronto';

/** Today's Toronto calendar date as `yyyy-MM-dd`. */
export function torontoToday(now: Date = new Date()): string {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: TORONTO,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(now);
}

/** Toronto today as a local-noon Date, matching how the dialogs parse date-only values (no UTC shift). */
export function torontoTodayDate(now: Date = new Date()): Date {
  return new Date(`${torontoToday(now)}T12:00:00`);
}

/** Case-insensitive alphabetical copy; stored values are not changed. */
export function sortDecisionSources(values: readonly string[] | null | undefined): string[] {
  return [...(values ?? [])].sort((a, b) =>
    a.localeCompare(b, undefined, { sensitivity: 'base' }),
  );
}

function dayKey(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

/**
 * Validator for the `closeDate` control of a form that also has `transactionType` and `openDate`:
 * a CLOSE needs a Close Date that is not before the Open Date.
 */
export function closeDateValidator(control: AbstractControl): ValidationErrors | null {
  const parent = control.parent;
  if (!parent || parent.get('transactionType')?.value !== 'CLOSE') return null;
  const close = control.value as Date | null;
  if (!close) return { required: true };
  const open = parent.get('openDate')?.value as Date | null;
  if (open && dayKey(close) < dayKey(open)) return { beforeOpen: true };
  return null;
}

/** Re-runs the close-date validation whenever the type or open date changes. */
export function revalidateCloseDateOn(form: AbstractControl): void {
  // Validators run before the control has a parent, so evaluate once now that the group exists.
  form.get('closeDate')?.updateValueAndValidity({ emitEvent: false });
  for (const name of ['transactionType', 'openDate']) {
    form.get(name)?.valueChanges.subscribe(() => {
      form.get('closeDate')?.updateValueAndValidity({ emitEvent: false });
    });
  }
}
