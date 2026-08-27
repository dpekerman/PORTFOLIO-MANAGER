import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DashboardStateService } from '../../../core/services/dashboard-state.service';

@Component({
  selector: 'app-market-leadership-widget',
  templateUrl: './market-leadership-widget.component.html',
  styleUrl: './market-leadership-widget.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, MatIconModule, MatProgressBarModule, MatTooltipModule],
})
export class MarketLeadershipWidgetComponent {
  protected readonly dashboard = inject(DashboardStateService);
  protected readonly leadership = this.dashboard.marketLeadership;
  protected readonly loading = this.dashboard.marketLeadershipLoading;

  protected leadershipCls(label: string): string {
    if (label === 'Strong') return 'ml-strong';
    if (label === 'Improving') return 'ml-improving';
    if (label === 'Weakening') return 'ml-weakening';
    if (label === 'Declining') return 'ml-declining';
    return 'ml-neutral';
  }
}
