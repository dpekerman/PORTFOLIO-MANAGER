import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { AutomationStore } from '../../../../../core/store/automation.store';
import { automationStatusClass, valueScreenerStatusLabel } from '../automation-status';

/** Step-by-step timeline of the most recent automation run. */
@Component({
  selector: 'app-automation-last-run-card',
  templateUrl: './automation-last-run-card.component.html',
  styleUrl: './automation-last-run-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatIconModule],
})
export class AutomationLastRunCardComponent {
  protected readonly automationState = inject(AutomationStore);
  protected readonly statusClass = automationStatusClass;
  protected readonly valueScreenerLabel = valueScreenerStatusLabel;
}
