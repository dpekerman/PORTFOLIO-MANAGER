import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatTooltipModule } from '@angular/material/tooltip';

/**
 * Presentation-only Final Action pill shared by Swing Trading, Portfolio and Watchlist. The label, colour bucket
 * (`ma-*`, from the canonical Final Action result) and tooltip are supplied by the caller; nothing is derived here.
 */
@Component({
  selector: 'app-final-action-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatTooltipModule],
  templateUrl: './final-action-badge.component.html',
  styleUrl: './final-action-badge.component.scss',
})
export class FinalActionBadgeComponent {
  readonly label = input.required<string>();
  /** Colour bucket class such as `ma-hold`; unknown or missing buckets render neutral. */
  readonly bucket = input<string | null>(null);
  readonly tooltip = input('');
  readonly ariaLabel = input<string | null>(null);
  /** No details are available (for example an aggregated multi-lot action): stays focusable but does not activate. */
  readonly disabled = input(false);
  readonly activated = output<void>();

  protected onClick(): void {
    if (!this.disabled()) this.activated.emit();
  }
}
