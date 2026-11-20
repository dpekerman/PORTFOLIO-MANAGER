import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Skeleton pulse placeholder — renders an animated shimmer block.
 * Use width/height via CSS class or inline style on the host element.
 */
@Component({
  selector: 'app-skeleton',
  templateUrl: './skeleton.component.html',
  styleUrl: './skeleton.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SkeletonComponent {
  readonly width = input('100%');
  readonly height = input('1rem');
  readonly circle = input(false);
  readonly borderRadius = input('6px');
}
