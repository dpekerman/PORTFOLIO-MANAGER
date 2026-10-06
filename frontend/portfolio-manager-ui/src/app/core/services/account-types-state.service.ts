import { HttpErrorResponse } from '@angular/common/http';
import { DestroyRef, Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, firstValueFrom } from 'rxjs';
import { AccountType } from '../models/portfolio.models';
import { AccountTypesApiService } from './account-types-api.service';
import { AuthStateService } from './auth-state.service';

export function accountTypeError(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const body: unknown = error.error;
    if (typeof body === 'string') return body;
    if (body && typeof body === 'object' && 'detail' in body && typeof body.detail === 'string')
      return body.detail;
  }
  return 'Could not save/load account types. Refresh the list and try again.';
}

@Injectable({ providedIn: 'root' })
export class AccountTypesStateService {
  private readonly api = inject(AccountTypesApiService);
  private readonly auth = inject(AuthStateService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly _items = signal<AccountType[]>([]);
  private readonly _loading = signal(false);
  private readonly _loaded = signal(false);
  private readonly _saving = signal(false);
  private readonly _error = signal<string | null>(null);
  private readonly _renamed = signal<{ oldName: string; newName: string } | null>(null);
  private generation = 0;

  readonly items = this._items.asReadonly();
  readonly names = computed(() => this._items().map((x) => x.name));
  readonly loading = this._loading.asReadonly();
  readonly saving = this._saving.asReadonly();
  readonly error = this._error.asReadonly();
  readonly ready = computed(() => this._loaded() && !this._loading() && !this._error());
  readonly renamed = this._renamed.asReadonly();

  constructor() {
    effect(() => {
      const authenticated = this.auth.isAuthenticated();
      const userId = this.auth.currentUser()?.id;
      this.generation++;
      this._items.set([]);
      this._loaded.set(false);
      this._error.set(null);
      this._renamed.set(null);
      this._loading.set(false);
      this._saving.set(false);
      if (authenticated && userId) untracked(() => this.load());
    });
  }

  load(): void {
    if (!this.auth.isAuthenticated() || this._loading() || this._saving()) return;
    const generation = ++this.generation;
    this._loading.set(true);
    this._error.set(null);
    this.api
      .getAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (items) => {
          if (generation !== this.generation) return;
          this._items.set(items);
          this._loaded.set(true);
          this._loading.set(false);
        },
        error: (error) => {
          if (generation !== this.generation) return;
          this._error.set(accountTypeError(error));
          this._loading.set(false);
        },
      });
  }

  async add(name: string): Promise<void> {
    await this.mutate(this.api.add(name), (item) => {
      this._items.update((items) => [...items, item]);
    });
  }

  async rename(item: AccountType, name: string): Promise<void> {
    await this.mutate(this.api.rename(item, name), (result) => {
      this._items.update((items) => items.map((x) => (x.id === item.id ? result.item : x)));
      this._renamed.set({ oldName: result.oldName, newName: result.item.name });
    });
  }

  async delete(item: AccountType): Promise<void> {
    await this.mutate(this.api.delete(item), () => {
      this._items.update((items) => items.filter((x) => x.id !== item.id));
    });
  }

  private async mutate<T>(request: Observable<T>, apply: (result: T) => void): Promise<void> {
    if (this._saving()) throw new Error('An account type operation is already pending.');
    const generation = this.generation;
    this._saving.set(true);
    this._error.set(null);
    try {
      const result = await firstValueFrom(request.pipe(takeUntilDestroyed(this.destroyRef)));
      if (generation !== this.generation) throw new Error('The authenticated session changed.');
      apply(result);
    } catch (error: unknown) {
      if (generation === this.generation) this._error.set(accountTypeError(error));
      throw error;
    } finally {
      if (generation === this.generation) this._saving.set(false);
    }
  }
}
