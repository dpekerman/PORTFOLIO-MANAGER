import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type StatusBadgeTone = 'neutral' | 'positive' | 'negative' | 'warning' | 'info';

@Component({
  selector: 'ui-status-badge',
  imports: [],
  templateUrl: './status-badge.html',
  styleUrl: './status-badge.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[attr.data-tone]': 'tone()',
  },
})
export class StatusBadge {
  readonly tone = input<StatusBadgeTone>('neutral');
}
