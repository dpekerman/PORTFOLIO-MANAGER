import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DashboardStateService } from '../../../core/services/dashboard-state.service';

@Component({
  selector: 'app-state-changes-widget',
  templateUrl: './state-changes-widget.component.html',
  styleUrl: './state-changes-widget.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, DecimalPipe, MatIconModule, MatProgressBarModule, MatTooltipModule],
})
export class StateChangesWidgetComponent {
  protected readonly dashboard = inject(DashboardStateService);
  protected readonly changes = this.dashboard.stateChanges;
  protected readonly loading = this.dashboard.stateChangesLoading;

  protected stateCls(state: string): string {
    if (state === 'Active' || state === 'FollowThrough') return 'state-active';
    if (state === 'Invalidated' || state === 'Reversed') return 'state-closed';
    if (state === 'Expired') return 'state-expired';
    return '';
  }

  protected scanIcon(scanType: string): string {
    return scanType === 'Oversold' ? 'trending_down' : 'trending_up';
  }
}
