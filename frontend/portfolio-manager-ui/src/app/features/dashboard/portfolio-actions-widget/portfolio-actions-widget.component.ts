import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PortfolioActionDto } from '../../../core/models/portfolio.models';
import { priceStructureLabel, priceStructureTooltip } from '../../../core/price-structure-display';
import { DashboardStateService } from '../../../core/services/dashboard-state.service';
import { DemoModeService } from '../../../core/services/demo-mode.service';

@Component({
  selector: 'app-portfolio-actions-widget',
  templateUrl: './portfolio-actions-widget.component.html',
  styleUrl: './portfolio-actions-widget.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, MatIconModule, MatProgressBarModule, MatTooltipModule],
})
export class PortfolioActionsWidgetComponent {
  private readonly demoMode = inject(DemoModeService);
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

  protected structureLabel(action: PortfolioActionDto): string {
    return priceStructureLabel(action.priceStructure, (value) => this.demoMode.maskValue(value));
  }

  protected structureTooltip(action: PortfolioActionDto): string {
    return priceStructureTooltip(action.priceStructure, (value) => this.demoMode.maskValue(value));
  }
}
