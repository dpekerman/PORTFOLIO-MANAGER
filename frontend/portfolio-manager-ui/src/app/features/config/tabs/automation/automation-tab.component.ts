import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, effect, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTimepickerModule } from '@angular/material/timepicker';
import { MatTooltipModule } from '@angular/material/tooltip';
import { NotificationApiService } from '../../../../core/services/notification-api.service';
import { AutomationStore } from '../../../../core/store/automation.store';
import { SettingsSectionComponent } from '../../../../shared/ui/settings-section/settings-section.component';
import { dateToTimeString, timeStrToDate } from '../../../../shared/utils/time';
import { AutomationBannersComponent } from './banners/automation-banners.component';
import { AutomationHistoryCardComponent } from './history-card/automation-history-card.component';
import { AutomationLastRunCardComponent } from './last-run-card/automation-last-run-card.component';
import { AutomationTimezoneCardComponent } from './timezone-card/automation-timezone-card.component';

@Component({
  selector: 'app-automation-tab',
  templateUrl: './automation-tab.component.html',
  styleUrl: './automation-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSlideToggleModule,
    MatTimepickerModule,
    MatTooltipModule,
    SettingsSectionComponent,
    AutomationBannersComponent,
    AutomationTimezoneCardComponent,
    AutomationLastRunCardComponent,
    AutomationHistoryCardComponent,
  ],
})
export class AutomationTabComponent implements OnInit {
  protected readonly automationState = inject(AutomationStore);
  private readonly notificationApi = inject(NotificationApiService);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly sendingAutomationTestEmail = signal(false);
  protected readonly sendingScoreAlertTest = signal(false);

  // Wake/keep-awake times are machine-availability settings, deliberately separate from the EOD
  // window / Value Screener business-time rules (echoed read-only). Time controls use Date | null.
  protected readonly automationForm = this.fb.group({
    enabled: [false],
    wakeTimeEt: [timeStrToDate('15:15'), [Validators.required]],
    keepAwakeUntilEtOverride: [null as Date | null],
    completionGraceMinutes: [15, [Validators.required, Validators.min(0), Validators.max(120)]],
    maxPollMinutes: [90, [Validators.required, Validators.min(5), Validators.max(360)]],
    missedRunAlertTimeEt: [timeStrToDate('17:30'), [Validators.required]],
  });

  constructor() {
    effect(() => {
      const s = this.automationState.settings();
      if (s && this.automationForm.pristine) {
        this.automationForm.setValue({
          enabled: s.enabled,
          wakeTimeEt: timeStrToDate(s.wakeTimeEt),
          keepAwakeUntilEtOverride: s.keepAwakeUntilEtOverride
            ? timeStrToDate(s.keepAwakeUntilEtOverride)
            : null,
          completionGraceMinutes: s.completionGraceMinutes,
          maxPollMinutes: s.maxPollMinutes,
          missedRunAlertTimeEt: timeStrToDate(s.missedRunAlertTimeEt),
        });
      }
    });

    effect(() => {
      const err = this.automationState.error();
      if (err) this.snackBar.open(err, 'Dismiss', { duration: 6000 });
    });
  }

  ngOnInit(): void {
    this.automationState.loadSettings();
    this.automationState.loadLastRun();
    this.automationState.loadHistory();
    this.automationState.loadTaskStatus();
    this.automationState.loadTimezoneDiagnostics();
  }

  protected saveAutomationSettings(): void {
    if (this.automationForm.invalid) return;
    const v = this.automationForm.value;
    this.automationState.saveSettings({
      enabled: v.enabled ?? false,
      wakeTimeEt: dateToTimeString(v.wakeTimeEt) || '15:15',
      keepAwakeUntilEtOverride: v.keepAwakeUntilEtOverride
        ? dateToTimeString(v.keepAwakeUntilEtOverride)
        : null,
      completionGraceMinutes: v.completionGraceMinutes ?? 15,
      maxPollMinutes: v.maxPollMinutes ?? 90,
      missedRunAlertTimeEt: dateToTimeString(v.missedRunAlertTimeEt) || '17:30',
    });
    this.automationForm.markAsPristine();
  }

  protected clearKeepAwakeOverride(): void {
    this.automationForm.controls.keepAwakeUntilEtOverride.setValue(null);
    this.automationForm.controls.keepAwakeUntilEtOverride.markAsDirty();
  }

  protected runAutomationNow(): void {
    this.automationState.runNow();
  }

  protected fixMissingData(): void {
    this.automationState.recoverMissedData();
  }

  protected backupNow(): void {
    this.automationState.backupNow();
  }

  protected testAutomationWake(): void {
    this.automationState.testWake();
  }

  protected enableAutomation(): void {
    this.automationState.setup();
  }

  protected rotateAutomationSecret(): void {
    this.automationState.rotateSecret();
  }

  protected sendAutomationTestEmail(): void {
    this.sendingAutomationTestEmail.set(true);
    this.notificationApi.sendAutomationTestEmail().subscribe({
      next: (result) => {
        this.sendingAutomationTestEmail.set(false);
        const message = result.success
          ? `${result.message ?? 'Automation test email sent.'} Check the registered inboxes.`
          : (result.error ?? 'Automation test email failed.');
        this.snackBar.open(message, result.success ? 'OK' : 'Dismiss', {
          duration: result.success ? 6000 : 9000,
        });
      },
      error: (err) => {
        this.sendingAutomationTestEmail.set(false);
        const message = err?.error?.error ?? err?.message ?? 'Automation test email failed.';
        this.snackBar.open(message, 'Dismiss', { duration: 9000 });
      },
    });
  }

  protected sendScoreAlertTest(): void {
    this.sendingScoreAlertTest.set(true);
    this.notificationApi.sendScoreAlertTest().subscribe({
      next: (result) => {
        this.sendingScoreAlertTest.set(false);
        this.snackBar.open(
          result.success
            ? (result.message ?? 'Test score alert sent.')
            : (result.error ?? 'Test score alert failed.'),
          'OK',
          { duration: 6000 },
        );
      },
      error: (err) => {
        this.sendingScoreAlertTest.set(false);
        this.snackBar.open(
          err?.error?.error ?? err?.message ?? 'Test score alert failed.',
          'Dismiss',
          { duration: 9000 },
        );
      },
    });
  }
}
