import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DashboardStateService } from '../../../core/services/dashboard-state.service';

@Component({
  selector: 'app-priority-candidates-widget',
  templateUrl: './priority-candidates-widget.component.html',
  styleUrl: './priority-candidates-widget.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, MatIconModule, MatProgressBarModule, MatTooltipModule],
})
export class PriorityCandidatesWidgetComponent implements OnInit {
  protected readonly dashboard = inject(DashboardStateService);
  protected readonly scores = this.dashboard.actionScores;
  protected readonly loading = this.dashboard.actionScoresLoading;

  ngOnInit(): void {
    this.dashboard.loadActionScores();
  }

  protected badgeCls(badge: string): string {
    if (badge === 'HIGH_PRIORITY') return 'badge-high';
    if (badge === 'WATCH') return 'badge-watch';
    return 'badge-no-add';
  }

  protected badgeLabel(badge: string): string {
    if (badge === 'HIGH_PRIORITY') return 'HIGH';
    if (badge === 'WATCH') return 'WATCH';
    return 'NO ADD';
  }
}
