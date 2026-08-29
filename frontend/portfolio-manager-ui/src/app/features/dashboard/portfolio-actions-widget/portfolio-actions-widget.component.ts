import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
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
  private readonly filter = signal<'ALL' | 'REQUIRED' | 'DEVELOPING' | 'INFORMATIONAL'>('ALL');
  protected readonly dashboard = inject(DashboardStateService);
  protected readonly actions = this.dashboard.portfolioActions;
  protected readonly loading = this.dashboard.actionsLoading;

  protected readonly holdingActions = computed(() =>
    this.filteredActions().filter((a) => a.isInPortfolio),
  );
  protected readonly watchlistActions = computed(() =>
    this.filteredActions().filter((a) => !a.isInPortfolio),
  );
  protected readonly activeFilter = this.filter.asReadonly();
  private readonly filteredActions = computed(() => {
    const filter = this.filter();
    return filter === 'ALL'
      ? this.actions()
      : this.actions().filter((a) => a.actionPriority === filter);
  });

  protected readonly requiredCount = computed(
    () => this.actions().filter((a) => a.actionPriority === 'REQUIRED').length,
  );
  protected readonly developingCount = computed(
    () => this.actions().filter((a) => a.actionPriority === 'DEVELOPING').length,
  );
  protected readonly informationalCount = computed(
    () => this.actions().filter((a) => a.actionPriority === 'INFORMATIONAL').length,
  );

  protected toggleFilter(filter: 'REQUIRED' | 'DEVELOPING' | 'INFORMATIONAL'): void {
    this.filter.update((current) => (current === filter ? 'ALL' : filter));
  }

  protected resetFilter(): void {
    this.filter.set('ALL');
  }

  protected severityCls(a: PortfolioActionDto): string {
    return `action-${a.actionSeverity} priority-${a.actionPriority.toLowerCase()}`;
  }
  protected scanIcon(scanType: string): string {
    return scanType === 'Oversold' ? 'trending_down' : 'trending_up';
  }

  protected channelTooltip(action: PortfolioActionDto): string {
    if (action.channelState === 'NONE' || action.channelState === 'CHANNEL_ACTIVE') return '';
    const touches = (action.channelTouchDetails ?? [])
      .map(
        (touch) =>
          `#${touch.touchNumber}  ${touch.touchDate.slice(0, 10)}\nRail: ${touch.railPrice.toFixed(2)}\nLow: ${touch.actualLow.toFixed(2)}\nBounce: +${touch.bounceATR.toFixed(2)} ATR`,
      )
      .join('\n\n');
    const interaction =
      action.priorConfirmedLowerTouches === 2
        ? '3rd Touch'
        : `${action.priorConfirmedLowerTouches + 1}th Touch`;
    return `RISING CHANNEL\n\nCURRENT STRUCTURE\nState: ${action.channelState}\nInteraction: ${interaction}\nQuality: ${action.channelQuality}/100\nEOD Close: ${action.eodClose.toFixed(2)}\nLower Rail: ${action.lowerRailToday.toFixed(2)}\nDistance: ${action.distanceToLowerRailPercent.toFixed(2)}%\nDistance ATR: ${action.distanceToLowerRailATR.toFixed(2)}\n\nTOUCH HISTORY\nConfirmed Touches: ${action.priorConfirmedLowerTouches}\n${touches}\n\nGAP\nNearest Open Gap Above: ${action.nearestOpenGapAbove?.toFixed(2) ?? '—'}`;
  }
}
