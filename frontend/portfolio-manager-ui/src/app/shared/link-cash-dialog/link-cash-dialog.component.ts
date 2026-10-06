import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatNativeDateModule } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { startWith } from 'rxjs';
import { CashItem } from '../../core/models/portfolio.models';
import { CashStateService } from '../../core/services/cash-state.service';
import { DemoModeService } from '../../core/services/demo-mode.service';
import { AccountTypeSelectComponent } from '../account-type-select/account-type-select.component';

export interface LinkCashDialogData {
  /** create: no cash row exists for this trade leg yet. update: a linked row exists but no longer matches the trade. */
  mode: 'create' | 'update';
  /** out = buy (cash leaves the account), in = sell (cash arrives). */
  direction: 'out' | 'in';
  heading: string;
  detail: string;
  amount: number | null;
  accountType: string | null;
  /** yyyy-MM-dd of the trade itself, used only for the back-dating hint. */
  tradeDate: string | null;
  /** yyyy-MM-dd pre-filled as the cash row's date; defaults to today (ET). Used when catching up an old, unlinked trade. */
  defaultDate?: string | null;
  description: string;
  /** update mode: the linked row being corrected. */
  existing?: CashItem | null;
  /** create mode: an unlinked, manually entered row that looks like the same trade. */
  duplicate?: CashItem | null;
}

export interface LinkCashDialogResult {
  amount: number;
  accountType: string | null;
  /** yyyy-MM-dd */
  transactionDate: string;
  description: string;
}

/** Calendar date in America/New_York as yyyy-MM-dd — the same clock the backend ledger uses. */
export function easternToday(): string {
  return new Date().toLocaleDateString('en-CA', { timeZone: 'America/New_York' });
}

@Component({
  selector: 'app-link-cash-dialog',
  templateUrl: './link-cash-dialog.component.html',
  styleUrl: './link-cash-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    AccountTypeSelectComponent,
    CurrencyPipe,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatSelectModule,
    MatDatepickerModule,
    MatNativeDateModule,
    ReactiveFormsModule,
  ],
})
export class LinkCashDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly cashState = inject(CashStateService);
  protected readonly demoMode = inject(DemoModeService);
  private readonly dialogRef = inject(MatDialogRef<LinkCashDialogComponent, LinkCashDialogResult>);
  protected readonly data = inject<LinkCashDialogData>(MAT_DIALOG_DATA);

  protected readonly isUpdate = this.data.mode === 'update';
  protected readonly sign = this.data.direction === 'out' ? -1 : 1;
  protected readonly flowLabel = this.data.direction === 'out' ? 'Cash out' : 'Cash in';
  protected readonly typeLabel = this.data.direction === 'out' ? 'TradePurchase' : 'TradeProceeds';

  readonly form = this.fb.group({
    amount: [this.data.amount as number | null, [Validators.required, Validators.min(0.01)]],
    accountType: [this.data.accountType],
    transactionDate: [
      this.toDate(
        this.data.existing?.transactionDate ?? this.data.defaultDate ?? easternToday(),
      ) as Date | null,
      [Validators.required],
    ],
    description: [this.data.description, [Validators.required, Validators.maxLength(200)]],
  });

  private readonly formValue = toSignal(
    this.form.valueChanges.pipe(startWith(this.form.getRawValue())),
    { initialValue: this.form.getRawValue() },
  );

  /** Balance of the chosen account before/after this entry; an update replaces the row's current amount. */
  protected readonly balance = computed(() => {
    const { amount, accountType } = this.formValue();
    const key = accountType ?? '—';
    const current = this.cashState.totalsByAccount().find((t) => t.accountType === key)?.total ?? 0;
    const existing = this.data.existing;
    const replaced = existing && (existing.accountType ?? '—') === key ? existing.amount : 0;
    const after = current - replaced + this.sign * (amount ?? 0);
    return { account: key, current, after };
  });

  protected readonly goesNegative = computed(() => {
    const { current, after } = this.balance();
    return after < 0 && after < current;
  });

  /** The trade happened on a different day than the date the cash row will carry. */
  protected readonly showBackdateHint = computed(() => {
    const picked = this.formValue().transactionDate;
    return !!this.data.tradeDate && !!picked && this.formatDate(picked) !== this.data.tradeDate;
  });

  protected readonly blurAmounts = computed(
    () => this.demoMode.isDemoMode() && this.demoMode.demoStyle() === 'blur',
  );

  /** Informational amounts only — form inputs always hold the real values. */
  protected dv(v: number): number {
    return this.demoMode.isDemoMode() && this.demoMode.demoStyle() === 'fake'
      ? this.demoMode.maskValue(v)
      : v;
  }

  protected day(value: string | null | undefined): string {
    return value ? value.split('T')[0] : '—';
  }

  protected submit(): void {
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    this.dialogRef.close({
      amount: Math.round(v.amount! * 100) / 100,
      accountType: v.accountType ?? null,
      transactionDate: this.formatDate(v.transactionDate!),
      description: v.description!.trim(),
    });
  }

  protected skip(): void {
    this.dialogRef.close(undefined);
  }

  private toDate(isoDate: string): Date {
    return new Date(`${isoDate.split('T')[0]}T12:00:00`);
  }

  private formatDate(d: Date): string {
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }
}
