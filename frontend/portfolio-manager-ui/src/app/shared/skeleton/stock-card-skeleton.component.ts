import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { SkeletonComponent } from './skeleton.component';

/**
 * Skeleton for a stock card — matches the real card layout with
 * shimmer placeholders for ticker, price, metrics rows.
 */
@Component({
  selector: 'app-stock-card-skeleton',
  template: `
    <div class="card-skeleton">
      <!-- Header row: avatar + ticker + company -->
      <div class="card-header">
        <app-skeleton width="40px" height="40px" [circle]="true" />
        <div class="header-text">
          <app-skeleton width="80px" height="14px" />
          <app-skeleton width="140px" height="10px" />
        </div>
        <app-skeleton width="80px" height="20px" />
      </div>
      <!-- Price row -->
      <div class="price-row">
        <app-skeleton width="110px" height="26px" />
        <app-skeleton width="90px" height="16px" />
      </div>
      <!-- Metrics row -->
      <div class="metrics-row">
        @for (i of items(); track i) {
          <div class="metric">
            <app-skeleton width="50px" height="10px" />
            <app-skeleton width="70px" height="14px" />
          </div>
        }
      </div>
    </div>
  `,
  styles: [
    `
      .card-skeleton {
        background: var(--bg-surface);
        border: 1px solid var(--border-color);
        border-radius: 12px;
        padding: 16px;
        display: flex;
        flex-direction: column;
        gap: 14px;
      }
      .card-header {
        display: flex;
        align-items: center;
        gap: 10px;
        & > :last-child {
          margin-left: auto;
        }
      }
      .header-text {
        display: flex;
        flex-direction: column;
        gap: 6px;
        flex: 1;
      }
      .price-row {
        display: flex;
        align-items: center;
        justify-content: space-between;
      }
      .metrics-row {
        display: grid;
        grid-template-columns: repeat(3, 1fr);
        gap: 10px;
      }
      .metric {
        display: flex;
        flex-direction: column;
        gap: 5px;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SkeletonComponent],
})
export class StockCardSkeletonComponent {
  readonly count = input(6);
  protected readonly items = () => Array.from({ length: this.count() }, (_, i) => i);
}
