import { ChangeDetectionStrategy, Component } from '@angular/core';
import { SkeletonComponent } from './skeleton.component';

/**
 * Skeleton for a scanner table row — shimmer row placeholder.
 */
@Component({
  selector: 'app-scanner-row-skeleton',
  template: `
    <div class="table-skeleton">
      @for (row of rows; track row) {
        <div class="skeleton-row">
          <app-skeleton width="60px" height="14px" />
          <app-skeleton width="50px" height="14px" />
          <app-skeleton width="70px" height="14px" />
          <app-skeleton width="60px" height="14px" />
          <div class="chip-row">
            <app-skeleton width="64px" height="22px" [borderRadius]="'16px'" />
            <app-skeleton width="64px" height="22px" [borderRadius]="'16px'" />
            <app-skeleton width="64px" height="22px" [borderRadius]="'16px'" />
          </div>
          <app-skeleton width="60px" height="22px" [borderRadius]="'16px'" />
          <app-skeleton width="90px" height="22px" [borderRadius]="'16px'" />
          <app-skeleton width="140px" height="12px" />
        </div>
      }
    </div>
  `,
  styles: [
    `
      .table-skeleton {
        background: var(--bg-surface);
        border-radius: 8px;
        overflow: hidden;
      }
      .skeleton-row {
        display: grid;
        grid-template-columns: 80px 60px 80px 70px 1fr 80px 110px 1fr;
        gap: 16px;
        align-items: center;
        padding: 14px 20px;
        border-bottom: 1px solid var(--border-color);
        &:last-child {
          border-bottom: none;
        }
      }
      .chip-row {
        display: flex;
        gap: 6px;
      }

      @media (max-width: 900px) {
        .skeleton-row {
          grid-template-columns: 80px 60px 80px 70px;
          .chip-row,
          :nth-child(6),
          :nth-child(7),
          :nth-child(8) {
            display: none;
          }
        }
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SkeletonComponent],
})
export class ScannerRowSkeletonComponent {
  protected readonly rows = [1, 2, 3, 4, 5];
}
