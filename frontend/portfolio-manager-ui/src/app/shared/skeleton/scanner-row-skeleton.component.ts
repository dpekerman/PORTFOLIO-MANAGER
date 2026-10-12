import { ChangeDetectionStrategy, Component } from '@angular/core';
import { SkeletonComponent } from './skeleton.component';

/**
 * Skeleton for a scanner table row — shimmer row placeholder.
 */
@Component({
  selector: 'app-scanner-row-skeleton',
  templateUrl: './scanner-row-skeleton.component.html',
  styleUrl: './scanner-row-skeleton.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SkeletonComponent],
})
export class ScannerRowSkeletonComponent {
  protected readonly rows = [1, 2, 3, 4, 5];
}
