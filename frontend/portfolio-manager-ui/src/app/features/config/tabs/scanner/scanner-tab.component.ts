import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { of } from 'rxjs';
import { PortfolioApiService } from '../../../../core/services/portfolio-api.service';
import { ConfigStore } from '../../../../core/store/config.store';
import { ScannerStore } from '../../../../core/store/scanner.store';
import { SettingsSectionComponent } from '../../../../shared/ui/settings-section/settings-section.component';
import { SubCardComponent } from '../../../../shared/ui/sub-card/sub-card.component';
import { formDirtySignal } from '../../../../shared/utils/forms';
import { ConfigSaveService } from '../../config-save.service';

@Component({
  selector: 'app-scanner-tab',
  templateUrl: './scanner-tab.component.html',
  styleUrl: './scanner-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTooltipModule,
    SettingsSectionComponent,
    SubCardComponent,
  ],
})
export class ScannerTabComponent {
  private readonly configService = inject(ConfigStore);
  private readonly api = inject(PortfolioApiService);
  private readonly scannerState = inject(ScannerStore);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);

  readonly SCAN_INTERVAL_OPTIONS: { label: string; value: number }[] = [
    { label: '0:00 — Disabled', value: 0 },
    { label: '0:30 (30 min)', value: 1800 },
    { label: '1:00 (1 hour)', value: 3600 },
    { label: '1:30', value: 5400 },
    { label: '2:00', value: 7200 },
    { label: '3:00', value: 10800 },
    { label: '4:00', value: 14400 },
    { label: '6:00', value: 21600 },
    { label: '8:00', value: 28800 },
    { label: '12:00', value: 43200 },
    { label: '24:00', value: 86400 },
  ];

  readonly APP_REFRESH_OPTIONS: { label: string; value: number }[] = [
    { label: '0:00 — Disabled', value: 0 },
    { label: '1:00 (1 min)', value: 60 },
    { label: '2:00 (2 min)', value: 120 },
    { label: '5:00 (5 min)', value: 300 },
    { label: '10:00 (10 min)', value: 600 },
    { label: '15:00 (15 min)', value: 900 },
    { label: '30:00 (30 min)', value: 1800 },
    { label: '60:00 (1 hour)', value: 3600 },
  ];

  protected readonly form = this.fb.group({
    scanIntervalSeconds: [this.configService.config().scanIntervalSeconds, [Validators.required]],
    appRefreshSeconds: [this.configService.config().appRefreshSeconds, [Validators.required]],
    rsiOversoldThreshold: [
      this.configService.config().rsiOversoldThreshold,
      [Validators.required, Validators.min(1), Validators.max(49)],
    ],
    rsiOverboughtThreshold: [
      this.configService.config().rsiOverboughtThreshold,
      [Validators.required, Validators.min(51), Validators.max(99)],
    ],
    sessionTimeoutMinutes: [
      this.configService.config().sessionTimeoutMinutes,
      [Validators.required, Validators.min(0), Validators.max(1440)],
    ],
  });

  constructor() {
    const dirty = formDirtySignal(this.form);
    inject(ConfigSaveService).register(
      {
        id: 'Scanner',
        dirty,
        save: () => {
          if (this.form.valid) this.applyChanges();
          return of(null);
        },
        reset: () => this.reset(false),
      },
      inject(DestroyRef),
    );
  }

  private applyChanges(): void {
    this.configService.update({
      scanIntervalSeconds: this.form.value.scanIntervalSeconds ?? 300,
      appRefreshSeconds: this.form.value.appRefreshSeconds ?? 120,
      rsiOversoldThreshold: this.form.value.rsiOversoldThreshold ?? 30,
      rsiOverboughtThreshold: this.form.value.rsiOverboughtThreshold ?? 75,
      sessionTimeoutMinutes: this.form.value.sessionTimeoutMinutes ?? 480,
    });
    this.form.markAsPristine();
    // Clear server RSI cache so the next scan uses the new thresholds
    this.api.clearRsiCache().subscribe({
      complete: () => this.scannerState.refresh(true),
      error: () => this.scannerState.refresh(true),
    });
  }

  protected save(): void {
    if (this.form.invalid) return;
    this.applyChanges();
    this.snackBar.open('Settings saved. RSI Scanner will refresh with new thresholds.', 'OK', {
      duration: 4000,
    });
  }

  protected reset(notify = true): void {
    this.configService.reset();
    const cfg = this.configService.config();
    this.form.setValue({
      scanIntervalSeconds: cfg.scanIntervalSeconds,
      appRefreshSeconds: cfg.appRefreshSeconds,
      rsiOversoldThreshold: cfg.rsiOversoldThreshold,
      rsiOverboughtThreshold: cfg.rsiOverboughtThreshold,
      sessionTimeoutMinutes: cfg.sessionTimeoutMinutes,
    });
    this.form.markAsPristine();
    if (notify) this.snackBar.open('Settings reset to defaults.', 'OK', { duration: 3000 });
  }
}
