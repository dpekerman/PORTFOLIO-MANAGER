import { CurrencyPipe, DecimalPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
} from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActionScoreDto, PriorityCandidateRow } from '../../../core/models/portfolio.models';
import { DashboardStore } from '../../../core/store/dashboard.store';
import { currencyCodeForAnalysis, formatTrendShift } from '../../../core/technical-display';

export type { PriorityCandidateRow };

@Component({
  selector: 'app-priority-candidates-widget',
  templateUrl: './priority-candidates-widget.component.html',
  styleUrl: './priority-candidates-widget.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CurrencyPipe, DecimalPipe, MatIconModule, MatProgressBarModule, MatTooltipModule],
})
export class PriorityCandidatesWidgetComponent implements OnInit {
  protected readonly dashboard = inject(DashboardStore);

  protected readonly scores = this.dashboard.actionScores;
  protected readonly loading = computed(
    () => this.dashboard.actionScoresLoading() || this.dashboard.priorityCandidatesLoading(),
  );

  /** Watchlist-only candidates the backend's canonical Final Action + score gate accepted, ranked by score. */
  protected readonly eligibleCandidates = computed<PriorityCandidateRow[]>(() =>
    this.dashboard
      .priorityCandidates()
      .filter((row) => row.eligibility.eligible)
      .sort((a, b) => b.score.totalScore - a.score.totalScore),
  );

  ngOnInit(): void {
    this.dashboard.loadActionScores();
    this.dashboard.loadPriorityCandidates();
  }

  protected badgeCls(priorityLevel: string): string {
    return priorityLevel === 'HIGH' ? 'badge-high' : 'badge-watch';
  }

  protected badgeLabel(priorityLevel: string): string {
    return priorityLevel === 'HIGH' ? 'HIGH' : 'WATCH';
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

  protected analysisCurrency(score: ActionScoreDto): string {
    return currencyCodeForAnalysis(score.symbol, score.analysisCurrency);
  }
}
