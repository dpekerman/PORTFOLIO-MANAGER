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
import {
  ValueScreenerSchedule,
  ValueScreenerScheduleStore,
} from '../../../../core/store/value-screener-schedule.store';
import { SettingsSectionComponent } from '../../../../shared/ui/settings-section/settings-section.component';
import { formDirtySignal } from '../../../../shared/utils/forms';
import { dateToTimeString, timeStrToDate } from '../../../../shared/utils/time';
import { ConfigSaveService } from '../../config-save.service';

@Component({
  selector: 'app-value-screener-tab',
  templateUrl: './value-screener-tab.component.html',
  styleUrl: './value-screener-tab.component.scss',
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
export class ValueScreenerTabComponent implements OnInit {
  private readonly store = inject(ValueScreenerScheduleStore);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly vsForm = this.fb.group({
    vsScheduleTime: [timeStrToDate('17:00'), [Validators.required]],
    vsScheduleEnabled: [true],
  });
  protected readonly savingVsSchedule = signal(false);

  constructor() {
    inject(ConfigSaveService).register(
      { id: 'Value Screener', dirty: formDirtySignal(this.vsForm), save: () => this.persist() },
      inject(DestroyRef),
    );

    effect(() => {
      const s = this.store.schedule();
      if (s && this.vsForm.pristine) {
        this.vsForm.setValue({
          vsScheduleTime: timeStrToDate(s.scheduledTimeEt),
          vsScheduleEnabled: s.enabled,
        });
      }
    });
  }

  ngOnInit(): void {
    this.store.load();
  }

  private current(): ValueScreenerSchedule {
    return {
      scheduledTimeEt: dateToTimeString(this.vsForm.value.vsScheduleTime),
      enabled: this.vsForm.value.vsScheduleEnabled ?? true,
    };
  }

  private persist(): Observable<unknown> {
    return this.store.save(this.current()).pipe(tap(() => this.vsForm.markAsPristine()));
  }

  protected saveVsSchedule(): void {
    if (this.vsForm.invalid) return;
    const { scheduledTimeEt, enabled } = this.current();
    this.savingVsSchedule.set(true);
    this.persist().subscribe({
      next: () => {
        this.savingVsSchedule.set(false);
        this.snackBar.open(
          `Value Screener schedule saved: ${scheduledTimeEt} ET, ${enabled ? 'Enabled' : 'Disabled'}.`,
          'OK',
          { duration: 4000 },
        );
      },
      error: () => {
        this.savingVsSchedule.set(false);
        this.snackBar.open('Failed to save Value Screener schedule.', 'Dismiss', {
          duration: 4000,
        });
      },
    });
  }
}
