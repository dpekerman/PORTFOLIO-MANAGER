import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AccountType } from '../../../core/models/portfolio.models';
import {
  accountTypeError,
  AccountTypesStateService,
} from '../../../core/services/account-types-state.service';
import { AuthStateService } from '../../../core/services/auth-state.service';
import { ConfirmDialogComponent } from '../../../shared/confirm-dialog/confirm-dialog.component';

type SortKey = 'name' | 'stockCount' | 'optionCount' | 'cashCount';

@Component({
  selector: 'app-account-types',
  templateUrl: './account-types.component.html',
  styleUrl: './account-types.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    NgTemplateOutlet,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatTooltipModule,
  ],
})
export class AccountTypesComponent implements OnInit {
  protected readonly state = inject(AccountTypesStateService);
  protected readonly auth = inject(AuthStateService);
  private readonly fb = inject(FormBuilder);
  private readonly dialog = inject(MatDialog);
  private readonly destroyRef = inject(DestroyRef);
  private readonly snack = inject(MatSnackBar);
  protected readonly editing = signal<AccountType | null>(null);
  protected readonly adding = signal(false);
  protected readonly sortKey = signal<SortKey>('name');
  protected readonly sortDir = signal<'asc' | 'desc'>('asc');
  protected readonly columns: { key: SortKey; label: string }[] = [
    { key: 'name', label: 'Name' },
    { key: 'stockCount', label: 'Stocks' },
    { key: 'optionCount', label: 'Options' },
    { key: 'cashCount', label: 'Cash' },
  ];
  protected readonly sortedItems = computed(() => {
    const key = this.sortKey();
    const direction = this.sortDir() === 'asc' ? 1 : -1;
    return [...this.state.items()].sort((a, b) => {
      const result =
        key === 'name'
          ? a.name.localeCompare(b.name, undefined, { sensitivity: 'base' })
          : a[key] - b[key];
      return (result || a.id - b.id) * direction;
    });
  });
  protected readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(120), Validators.pattern(/\S/)]],
  });

  ngOnInit(): void {
    this.state.load();
  }
  protected toggleSort(key: SortKey): void {
    if (this.sortKey() === key) {
      this.sortDir.update((direction) => (direction === 'asc' ? 'desc' : 'asc'));
    } else {
      this.sortKey.set(key);
      this.sortDir.set('asc');
    }
  }
  protected ariaSort(key: SortKey): 'ascending' | 'descending' | 'none' {
    if (this.sortKey() !== key) return 'none';
    return this.sortDir() === 'asc' ? 'ascending' : 'descending';
  }
  protected usage(item: AccountType): number {
    return item.stockCount + item.optionCount + item.cashCount;
  }
  protected startAdd(): void {
    this.editing.set(null);
    this.form.reset();
    this.adding.set(true);
  }
  protected edit(item: AccountType): void {
    this.adding.set(false);
    this.editing.set(item);
    this.form.setValue({ name: item.name });
  }
  protected cancel(): void {
    this.editing.set(null);
    this.adding.set(false);
    this.form.reset();
  }
  protected save(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.state.saving()) return;
    const name = this.form.getRawValue().name.trim();
    const item = this.editing();
    if (!item) {
      void this.commit(() => this.state.add(name));
      return;
    }
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Rename account type',
          message: `Rename "${item.name}" to "${name}" for all users? This updates ${this.usage(item)} stock, option and cash records, including closed transactions. Amounts and dates will not change.`,
          confirmLabel: 'Rename everywhere',
        },
      })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((confirmed) => {
        if (confirmed) void this.commit(() => this.state.rename(item, name));
      });
  }
  protected remove(item: AccountType): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        data: {
          title: 'Delete unused account type',
          message: `Delete "${item.name}" from the shared list?`,
          confirmLabel: 'Delete',
          danger: true,
        },
      })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((confirmed) => {
        if (confirmed) void this.commit(() => this.state.delete(item));
      });
  }
  private async commit(operation: () => Promise<void>): Promise<void> {
    try {
      await operation();
      this.cancel();
      this.snack.open('Account types saved.', 'OK', { duration: 3000 });
    } catch (error: unknown) {
      this.snack.open(accountTypeError(error), 'Dismiss', { duration: 7000 });
    }
  }
}
