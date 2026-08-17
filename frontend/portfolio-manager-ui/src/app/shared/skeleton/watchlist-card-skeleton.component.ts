import { ChangeDetectionStrategy, Component } from '@angular/core';
import { SkeletonComponent } from './skeleton.component';

/**
 * Skeleton for a watchlist card — shimmer placeholder matching the card layout.
 */
@Component({
  selector: 'app-watchlist-card-skeleton',
  template: `
    <div class="wl-skeleton">
      <div class="wl-header">
        <app-skeleton width="40px" height="40px" [circle]="true" />
        <div class="wl-header-text">
          <app-skeleton width="80px" height="14px" />
          <app-skeleton width="130px" height="10px" />
        </div>
        <app-skeleton width="20px" height="20px" />
      </div>
      <app-skeleton width="120px" height="28px" />
      <div class="wl-row">
        <app-skeleton width="60px" height="10px" />
        <app-skeleton width="60px" height="10px" />
        <app-skeleton width="60px" height="10px" />
      </div>
      <app-skeleton width="80px" height="10px" />
    </div>
  `,
  styles: [
    `
      .wl-skeleton {
        background: var(--bg-surface);
        border: 1px solid var(--border-color);
        border-radius: 12px;
        padding: 16px;
        display: flex;
        flex-direction: column;
        gap: 12px;
      }
      .wl-header {
        display: flex;
        align-items: center;
        gap: 10px;
        & > :last-child {
          margin-left: auto;
        }
      }
      .wl-header-text {
        display: flex;
        flex-direction: column;
        gap: 6px;
        flex: 1;
      }
      .wl-row {
        display: flex;
        gap: 16px;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SkeletonComponent],
})
export class WatchlistCardSkeletonComponent {}
