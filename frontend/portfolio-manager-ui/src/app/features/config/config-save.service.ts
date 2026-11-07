import { DestroyRef, Injectable, Signal, computed, inject, signal } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Observable, catchError, finalize, forkJoin, map, of } from 'rxjs';

/** A savable unit of configuration registered by a tab while it is alive. */
export interface ConfigSection {
  id: string;
  dirty: Signal<boolean>;
  /** Persists pending edits; emits once when done and errors when the save fails. */
  save(): Observable<unknown>;
  /** Restores factory defaults for the section (optional). */
  reset?(): void;
}

/**
 * Registry that lets the config shell's "Save All" / "Reset All" reach every mounted tab without the
 * shell knowing what is in them. Tabs call {@link register} from their constructor.
 */
@Injectable({ providedIn: 'root' })
export class ConfigSaveService {
  private readonly snackBar = inject(MatSnackBar);
  private readonly sections = signal<ConfigSection[]>([]);

  readonly saving = signal(false);
  readonly dirtyCount = computed(() => this.sections().filter((s) => s.dirty()).length);

  register(section: ConfigSection, destroyRef: DestroyRef): void {
    this.sections.update((list) => [...list.filter((s) => s.id !== section.id), section]);
    destroyRef.onDestroy(() => this.sections.update((list) => list.filter((s) => s !== section)));
  }

  saveAll(): void {
    if (this.saving()) return;
    const dirty = this.sections().filter((s) => s.dirty());
    if (dirty.length === 0) {
      this.snackBar.open('No pending changes to save.', 'OK', { duration: 3000 });
      return;
    }
    this.saving.set(true);
    forkJoin(
      dirty.map((s) =>
        s.save().pipe(
          map(() => null),
          catchError(() => of(s.id)),
        ),
      ),
    )
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe((results) => {
        const failed = results.filter((r): r is string => r !== null);
        if (failed.length === 0) {
          this.snackBar.open('All configuration settings saved.', 'OK', { duration: 4000 });
        } else {
          this.snackBar.open(`Could not save: ${failed.join(', ')}`, 'Dismiss', {
            duration: 6000,
          });
        }
      });
  }

  resetAll(): void {
    this.sections().forEach((s) => s.reset?.());
    this.snackBar.open('Settings reset to defaults.', 'OK', { duration: 3000 });
  }
}
