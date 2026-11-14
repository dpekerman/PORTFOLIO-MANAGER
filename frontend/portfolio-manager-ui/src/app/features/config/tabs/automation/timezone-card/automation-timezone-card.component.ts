import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AutomationStore } from '../../../../../core/store/automation.store';

/** Read-only timezone diagnostics: verifies the Scheduled Task still matches Eastern Time. */
@Component({
  selector: 'app-automation-timezone-card',
  templateUrl: './automation-timezone-card.component.html',
  styleUrl: './automation-timezone-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, MatButtonModule, MatIconModule, MatTooltipModule],
})
export class AutomationTimezoneCardComponent {
  protected readonly automationState = inject(AutomationStore);
}
