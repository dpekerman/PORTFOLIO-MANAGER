import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { SkeletonComponent } from './skeleton.component';

/**
 * Skeleton for a stock card — matches the real card layout with
 * shimmer placeholders for ticker, price, metrics rows.
 */
@Component({
  selector: 'app-stock-card-skeleton',
  templateUrl: './stock-card-skeleton.component.html',
  styleUrl: './stock-card-skeleton.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SkeletonComponent],
})
export class StockCardSkeletonComponent {
  readonly count = input(6);
  protected readonly items = () => Array.from({ length: this.count() }, (_, i) => i);
}
