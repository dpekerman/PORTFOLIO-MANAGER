import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { ActionCenterFilter, OpenDecision } from '../../../core/models/portfolio.models';
import { filterDecisions, OpenDecisionsStore } from '../../../core/store/open-decisions.store';

interface FilterChip {
  filter: ActionCenterFilter;
  label: string;
  count: number;
}

/**
 * Action Center: the queue of current, validated decisions (entry / add, trim / exit, risk review).
 * It presents the recommendation and its evidence and opens the Signal Tracker; it never executes a trade.
 */
@Component({
  selector: 'app-portfolio-actions-widget',
  templateUrl: './portfolio-actions-widget.component.html',
  styleUrl: './portfolio-actions-widget.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatButtonModule, MatIconModule, MatProgressBarModule, MatTooltipModule, RouterLink],
})
export class PortfolioActionsWidgetComponent {
  private readonly store = inject(OpenDecisionsStore);
  private readonly datePipe = new DatePipe('en-US');

  protected readonly items = this.store.items;
  protected readonly loading = this.store.loading;
  protected readonly error = this.store.error;
  protected readonly activeFilter = this.store.filter;
  protected readonly hasAny = computed(() => this.store.allItems().length > 0);

  protected readonly chips = computed<FilterChip[]>(() => {
    const all = this.store.allItems();
    const count = (filter: ActionCenterFilter) => filterDecisions(all, filter).length;
    return [
      { filter: 'Holdings', label: 'Holdings', count: count('Holdings') },
      { filter: 'Watchlist', label: 'Watchlist', count: count('Watchlist') },
    ];
  });

  protected readonly validationState = computed(() => this.store.validation()?.state ?? 'Pending');
  protected readonly pendingCount = computed(() => this.store.validation()?.pendingCount ?? 0);
  protected readonly lastComplete = computed(() => {
    const at = this.store.validation()?.lastCompleteEvaluationUtc;
    return at ? this.datePipe.transform(at, 'MMM d, h:mm a') : 'never';
  });

  /** The category chosen through the header counters (Holdings / Watchlist have their own chips). */
  protected readonly categoryLabel = computed(() => {
    switch (this.activeFilter()) {
      case 'Entry':
        return 'Entry / Add';
      case 'ReduceExit':
        return 'Trim / Exit';
      case 'Risk':
        return 'Risk Review';
      default:
        return null;
    }
  });

  protected clearFilter(): void {
    this.store.setFilter('All');
  }

  protected select(filter: ActionCenterFilter): void {
    this.store.toggleFilter(filter);
  }

  protected reload(): void {
    this.store.load();
  }

  protected bucketLabel(d: OpenDecision): string {
    return d.bucket === 'EntryAdd' ? 'Entry / Add' : d.bucket === 'TrimExit' ? 'Trim / Exit' : 'Risk review';
  }

  protected bucketTone(d: OpenDecision): string {
    return d.bucket === 'EntryAdd' ? 'entry' : d.bucket === 'TrimExit' ? 'exit' : 'risk';
  }

  /** "Not Actioned" is the investor's own earlier choice, not a system status, so it is shown as recorded. */
  protected responseLabel(d: OpenDecision): string {
    return d.myAction === 'Pending' ? 'Pending review' : d.myAction;
  }
}
