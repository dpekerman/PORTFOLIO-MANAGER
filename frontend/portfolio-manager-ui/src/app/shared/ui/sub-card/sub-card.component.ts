import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/** Titled inner card used inside a settings section; body is projected. */
@Component({
  selector: 'app-sub-card',
  templateUrl: './sub-card.component.html',
  styleUrl: './sub-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
})
export class SubCardComponent {
  readonly icon = input<string>('');
  readonly title = input.required<string>();
}
