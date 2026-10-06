import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  effect,
  forwardRef,
  inject,
  signal,
} from '@angular/core';
import {
  AbstractControl,
  ControlValueAccessor,
  NG_VALIDATORS,
  NG_VALUE_ACCESSOR,
  ValidationErrors,
  Validator,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { AccountTypesStateService } from '../../core/services/account-types-state.service';

@Component({
  selector: 'app-account-type-select',
  templateUrl: './account-type-select.component.html',
  styleUrl: './account-type-select.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatFormFieldModule, MatSelectModule, MatButtonModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => AccountTypeSelectComponent),
      multi: true,
    },
    {
      provide: NG_VALIDATORS,
      useExisting: forwardRef(() => AccountTypeSelectComponent),
      multi: true,
    },
  ],
})
export class AccountTypeSelectComponent implements ControlValueAccessor, Validator, OnInit {
  protected readonly state = inject(AccountTypesStateService);
  protected readonly value = signal<string | null>(null);
  protected readonly disabled = signal(false);
  private onChange: (value: string | null) => void = () => {};
  private onTouched: () => void = () => {};
  private onValidationChange: () => void = () => {};

  constructor() {
    effect(() => {
      this.state.ready();
      this.state.names();
      this.onValidationChange();
    });
  }

  ngOnInit(): void {
    this.state.load();
  }
  writeValue(value: string | null): void {
    this.value.set(value);
  }
  registerOnChange(fn: (value: string | null) => void): void {
    this.onChange = fn;
  }
  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }
  setDisabledState(disabled: boolean): void {
    this.disabled.set(disabled);
  }
  registerOnValidatorChange(fn: () => void): void {
    this.onValidationChange = fn;
  }
  validate(control: AbstractControl): ValidationErrors | null {
    if (!this.state.ready()) return { accountTypesUnavailable: true };
    return control.value && !this.state.names().includes(control.value)
      ? { accountTypeStale: true }
      : null;
  }
  protected select(value: string | null): void {
    this.value.set(value);
    this.onChange(value);
    this.onTouched();
  }
  protected touched(): void {
    this.onTouched();
  }
}
