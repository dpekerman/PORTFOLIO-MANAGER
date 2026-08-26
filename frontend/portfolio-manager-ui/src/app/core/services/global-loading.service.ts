import { Injectable, computed, signal } from '@angular/core';

/** Reference-counted global loading state. push() before async work, pop() when done. */
@Injectable({ providedIn: 'root' })
export class GlobalLoadingService {
  private readonly _count = signal(0);
  readonly isLoading = computed(() => this._count() > 0);

  push(): void {
    this._count.update((c) => c + 1);
  }

  pop(): void {
    this._count.update((c) => Math.max(0, c - 1));
  }
}
