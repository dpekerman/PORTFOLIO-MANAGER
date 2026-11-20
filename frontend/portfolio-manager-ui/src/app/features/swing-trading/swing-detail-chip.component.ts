import { ChangeDetectionStrategy, Component, input, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

export type DetailChipTone = 'ok' | 'warn' | 'bad' | 'dim';

/**
 * A compact status pill whose explanation opens on hover/focus (tooltip) and on click/tap/Enter/Space (inline note),
 * so the detail is never hover-only.
 */
@Component({
  selector: 'app-swing-detail-chip',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, MatTooltipModule],
  templateUrl: './swing-detail-chip.component.html',
  styleUrl: './swing-detail-chip.component.scss',
})
export class SwingDetailChipComponent {
  readonly label = input.required<string>();
  readonly detail = input.required<string>();
  readonly tone = input<DetailChipTone>('dim');
  readonly icon = input<string | null>(null);
  readonly testId = input<string | null>(null);
  protected readonly open = signal(false);

  protected toggle(): void {
    this.open.update((v) => !v);
  }
}
