import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DashboardStateService } from '../../../core/services/dashboard-state.service';
import { formatTrendShift } from '../../../core/technical-display';

@Component({
  selector: 'app-priority-candidates-widget',
  templateUrl: './priority-candidates-widget.component.html',
  styleUrl: './priority-candidates-widget.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CurrencyPipe, DecimalPipe, MatIconModule, MatProgressBarModule, MatTooltipModule],
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

  protected eodBadgeLabel(score: {
    latestEodSignalState?: string | null;
    latestEodIsNew?: boolean;
    latestEodIsInvalidated?: boolean;
  }): string {
    if (score.latestEodIsInvalidated) return 'EOD X';
    if (score.latestEodSignalState === 'Active') return score.latestEodIsNew ? 'EOD NEW' : 'EOD';
    return 'EOD DEV';
  }

  protected eodTooltip(score: {
    latestEodSignalState?: string | null;
    latestEodScanType?: string | null;
  }): string {
    return `Latest EOD signal\nState: ${score.latestEodSignalState ?? 'n/a'}\nScan: ${score.latestEodScanType ?? 'n/a'}\nScore boost: +2 technical points`;
  }

  /** Strip leading emoji + space from trend shift string. */
  protected trendLabel(raw: string): string {
    return formatTrendShift(raw, '');
  }

  /** CSS modifier class derived from the leading emoji. */
  protected trendDotCls(raw: string): string {
    if (raw.startsWith('🟢')) return 'dot-green';
    if (raw.startsWith('🟡')) return 'dot-yellow';
    if (raw.startsWith('🔴')) return 'dot-red';
    return 'dot-neutral';
  }
}
