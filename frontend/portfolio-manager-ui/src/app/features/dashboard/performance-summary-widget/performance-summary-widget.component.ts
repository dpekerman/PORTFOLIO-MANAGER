import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DashboardStateService } from '../../../core/services/dashboard-state.service';
import { DemoModeService } from '../../../core/services/demo-mode.service';

@Component({
  selector: 'app-performance-summary-widget',
  templateUrl: './performance-summary-widget.component.html',
  styleUrl: './performance-summary-widget.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, MatIconModule, MatProgressBarModule, MatTooltipModule],
})
export class PerformanceSummaryWidgetComponent {
  protected readonly dashboard = inject(DashboardStateService);
  private readonly demoMode = inject(DemoModeService);
  protected readonly summary = this.dashboard.performanceSummary;
  protected readonly loading = this.dashboard.performanceSummaryLoading;

  protected signCls(v: number): string {
    return v >= 0 ? 'pos' : 'neg';
  }
  protected sign(v: number): string {
    return v >= 0 ? '+' : '';
  }
  protected maskVal(v: number): number {
    return this.demoMode.maskValue(v);
  }
}
