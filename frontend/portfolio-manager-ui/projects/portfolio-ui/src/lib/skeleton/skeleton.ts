import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'ui-skeleton',
  imports: [],
  templateUrl: './skeleton.html',
  styleUrl: './skeleton.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    role: 'status',
    '[attr.aria-label]': 'label()',
    '[class.skeleton--circle]': 'circle()',
    '[style.width]': 'width()',
    '[style.height]': 'height()',
  },
})
export class Skeleton {
  readonly width = input('100%');
  readonly height = input('1rem');
  readonly circle = input(false);
  readonly label = input('Loading');
}
