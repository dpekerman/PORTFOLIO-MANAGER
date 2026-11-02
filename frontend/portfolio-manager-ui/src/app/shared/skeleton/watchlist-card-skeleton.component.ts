import { ChangeDetectionStrategy, Component } from '@angular/core';
import { SkeletonComponent } from './skeleton.component';

/**
 * Skeleton for a watchlist card — shimmer placeholder matching the card layout.
 */
@Component({
  selector: 'app-watchlist-card-skeleton',
  templateUrl: './watchlist-card-skeleton.component.html',
  styleUrl: './watchlist-card-skeleton.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SkeletonComponent],
})
export class WatchlistCardSkeletonComponent {}
