import { Signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl } from '@angular/forms';
import { map } from 'rxjs';

/** Signal that is true while the form has unsaved edits. Call from an injection context. */
export function formDirtySignal(form: AbstractControl): Signal<boolean> {
  return toSignal(form.events.pipe(map(() => form.dirty)), { initialValue: form.dirty });
}
