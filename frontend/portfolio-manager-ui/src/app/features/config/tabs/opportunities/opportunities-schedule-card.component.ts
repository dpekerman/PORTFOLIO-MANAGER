import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, effect, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTimepickerModule } from '@angular/material/timepicker';
import { ScheduleDay } from '../../../../core/models/opportunities.models';
import { OpportunityScheduleStore } from '../../../../core/store/opportunity-schedule.store';
import { SettingsSectionComponent } from '../../../../shared/ui/settings-section/settings-section.component';
import { dateToTimeString, timeStrToDate } from '../../../../shared/utils/time';

const WEEKDAYS: { value: ScheduleDay; label: string }[] = [
  { value: 'Monday', label: 'Mon' },
  { value: 'Tuesday', label: 'Tue' },
  { value: 'Wednesday', label: 'Wed' },
  { value: 'Thursday', label: 'Thu' },
  { value: 'Friday', label: 'Fri' },
];

/**
 * Day/time picker for the automatic Trading Opportunities refresh. Same behaviour as the Automation tab: edit, Save,
 * and the backend scheduler runs the refresh on the chosen market days (holidays are skipped automatically).
 */
@Component({
  selector: 'app-opportunities-schedule-card',
  templateUrl: './opportunities-schedule-card.component.html',
  styleUrl: './opportunities-schedule-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSlideToggleModule,
    MatTimepickerModule,
    SettingsSectionComponent,
  ],
})
export class OpportunitiesScheduleCardComponent implements OnInit {
  protected readonly store = inject(OpportunityScheduleStore);
  protected readonly weekdays = WEEKDAYS;

  protected readonly enabled = signal(false);
  protected readonly time = signal<Date | null>(timeStrToDate('18:05'));
  protected readonly days = signal<ScheduleDay[]>([]);

  constructor() {
    effect(() => {
      const s = this.store.schedule();
      if (!s) return;
      this.enabled.set(s.enabled);
      this.time.set(timeStrToDate(s.timeEt));
      this.days.set(s.days);
    });
  }

  ngOnInit(): void {
    this.store.load();
  }

  protected canSave(): boolean {
    return !!this.time() && this.days().length > 0 && !this.store.saving();
  }

  protected save(): void {
    if (!this.canSave()) return;
    this.store.save({ enabled: this.enabled(), timeEt: dateToTimeString(this.time()), days: this.days() });
  }
}
