import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DashboardStateService } from '../../../core/services/dashboard-state.service';

@Component({
  selector: 'app-portfolio-actions-widget',
  templateUrl: './portfolio-actions-widget.component.html',
  styleUrl: './portfolio-actions-widget.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, MatIconModule, MatProgressBarModule, MatTooltipModule],
})
export class PortfolioActionsWidgetComponent {
  protected readonly dashboard = inject(DashboardStateService);
  protected readonly actions = this.dashboard.portfolioActions;
  protected readonly loading = this.dashboard.actionsLoading;

  protected severityCls(severity: string): string {
    return `action-${severity}`;
  }

  protected scanIcon(scanType: string): string {
    return scanType === 'Oversold' ? 'trending_down' : 'trending_up';
  }
}
