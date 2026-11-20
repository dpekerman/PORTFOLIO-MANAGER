import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTimepickerModule } from '@angular/material/timepicker';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Observable, tap } from 'rxjs';
import { ConfigStore } from '../../../../core/store/config.store';
import { EodSettings, EodSettingsStore } from '../../../../core/store/eod-settings.store';
import { ScannerStore } from '../../../../core/store/scanner.store';
import { SettingsSectionComponent } from '../../../../shared/ui/settings-section/settings-section.component';
import { formDirtySignal } from '../../../../shared/utils/forms';
import { dateToTimeString, timeStrToDate } from '../../../../shared/utils/time';
import { ConfigSaveService } from '../../config-save.service';

@Component({
  selector: 'app-eod-window-tab',
  templateUrl: './eod-window-tab.component.html',
  styleUrl: './eod-window-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSlideToggleModule,
    MatTimepickerModule,
    MatTooltipModule,
    SettingsSectionComponent,
  ],
})
export class EodWindowTabComponent implements OnInit {
  private readonly store = inject(EodSettingsStore);
  private readonly configService = inject(ConfigStore);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly eodWindowActive = inject(ScannerStore).eodWindowActive;
  protected readonly savingEodSettings = signal(false);

  // Time controls hold Date | null (MatTimepickerInput); converted to "HH:mm" for the backend.
  protected readonly eodForm = this.fb.group({
    eodWindowStart: [
      timeStrToDate(this.configService.config().eodWindowStart),
      [Validators.required],
    ],
    eodWindowEnd: [timeStrToDate(this.configService.config().eodWindowEnd), [Validators.required]],
    eodWindowEnabled: [this.configService.config().eodWindowEnabled],
    eodOversoldRsiThreshold: [25, [Validators.required, Validators.min(1), Validators.max(49)]],
    eodOverboughtRsiThreshold: [75, [Validators.required, Validators.min(51), Validators.max(99)]],
  });

  constructor() {
    inject(ConfigSaveService).register(
      {
        id: 'EOD window',
        dirty: formDirtySignal(this.eodForm),
        save: () => this.persist(),
        reset: () => {
          const cfg = this.configService.config();
          this.eodForm.setValue({
            eodWindowStart: timeStrToDate(cfg.eodWindowStart),
            eodWindowEnd: timeStrToDate(cfg.eodWindowEnd),
            eodWindowEnabled: cfg.eodWindowEnabled,
            eodOversoldRsiThreshold: 25,
            eodOverboughtRsiThreshold: 75,
          });
        },
      },
      inject(DestroyRef),
    );

    // Show the current server-side state until the user starts editing
    effect(() => {
      const s = this.store.settings();
      if (s && this.eodForm.pristine) {
        this.eodForm.setValue({
          eodWindowStart: timeStrToDate(s.eodWindowStart),
          eodWindowEnd: timeStrToDate(s.eodWindowEnd),
          eodWindowEnabled: s.eodWindowEnabled,
          eodOversoldRsiThreshold: s.eodOversoldRsiThreshold,
          eodOverboughtRsiThreshold: s.eodOverboughtRsiThreshold,
        });
      }
    });
  }

  ngOnInit(): void {
    this.store.load();
  }

  private current(): EodSettings {
    const v = this.eodForm.value;
    return {
      eodWindowStart: dateToTimeString(v.eodWindowStart),
      eodWindowEnd: dateToTimeString(v.eodWindowEnd),
      eodWindowEnabled: v.eodWindowEnabled ?? true,
      eodOversoldRsiThreshold: v.eodOversoldRsiThreshold ?? 25,
      eodOverboughtRsiThreshold: v.eodOverboughtRsiThreshold ?? 75,
    };
  }

  private persist(): Observable<unknown> {
    return this.store.save(this.current()).pipe(tap(() => this.eodForm.markAsPristine()));
  }

  protected saveEodSettings(): void {
    if (this.eodForm.invalid) return;
    const s = this.current();
    this.savingEodSettings.set(true);
    this.persist().subscribe({
      next: () => {
        this.savingEodSettings.set(false);
        this.snackBar.open(
          `EOD window saved: ${s.eodWindowStart}–${s.eodWindowEnd} ET (${s.eodWindowEnabled ? 'Enabled' : 'Disabled'}). RSI <${s.eodOversoldRsiThreshold}/>${s.eodOverboughtRsiThreshold}.`,
          'OK',
          { duration: 4000 },
        );
      },
      error: () => {
        this.savingEodSettings.set(false);
        this.snackBar.open('Failed to save EOD settings to server.', 'Dismiss', {
          duration: 4000,
        });
      },
    });
  }
}
