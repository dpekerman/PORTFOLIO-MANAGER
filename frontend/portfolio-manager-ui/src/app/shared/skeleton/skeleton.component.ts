import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Skeleton pulse placeholder — renders an animated shimmer block.
 * Use width/height via CSS class or inline style on the host element.
 */
@Component({
  selector: 'app-skeleton',
  template: `
    <div
      class="skeleton-block"
      [class.skeleton-circle]="circle()"
      [style.width]="width()"
      [style.height]="height()"
      [style.border-radius]="circle() ? '50%' : borderRadius()"
      role="status"
      aria-label="Loading…"
    ></div>
  `,
  styles: [
    `
      .skeleton-block {
        background: linear-gradient(
          90deg,
          var(--skeleton-base, #1e1e1e) 25%,
          var(--skeleton-shine, #2a2a2a) 50%,
          var(--skeleton-base, #1e1e1e) 75%
        );
        background-size: 200% 100%;
        animation: skeleton-shimmer 1.4s ease-in-out infinite;
        border-radius: 6px;
        display: block;
      }
      @keyframes skeleton-shimmer {
        0% {
          background-position: 200% 0;
        }
        100% {
          background-position: -200% 0;
        }
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SkeletonComponent {
  readonly width = input('100%');
  readonly height = input('1rem');
  readonly circle = input(false);
  readonly borderRadius = input('6px');
}
