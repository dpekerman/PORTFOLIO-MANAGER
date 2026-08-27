import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PortfolioActionDto } from '../../../core/models/portfolio.models';
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

  protected readonly holdingActions = computed(() => this.actions().filter((a) => a.isInPortfolio));
  protected readonly watchlistActions = computed(() =>
    this.actions().filter((a) => !a.isInPortfolio),
  );

  protected readonly requiredCount = computed(
    () => this.actions().filter((a) => a.actionPriority === 'REQUIRED').length,
  );
  protected readonly developingCount = computed(
    () => this.actions().filter((a) => a.actionPriority === 'DEVELOPING').length,
  );
  protected readonly informationalCount = computed(
    () => this.actions().filter((a) => a.actionPriority === 'INFORMATIONAL').length,
  );

  protected severityCls(a: PortfolioActionDto): string {
    return `action-${a.actionSeverity} priority-${a.actionPriority.toLowerCase()}`;
  }
  protected scanIcon(scanType: string): string {
    return scanType === 'Oversold' ? 'trending_down' : 'trending_up';
  }
}
