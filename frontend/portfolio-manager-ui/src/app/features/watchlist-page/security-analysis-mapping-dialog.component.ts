import { ChangeDetectionStrategy, Component, Inject, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { SecurityAnalysisMappingStateService } from '../../core/services/security-analysis-mapping-state.service';

export interface SecurityAnalysisMappingDialogData {
  tradingTicker: string;
}

@Component({
  selector: 'app-security-analysis-mapping-dialog',
  templateUrl: './security-analysis-mapping-dialog.component.html',
  styleUrl: './security-analysis-mapping-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatDialogModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    ReactiveFormsModule,
  ],
})
export class SecurityAnalysisMappingDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly state = inject(SecurityAnalysisMappingStateService);
  private readonly dialogRef = inject(MatDialogRef<SecurityAnalysisMappingDialogComponent>);

  protected readonly mapping = this.state.mapping;
  protected readonly loading = this.state.loading;
  protected readonly error = this.state.error;
  protected validationMessage = '';

  readonly form = this.fb.group({
    underlyingTicker: ['', [Validators.required, Validators.maxLength(20)]],
    useUnderlyingForAnalysis: [true],
  });

  constructor(@Inject(MAT_DIALOG_DATA) protected readonly data: SecurityAnalysisMappingDialogData) {
    void this.load();
  }

  protected async validate(): Promise<void> {
    if (this.form.controls.underlyingTicker.invalid) return;
    this.validationMessage = '';
    try {
      await this.state.validate(
        this.data.tradingTicker,
        this.form.controls.underlyingTicker.value ?? '',
      );
      this.validationMessage = 'Ticker validated.';
    } catch {
      this.validationMessage =
        'The ticker could not be validated as a supported security with sufficient market history.';
    }
  }

  protected async save(): Promise<void> {
    if (this.form.invalid) return;
    const saved = await this.state.save(this.data.tradingTicker, {
      underlyingTicker: (this.form.controls.underlyingTicker.value ?? '').trim().toUpperCase(),
      useUnderlyingForAnalysis: this.form.controls.useUnderlyingForAnalysis.value ?? true,
    });
    if (saved) this.dialogRef.close(true);
  }

  protected async remove(): Promise<void> {
    if (await this.state.remove(this.data.tradingTicker)) this.dialogRef.close(true);
  }

  protected cancel(): void {
    this.dialogRef.close(false);
  }

  private async load(): Promise<void> {
    const mapping = await this.state.load(this.data.tradingTicker);
    if (!mapping) return;
    this.form.patchValue({
      underlyingTicker: mapping.usesUnderlyingSecurity ? mapping.analysisTicker : '',
      useUnderlyingForAnalysis: mapping.usesUnderlyingSecurity,
    });
  }
}
